using Microsoft.Data.Sqlite;

namespace MiniTime.Data;

public sealed record ResultadoReparo(bool Integro, List<string> Correcoes, List<string> Problemas);

/// <summary>Backup, restauração, reorganização e reparo do banco SQLite.</summary>
public sealed class ManutencaoService(MiniTimeDb db)
{
    /// <summary>Cópia consistente do banco (VACUUM INTO) — pode ser feita com o programa em uso.</summary>
    public void Backup(string destino)
    {
        if (File.Exists(destino)) File.Delete(destino);
        using var c = db.Abrir();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "VACUUM INTO @d";
        cmd.Parameters.AddWithValue("@d", destino);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Confere se o arquivo é um banco do MiniTime utilizável; devolve a mensagem de erro ou null.</summary>
    public static string? ValidarArquivoBackup(string arquivo)
    {
        if (!File.Exists(arquivo)) return "Arquivo não encontrado.";
        try
        {
            var csb = new SqliteConnectionStringBuilder { DataSource = arquivo, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
            using var c = new SqliteConnection(csb.ToString());
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name IN ('Marcacao','Funcionario','Parametros')";
            if (Convert.ToInt32(cmd.ExecuteScalar()) < 3) return "O arquivo não é um banco de dados do MiniTime.";
            cmd.CommandText = "PRAGMA user_version";
            if (Convert.ToInt32(cmd.ExecuteScalar()) > Schema.VersaoAtual) return "O backup foi gerado por uma versão mais nova do programa.";
            cmd.CommandText = "PRAGMA quick_check";
            if (!string.Equals(cmd.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase)) return "O arquivo de backup está corrompido.";
            return null;
        }
        catch (SqliteException) { return "O arquivo não é um banco de dados válido."; }
    }

    /// <summary>
    /// Restaura um backup sobre o banco atual. Antes, guarda uma cópia de segurança ao lado do banco
    /// (minitime-antes-da-restauracao-*.db) e devolve o caminho dela. O chamador deve reabrir o banco depois.
    /// </summary>
    public static string Restaurar(string backup, string bancoAtual)
    {
        var erro = ValidarArquivoBackup(backup);
        if (erro is not null) throw new InvalidOperationException(erro);
        SqliteConnection.ClearAllPools();
        string? seguranca = null;
        if (File.Exists(bancoAtual))
        {
            seguranca = Path.Combine(Path.GetDirectoryName(bancoAtual) ?? ".", $"minitime-antes-da-restauracao-{DateTime.Now:yyyyMMdd-HHmmss}.db");
            File.Copy(bancoAtual, seguranca, overwrite: false);
        }
        foreach (var sufixo in new[] { "-wal", "-shm" })
            if (File.Exists(bancoAtual + sufixo)) File.Delete(bancoAtual + sufixo);
        File.Copy(backup, bancoAtual, overwrite: true);
        return seguranca ?? "";
    }

    /// <summary>Reorganização: refaz índices/estatísticas e compacta o arquivo.</summary>
    public void Reorganizar()
    {
        using var c = db.Abrir();
        foreach (var sql in new[] { "REINDEX", "ANALYZE", "VACUUM" })
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Verifica a integridade e corrige inconsistências de Marcações e Horários (função "Reparar").</summary>
    public ResultadoReparo Reparar()
    {
        var correcoes = new List<string>();
        var problemas = new List<string>();
        using var c = db.Abrir();
        string Escalar(string sql) { using var cmd = c.CreateCommand(); cmd.CommandText = sql; return Convert.ToString(cmd.ExecuteScalar()) ?? ""; }

        var integridade = Escalar("PRAGMA integrity_check");
        var integro = integridade.Equals("ok", StringComparison.OrdinalIgnoreCase);
        if (!integro) problemas.Add("Falha de integridade do arquivo: " + integridade);

        using var tx = c.BeginTransaction();
        void Corrige(string descricao, string sql)
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            var n = cmd.ExecuteNonQuery();
            if (n > 0) correcoes.Add($"{descricao}: {n} registro(s)");
        }

        Corrige("Marcações com cartão fora do padrão de 16 posições corrigidas", "UPDATE Marcacao SET Cracha = substr('0000000000000000' || trim(Cracha), -16, 16) WHERE length(Cracha) <> 16");
        Corrige("Marcações duplicadas removidas", "DELETE FROM Marcacao WHERE Id NOT IN (SELECT MIN(Id) FROM Marcacao GROUP BY Cracha, DataHora, Tipo)");
        Corrige("Marcações com data inválida removidas", "DELETE FROM Marcacao WHERE DataHora < '1990-01-01' OR DataHora > '2999-12-31'");
        Corrige("Férias com período invertido removidas", "DELETE FROM Ferias WHERE Fim < Inicio");
        Corrige("Férias de funcionários inexistentes removidas", "DELETE FROM Ferias WHERE Funcionario NOT IN (SELECT Codigo FROM Funcionario)");
        Corrige("Templates de funcionários inexistentes removidos", "DELETE FROM Templates WHERE CodigoCartao NOT IN (SELECT Codigo FROM Funcionario)");
        Corrige("Horários com tolerância negativa ajustados", "UPDATE Horarios SET TolManha=MAX(TolManha,0), TolTarde=MAX(TolTarde,0), TolSaida=MAX(TolSaida,0), TolExtEnt=MAX(TolExtEnt,0), TolExtInt=MAX(TolExtInt,0), TolExtSai=MAX(TolExtSai,0) WHERE TolManha<0 OR TolTarde<0 OR TolSaida<0 OR TolExtEnt<0 OR TolExtInt<0 OR TolExtSai<0");
        tx.Commit();

        var jornadasOrfas = Escalar("SELECT COUNT(*) FROM Funcionario WHERE Horario <> 0 AND Horario NOT IN (SELECT Codigo FROM Jornadas)");
        if (jornadasOrfas != "0") problemas.Add($"{jornadasOrfas} funcionário(s) apontam para uma jornada que não existe (abra o cadastro de cartões e escolha outra).");
        const string dias = "Segunda,Terca,Quarta,Quinta,Sexta,Sabado,Domingo";
        var cond = string.Join(" OR ", dias.Split(',').Select(d => $"({d}<>0 AND {d} NOT IN (SELECT Codigo FROM Horarios))"));
        var horariosOrfaos = Escalar($"SELECT COUNT(*) FROM Jornadas WHERE {cond}");
        if (horariosOrfaos != "0") problemas.Add($"{horariosOrfaos} jornada(s) usam um horário de trabalho que não existe (revise o cadastro de jornadas).");
        return new ResultadoReparo(integro && problemas.Count == 0, correcoes, problemas);
    }
}
