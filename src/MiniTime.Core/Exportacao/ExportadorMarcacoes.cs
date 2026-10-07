using System.Globalization;
using System.Text;
using MiniTime.Core.Apuracao;
using MiniTime.Core.Models;

namespace MiniTime.Core.Exportacao;

public sealed record OpcoesExportacao(int DigitosCartao = 4, int DigitosAno = 2, bool SomenteNoturnos = false);

/// <summary>
/// Arquivo de marcações (MOVIMENT.TXT). Uma linha por marcação, formato do manual do MiniTime:
/// cartão (N dígitos) | data ddmmaa (ou ddmmaaaa) | hora hhmm | tipo (07 coleta / 00 inserido) | "00" | relógio (3 dígitos) | "00".
/// Exemplo: 05362002031018070000100.
/// </summary>
public static class ExportadorMarcacoes
{
    /// <summary>Marcações que entram no arquivo: coletadas e inseridas pelo usuário (sem as desprezadas nem as geradas).</summary>
    public static bool Exporta(Marcacao m)
        => m.Tipo == TipoMarcacao.Coletada
           || (m.Tipo == TipoMarcacao.Manual && m.Justificativa != TipoMarcacao.JustificativaAutomatica && m.EntradaSaida != Posicao.StatusDia);

    public static string Linha(Marcacao m, OpcoesExportacao o)
    {
        var cartao = (m.Cracha.TrimStart('0') is { Length: > 0 } c ? c : "0");
        cartao = cartao.Length >= o.DigitosCartao ? cartao[^o.DigitosCartao..] : cartao.PadLeft(o.DigitosCartao, '0');
        var data = m.DataHora.ToString(o.DigitosAno == 4 ? "ddMMyyyy" : "ddMMyy", CultureInfo.InvariantCulture);
        var hora = m.DataHora.ToString("HHmm", CultureInfo.InvariantCulture);
        var tipo = (m.Tipo & 255).ToString("00", CultureInfo.InvariantCulture);
        var relogio = Math.Clamp(m.Terminal, 0, 999).ToString("000", CultureInfo.InvariantCulture);
        return $"{cartao}{data}{hora}{tipo}00{relogio}00";
    }

    /// <summary>Gera o conteúdo do arquivo (linhas terminadas em CRLF, como o VB gravava).</summary>
    public static string Gerar(IEnumerable<Marcacao> marcacoes, OpcoesExportacao o, ISet<string>? cartoesNoturnos = null)
    {
        var sb = new StringBuilder();
        foreach (var m in marcacoes.Where(Exporta).OrderBy(m => m.DataHora).ThenBy(m => m.Cracha))
        {
            if (o.SomenteNoturnos && cartoesNoturnos is not null && !cartoesNoturnos.Contains(m.Cracha)) continue;
            sb.Append(Linha(m, o)).Append("\r\n");
        }
        return sb.ToString();
    }
}
