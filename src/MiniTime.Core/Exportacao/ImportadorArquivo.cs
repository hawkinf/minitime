using System.Globalization;
using MiniTime.Core.Util;

namespace MiniTime.Core.Exportacao;

public sealed record LinhaImportada(string Cartao, DateTime DataHora, int Tipo, int Relogio);

public sealed record ResultadoLeitura(List<LinhaImportada> Linhas, List<(int Numero, string Texto, string Motivo)> Invalidas);

/// <summary>Lê o arquivo de marcações no mesmo layout gerado por <see cref="ExportadorMarcacoes"/> (MOVIMENT.TXT).</summary>
public static class ImportadorArquivo
{
    public static ResultadoLeitura Ler(IEnumerable<string> linhas, int digitosCartao, int digitosAno)
    {
        var res = new ResultadoLeitura([], []);
        var n = 0;
        foreach (var bruta in linhas)
        {
            n++;
            var l = bruta.Trim();
            if (l.Length == 0) continue;
            var minimo = digitosCartao + (digitosAno == 4 ? 8 : 6) + 4 + 2;
            if (l.Length < minimo || !l.All(char.IsDigit)) { res.Invalidas.Add((n, l, "Formato inválido")); continue; }
            var p = 0;
            string Pega(int len) { var s = l.Substring(p, len); p += len; return s; }
            var cartao = Pega(digitosCartao);
            var dia = int.Parse(Pega(2), CultureInfo.InvariantCulture);
            var mes = int.Parse(Pega(2), CultureInfo.InvariantCulture);
            var ano = int.Parse(Pega(digitosAno), CultureInfo.InvariantCulture);
            if (digitosAno == 2) ano += ano < 80 ? 2000 : 1900;
            var hh = int.Parse(Pega(2), CultureInfo.InvariantCulture);
            var mm = int.Parse(Pega(2), CultureInfo.InvariantCulture);
            var tipo = int.Parse(Pega(2), CultureInfo.InvariantCulture);
            var relogio = 0;
            if (l.Length >= p + 2 + 3) { p += 2; relogio = int.Parse(Pega(3), CultureInfo.InvariantCulture); }

            if (mes is < 1 or > 12 || dia < 1 || dia > DateTime.DaysInMonth(ano, mes) || hh > 23 || mm > 59)
            {
                res.Invalidas.Add((n, l, "Data ou hora inválida"));
                continue;
            }
            res.Linhas.Add(new LinhaImportada(CodigoCartao.Normalizar(cartao), new DateTime(ano, mes, dia, hh, mm, 0), tipo, relogio));
        }
        return res;
    }
}
