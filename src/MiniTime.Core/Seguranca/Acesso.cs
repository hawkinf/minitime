using MiniTime.Core.Models;

namespace MiniTime.Core.Seguranca;

/// <summary>Níveis de acesso do programa (Usuario.Nivel).</summary>
public static class NivelAcesso
{
    public const int Consulta = 0;
    public const int Completo = 1;
    public const int Administrador = 2;
}

/// <summary>Regras de autorização. Sem usuário autenticado (nenhum cadastrado) o acesso é livre, como no programa original.</summary>
public static class Acesso
{
    public static bool Permite(Usuario? usuario, int nivelExigido) => (usuario?.Nivel ?? NivelAcesso.Administrador) >= nivelExigido;
}

/// <summary>Política de senhas dos usuários do programa.</summary>
public static class PoliticaSenha
{
    public const int Minimo = 6;

    /// <summary>Devolve a mensagem de erro, ou null se a senha é aceitável.</summary>
    public static string? Validar(string? senha)
    {
        if (string.IsNullOrEmpty(senha) || senha.Length < Minimo) return $"A senha deve ter pelo menos {Minimo} caracteres.";
        if (senha.Distinct().Count() == 1) return "A senha não pode repetir um único caractere.";
        return null;
    }
}

/// <summary>Bloqueio temporário após falhas consecutivas de login. O estado é serializável para sobreviver ao reinício do programa.</summary>
public sealed class LimitadorTentativas(int maximo = 3, TimeSpan? bloqueio = null)
{
    private readonly TimeSpan _bloqueio = bloqueio ?? TimeSpan.FromMinutes(1);
    public int Falhas { get; private set; }
    public DateTime? BloqueadoAte { get; private set; }

    public static LimitadorTentativas Restaurar(int falhas, DateTime? bloqueadoAte, int maximo = 3, TimeSpan? bloqueio = null)
        => new(maximo, bloqueio) { Falhas = falhas, BloqueadoAte = bloqueadoAte };

    public bool Bloqueado(DateTime agora) => BloqueadoAte is { } ate && agora < ate;

    public TimeSpan Restante(DateTime agora) => Bloqueado(agora) ? BloqueadoAte!.Value - agora : TimeSpan.Zero;

    /// <summary>Registra uma falha; ao atingir o máximo, bloqueia (a cada novo ciclo o tempo dobra, até 16x).</summary>
    public void RegistrarFalha(DateTime agora)
    {
        Falhas++;
        if (Falhas % maximo != 0) return;
        var ciclo = Math.Min(Falhas / maximo - 1, 4);
        BloqueadoAte = agora + _bloqueio * (1 << ciclo);
    }

    public void RegistrarSucesso()
    {
        Falhas = 0;
        BloqueadoAte = null;
    }
}
