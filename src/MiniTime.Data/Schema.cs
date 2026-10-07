using Microsoft.Data.Sqlite;

namespace MiniTime.Data;

/// <summary>DDL do banco SQLite e migrations (controladas por PRAGMA user_version).</summary>
public static class Schema
{
    public const int VersaoAtual = 1;

    // Horários do dia (TimeSpan) ficam como TEXT 'HH:mm'; datas como TEXT ISO 'yyyy-MM-dd HH:mm:ss'.
    private static readonly string[] V1 =
    [
        """
        CREATE TABLE Funcionario(
            Codigo TEXT NOT NULL PRIMARY KEY,
            Nome TEXT NOT NULL DEFAULT '',
            Cargo TEXT, RG TEXT,
            Horario INTEGER NOT NULL DEFAULT 0,
            HorarioAlmoco INTEGER NOT NULL DEFAULT 0,
            AutHoraExtra INTEGER NOT NULL DEFAULT 0,
            HrMudancaData TEXT,
            HorarioNoturno INTEGER NOT NULL DEFAULT 0,
            HorarioDiurno INTEGER NOT NULL DEFAULT 0,
            HrMudancaDataDiaLivre TEXT,
            SentidoMudancaDataDiaLivre INTEGER NOT NULL DEFAULT 0,
            Senha TEXT)
        """,
        "CREATE INDEX IX_Funcionario_Nome ON Funcionario(Nome)",
        """
        CREATE TABLE Horarios(
            Codigo INTEGER NOT NULL PRIMARY KEY,
            Descricao TEXT NOT NULL DEFAULT '',
            DeSS1 TEXT, DeSS2 TEXT, DeSS3 TEXT, DeSS4 TEXT,
            TolManha INTEGER NOT NULL DEFAULT 0, TolTarde INTEGER NOT NULL DEFAULT 0, TolSaida INTEGER NOT NULL DEFAULT 0,
            RefObrig INTEGER NOT NULL DEFAULT 0, RefMinimo INTEGER NOT NULL DEFAULT 0,
            Intervalo INTEGER NOT NULL DEFAULT 0, Noturno INTEGER NOT NULL DEFAULT 0, NaoTrabalha INTEGER NOT NULL DEFAULT 0,
            TolExtEnt INTEGER NOT NULL DEFAULT 0, TolExtInt INTEGER NOT NULL DEFAULT 0, TolExtSai INTEGER NOT NULL DEFAULT 0)
        """,
        """
        CREATE TABLE Jornadas(
            Codigo INTEGER NOT NULL PRIMARY KEY,
            Descricao TEXT NOT NULL DEFAULT '',
            Segunda INTEGER NOT NULL DEFAULT 0, Terca INTEGER NOT NULL DEFAULT 0, Quarta INTEGER NOT NULL DEFAULT 0,
            Quinta INTEGER NOT NULL DEFAULT 0, Sexta INTEGER NOT NULL DEFAULT 0, Sabado INTEGER NOT NULL DEFAULT 0,
            Domingo INTEGER NOT NULL DEFAULT 0,
            NaoMarcarFalta INTEGER NOT NULL DEFAULT 0, TrataDSR INTEGER NOT NULL DEFAULT 0,
            JornSemanal INTEGER NOT NULL DEFAULT 0, DiaDSR INTEGER NOT NULL DEFAULT 0)
        """,
        "CREATE TABLE Feriados(Codigo INTEGER NOT NULL PRIMARY KEY, Descricao TEXT NOT NULL DEFAULT '', Dia INTEGER NOT NULL, Mes INTEGER NOT NULL)",
        "CREATE INDEX IX_Feriados_DiaMes ON Feriados(Mes, Dia)",
        "CREATE TABLE Ferias(Id INTEGER PRIMARY KEY AUTOINCREMENT, Funcionario TEXT NOT NULL, Inicio TEXT NOT NULL, Fim TEXT NOT NULL)",
        "CREATE INDEX IX_Ferias_Func ON Ferias(Funcionario, Inicio)",
        "CREATE TABLE Justificativas(Codigo INTEGER NOT NULL PRIMARY KEY, Descricao TEXT NOT NULL DEFAULT '', Tipo INTEGER NOT NULL DEFAULT 0)",
        """
        CREATE TABLE Marcacao(
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Cracha TEXT NOT NULL,
            DataHora TEXT NOT NULL,
            Terminal INTEGER NOT NULL DEFAULT 0,
            EntradaSaida INTEGER NOT NULL DEFAULT 0,
            Situacao INTEGER NOT NULL DEFAULT 0,
            Tipo INTEGER NOT NULL DEFAULT 0,
            Divergencia INTEGER NOT NULL DEFAULT 0,
            SaiuMarcacao INTEGER NOT NULL DEFAULT 0,
            Justificativa INTEGER NOT NULL DEFAULT 0)
        """,
        "CREATE INDEX IX_Marcacao_CrachaData ON Marcacao(Cracha, DataHora)",
        "CREATE INDEX IX_Marcacao_Data ON Marcacao(DataHora)",
        """
        CREATE TABLE Backup(
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Cracha TEXT NOT NULL, DataHora TEXT NOT NULL,
            Terminal INTEGER NOT NULL DEFAULT 0, EntradaSaida INTEGER NOT NULL DEFAULT 0,
            Situacao INTEGER NOT NULL DEFAULT 0, Tipo INTEGER NOT NULL DEFAULT 0,
            Divergencia INTEGER NOT NULL DEFAULT 0, SaiuMarcacao INTEGER NOT NULL DEFAULT 0)
        """,
        "CREATE INDEX IX_Backup_Data ON Backup(DataHora)",
        "CREATE TABLE Sirene(Codigo INTEGER NOT NULL PRIMARY KEY, Descricao TEXT NOT NULL DEFAULT '', Horario TEXT, Duracao INTEGER NOT NULL DEFAULT 0, Util INTEGER NOT NULL DEFAULT 0, FimSem INTEGER NOT NULL DEFAULT 0)",
        "CREATE TABLE SireneBioLite(Codigo INTEGER NOT NULL PRIMARY KEY, Descricao TEXT NOT NULL DEFAULT '', Horario TEXT, Util INTEGER NOT NULL DEFAULT 0, FimSem INTEGER NOT NULL DEFAULT 0)",
        "CREATE TABLE Terminal(Endereco INTEGER NOT NULL PRIMARY KEY, Porta INTEGER NOT NULL DEFAULT 1, FaixaInicio TEXT, FaixaFim TEXT, VersaoFW TEXT, Impressora INTEGER NOT NULL DEFAULT 0, PenDrive INTEGER NOT NULL DEFAULT 0)",
        "CREATE TABLE Templates(CodigoCartao TEXT NOT NULL, Dedo TEXT NOT NULL, Template BLOB, PRIMARY KEY(CodigoCartao, Dedo))",
        """
        CREATE TABLE Parametros(
            Id INTEGER NOT NULL PRIMARY KEY CHECK (Id = 1),
            NomeCliente TEXT NOT NULL DEFAULT '', Digitos TEXT, UltUsuario TEXT, CNPJ TEXT,
            DiaFechamento INTEGER NOT NULL DEFAULT 0,
            TolManha INTEGER NOT NULL DEFAULT 0, TolTarde INTEGER NOT NULL DEFAULT 0, TolSaida INTEGER NOT NULL DEFAULT 0,
            TipoExp INTEGER NOT NULL DEFAULT 0, NumCartao INTEGER NOT NULL DEFAULT 5, NumDigAno INTEGER NOT NULL DEFAULT 2,
            DigVerificador INTEGER NOT NULL DEFAULT 0, FiltroExp INTEGER NOT NULL DEFAULT 0,
            Velocidade INTEGER NOT NULL DEFAULT 19200, DisqVirtual INTEGER NOT NULL DEFAULT 0, Versao INTEGER NOT NULL DEFAULT 441,
            HorarioVeraoAtivo INTEGER NOT NULL DEFAULT 0, InicioHorarioVerao TEXT, FimHorarioVerao TEXT,
            TipoRelogio TEXT NOT NULL DEFAULT 'Mini Point', QtdeDig TEXT NOT NULL DEFAULT '04',
            Checagem INTEGER NOT NULL DEFAULT 0, ExpMonitoracao INTEGER NOT NULL DEFAULT 0,
            NomeArq TEXT, TipoEmpresa TEXT, DuracaoSireneBioLite INTEGER NOT NULL DEFAULT 0)
        """,
        "INSERT INTO Parametros(Id) VALUES (1)",
        // Usuários do programa (FrmSenhas); a estrutura final é refinada quando a spec das telas chegar.
        "CREATE TABLE Usuario(Nome TEXT NOT NULL PRIMARY KEY, SenhaHash TEXT NOT NULL, Nivel INTEGER NOT NULL DEFAULT 0)",
        // Rastreio da importação do MDB legado.
        "CREATE TABLE ImportacaoLog(Id INTEGER PRIMARY KEY AUTOINCREMENT, DataHora TEXT NOT NULL, Origem TEXT NOT NULL, Resumo TEXT NOT NULL)",
        // Linhas do MDB que não puderam ser importadas como estavam (ex.: datas absurdas).
        "CREATE TABLE ImportacaoQuarentena(Id INTEGER PRIMARY KEY AUTOINCREMENT, Tabela TEXT NOT NULL, Motivo TEXT NOT NULL, Dados TEXT NOT NULL)",
    ];

    public static void Migrar(SqliteConnection c)
    {
        var atual = Convert.ToInt32(Scalar(c, "PRAGMA user_version"));
        if (atual > VersaoAtual)
            throw new InvalidOperationException($"Banco de dados mais novo (v{atual}) que este programa (v{VersaoAtual}).");
        if (atual >= VersaoAtual) return;

        using var tx = c.BeginTransaction();
        if (atual < 1)
            foreach (var sql in V1) Exec(c, tx, sql);
        Exec(c, tx, $"PRAGMA user_version = {VersaoAtual}");
        tx.Commit();
    }

    private static object? Scalar(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    private static void Exec(SqliteConnection c, SqliteTransaction tx, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
