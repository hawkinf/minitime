using System.Security.Cryptography;

namespace MiniTime.Core.Seguranca;

/// <summary>Hash de senhas de usuários do programa (PBKDF2-SHA256 com sal individual).</summary>
public static class Senha
{
    private const int Iteracoes = 100_000;

    public static string Hash(string senha)
    {
        var sal = RandomNumberGenerator.GetBytes(16);
        var h = Rfc2898DeriveBytes.Pbkdf2(senha, sal, Iteracoes, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2${Iteracoes}${Convert.ToBase64String(sal)}${Convert.ToBase64String(h)}";
    }

    public static bool Confere(string senha, string armazenado)
    {
        var p = armazenado.Split('$');
        if (p.Length != 4 || p[0] != "pbkdf2" || !int.TryParse(p[1], out var it)) return false;
        try
        {
            var esperado = Convert.FromBase64String(p[3]);
            var h = Rfc2898DeriveBytes.Pbkdf2(senha, Convert.FromBase64String(p[2]), it, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(h, esperado);
        }
        catch (FormatException) { return false; }
    }
}
