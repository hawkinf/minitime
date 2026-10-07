using System.Security.Cryptography;
using System.Text;

namespace MiniTime.App.Services;

/// <summary>Protege segredos guardados em disco com DPAPI (escopo do usuário do Windows): só este usuário, neste computador, consegue ler.</summary>
public static class SegredoLocal
{
    private static readonly byte[] Entropia = "MiniTime.v1"u8.ToArray();

    public static string? Proteger(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return null;
        var cifrado = ProtectedData.Protect(Encoding.UTF8.GetBytes(texto), Entropia, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cifrado);
    }

    /// <summary>Devolve null se o texto não puder ser aberto (outro usuário/computador ou arquivo adulterado).</summary>
    public static string? Desproteger(string? protegido)
    {
        if (string.IsNullOrEmpty(protegido)) return null;
        try
        {
            var claro = ProtectedData.Unprotect(Convert.FromBase64String(protegido), Entropia, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(claro);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException) { return null; }
    }
}
