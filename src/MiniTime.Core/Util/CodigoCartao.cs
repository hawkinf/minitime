namespace MiniTime.Core.Util;

/// <summary>Cartões são guardados com 16 posições (zeros à esquerda), como no sistema legado.</summary>
public static class CodigoCartao
{
    public const int Tamanho = 16;

    public static string Normalizar(string? valor)
    {
        var s = (valor ?? "").Trim();
        return s.Length >= Tamanho ? s[..Tamanho] : s.PadLeft(Tamanho, '0');
    }

    /// <summary>Exibição sem zeros à esquerda (mantém ao menos um dígito).</summary>
    public static string Curto(string? codigo)
    {
        var s = (codigo ?? "").Trim().TrimStart('0');
        return s.Length == 0 ? "0" : s;
    }
}
