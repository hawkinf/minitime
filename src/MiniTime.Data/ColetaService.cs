using Microsoft.Data.Sqlite;
using MiniTime.Core.Apuracao;
using MiniTime.Core.Models;
using MiniTime.Core.Util;

namespace MiniTime.Data;

public sealed record ResultadoColeta(int Recebidas, int Gravadas, int Repetidas, int Invalidas);

/// <summary>Grava marcações coletadas (do relógio ou de arquivos) em Marcacao e Backup, sem duplicar.</summary>
public sealed class ColetaService(MiniTimeDb db)
{
    /// <summary>Marcações com data fora de [1990, agora+1 dia] são recusadas (relógio com data errada).</summary>
    public ResultadoColeta Gravar(IEnumerable<(string Cartao, DateTime DataHora)> registros, int terminal, DateTime? agora = null)
    {
        var limite = (agora ?? DateTime.Now).AddDays(1);
        int recebidas = 0, gravadas = 0, repetidas = 0, invalidas = 0;
        using var c = db.Abrir();
        // BEGIN IMMEDIATE: a checagem de duplicidade e o INSERT acontecem sob o mesmo bloqueio de escrita (duas coletas simultâneas não duplicam).
        using var tx = c.BeginTransaction(deferred: false);
        foreach (var (cartao, dh) in registros)
        {
            recebidas++;
            var dt = new DateTime(dh.Year, dh.Month, dh.Day, dh.Hour, dh.Minute, 0);
            if (dt.Year < 1990 || dt > limite) { invalidas++; continue; }
            var cracha = CodigoCartao.Normalizar(cartao);
            // Duplicata = mesma batida coletada (mesmo critério do "Reparar"); uma marcação manual no mesmo minuto é outra coisa.
            var existe = ExisteColetada(c, tx, cracha, dt);
            if (existe) { repetidas++; continue; }
            MiniTimeDb.Execute(c, tx,
                "INSERT INTO Marcacao(Cracha, DataHora, Terminal, EntradaSaida, Situacao, Tipo, Divergencia, SaiuMarcacao, Justificativa) VALUES(@c,@d,@t,0,0,@tp,0,0,0)",
                ("c", cracha), ("d", dt), ("t", terminal), ("tp", TipoMarcacao.Coletada));
            MiniTimeDb.Execute(c, tx,
                "INSERT INTO Backup(Cracha, DataHora, Terminal, EntradaSaida, Situacao, Tipo, Divergencia, SaiuMarcacao) VALUES(@c,@d,@t,0,0,@tp,0,0)",
                ("c", cracha), ("d", dt), ("t", terminal), ("tp", TipoMarcacao.Coletada));
            gravadas++;
        }
        tx.Commit();
        return new ResultadoColeta(recebidas, gravadas, repetidas, invalidas);
    }

    private static bool ExisteColetada(SqliteConnection c, SqliteTransaction tx, string cracha, DateTime dt)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT 1 FROM Marcacao WHERE Cracha=@c AND DataHora=@d AND Tipo=@tp LIMIT 1";
        cmd.Parameters.AddWithValue("@c", cracha);
        cmd.Parameters.AddWithValue("@d", MiniTimeDb.Valor(dt));
        cmd.Parameters.AddWithValue("@tp", TipoMarcacao.Coletada);
        return cmd.ExecuteScalar() is not null;
    }
}
