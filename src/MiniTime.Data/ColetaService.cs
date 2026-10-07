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
        using var tx = c.BeginTransaction();
        foreach (var (cartao, dh) in registros)
        {
            recebidas++;
            var dt = new DateTime(dh.Year, dh.Month, dh.Day, dh.Hour, dh.Minute, 0);
            if (dt.Year < 1990 || dt > limite) { invalidas++; continue; }
            var cracha = CodigoCartao.Normalizar(cartao);
            var existe = MiniTimeDb.Query<Marcacao>(c, tx, "SELECT * FROM Marcacao WHERE Cracha=@c AND DataHora=@d LIMIT 1", ("c", cracha), ("d", dt)).Count > 0;
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
}
