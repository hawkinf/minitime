using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MiniTime.Serial;

/// <summary>Quadro decodificado de uma captura, com a direção em que trafegou.</summary>
public sealed record QuadroCapturado(string Direcao, Quadro Quadro, string? Instante)
{
    /// <summary>Dados como texto ASCII quando todos os bytes são imprimíveis; senão null.</summary>
    public string? DadosComoTexto
        => Quadro.Dados.Length > 0 && Quadro.Dados.All(b => b is >= 0x20 and < 0x7F) ? Encoding.ASCII.GetString(Quadro.Dados) : null;

    public override string ToString()
    {
        var q = Quadro;
        var sb = new StringBuilder();
        if (Instante is not null) sb.Append(Instante).Append(' ');
        sb.Append(Direcao).Append(q.Ascii ? " ASCII" : " BIN  ")
          .Append($" end={Quadro.DeBcd(q.Endereco):00} cmd=0x{q.Comando:X2} tam={q.Dados.Length,3}");
        if (q.Dados.Length > 0) sb.Append(" dados=").Append(BitConverter.ToString(q.Dados).Replace('-', ' '));
        if (DadosComoTexto is { } t) sb.Append($"  \"{t}\"");
        return sb.ToString();
    }
}

/// <summary>Resultado da decodificação de uma captura.</summary>
public sealed record ResultadoDecodificacao(List<QuadroCapturado> Quadros, int ErrosChecksum, int BytesLidos);

/// <summary>
/// Decodifica capturas de tráfego serial (log hexadecimal) em quadros do protocolo.
/// Aceita o formato da tela de coleta ("14:05:01.123 TX AA FF FF FE 01 0E 00 0F F0"), logs de monitores de porta
/// serial e hex puro ("AAFFFFFE01..."). Linhas sem direção (TX/RX) contam como "RX".
/// </summary>
public static class Decodificador
{
    private static readonly Regex Instante = new(@"^\s*(\d{1,2}:\d{2}:\d{2}(?:[.,]\d+)?)\s*", RegexOptions.Compiled);
    private static readonly Regex Direcao = new(@"^\s*(TX|RX|PC|RELOGIO|->|<-)\b[:\s]*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static ResultadoDecodificacao Decodificar(IEnumerable<string> linhas)
    {
        // Um receptor por direção: os quadros de uma direção podem chegar em várias linhas.
        var receptores = new Dictionary<string, ReceptorQuadros>();
        var quadros = new List<QuadroCapturado>();
        var bytesLidos = 0;
        foreach (var linha in linhas)
        {
            var resto = linha.Trim();
            if (resto.Length == 0 || resto.StartsWith('#') || resto.StartsWith("//")) continue;
            string? instante = null;
            if (Instante.Match(resto) is { Success: true } mi) { instante = mi.Groups[1].Value; resto = resto[mi.Length..]; }
            var dir = "RX";
            if (Direcao.Match(resto) is { Success: true } md) { dir = Normalizar(md.Groups[1].Value); resto = resto[md.Length..]; }
            var bytes = LerHex(resto);
            bytesLidos += bytes.Count;
            if (!receptores.TryGetValue(dir, out var rx)) receptores[dir] = rx = new ReceptorQuadros();
            foreach (var q in rx.Alimentar(bytes.ToArray())) quadros.Add(new QuadroCapturado(dir, q, instante));
        }
        return new ResultadoDecodificacao(quadros, receptores.Values.Sum(r => r.ErrosChecksum), bytesLidos);
    }

    private static string Normalizar(string d) => d.ToUpperInvariant() switch
    {
        "TX" or "PC" or "->" => "TX",
        _ => "RX",
    };

    /// <summary>Extrai bytes de um texto hex ("AA FF", "0xAA,0xFF" ou "AAFF"); ignora o que não for hex.</summary>
    public static List<byte> LerHex(string texto)
    {
        var bytes = new List<byte>();
        foreach (var parte in Regex.Split(texto, @"[\s,;:-]+"))
        {
            var p = parte.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? parte[2..] : parte;
            if (p.Length == 0 || p.Length % 2 != 0 || !p.All(Uri.IsHexDigit)) continue;
            for (var i = 0; i < p.Length; i += 2) bytes.Add(byte.Parse(p.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }
        return bytes;
    }

    /// <summary>Resumo por comando (quantas vezes, em qual direção, tamanhos de dados) para montar a tabela de comandos.</summary>
    public static string Resumo(ResultadoDecodificacao r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{r.Quadros.Count} quadro(s) válido(s), {r.ErrosChecksum} com checksum inválido, {r.BytesLidos} byte(s) lidos.");
        foreach (var g in r.Quadros.GroupBy(q => (q.Direcao, q.Quadro.Comando)).OrderBy(g => g.Key.Direcao).ThenBy(g => g.Key.Comando))
        {
            var tams = string.Join(",", g.Select(q => q.Quadro.Dados.Length).Distinct().OrderBy(x => x));
            sb.AppendLine($"  {g.Key.Direcao} cmd=0x{g.Key.Comando:X2}  x{g.Count(),-4} tamanhos de dados: {tams}");
        }
        return sb.ToString();
    }
}
