using MiniTime.Core.Models;
using MiniTime.Core.Seguranca;

namespace MiniTime.Tests;

public class AcessoTests
{
    [Fact]
    public void Sem_usuario_o_acesso_e_livre_e_com_usuario_respeita_o_nivel()
    {
        Assert.True(Acesso.Permite(null, NivelAcesso.Administrador));
        var consulta = new Usuario { Nome = "c", Nivel = NivelAcesso.Consulta };
        var completo = new Usuario { Nome = "m", Nivel = NivelAcesso.Completo };
        var admin = new Usuario { Nome = "a", Nivel = NivelAcesso.Administrador };
        Assert.True(Acesso.Permite(consulta, NivelAcesso.Consulta));
        Assert.False(Acesso.Permite(consulta, NivelAcesso.Completo));
        Assert.True(Acesso.Permite(completo, NivelAcesso.Completo));
        Assert.False(Acesso.Permite(completo, NivelAcesso.Administrador));
        Assert.True(Acesso.Permite(admin, NivelAcesso.Administrador));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("aaaaaaaa")]
    public void Senhas_fracas_sao_recusadas(string? senha) => Assert.NotNull(PoliticaSenha.Validar(senha));

    [Fact]
    public void Senha_aceitavel_passa() => Assert.Null(PoliticaSenha.Validar("abc123"));

    [Fact]
    public void Limitador_bloqueia_apos_o_maximo_e_dobra_no_ciclo_seguinte()
    {
        var t0 = new DateTime(2026, 1, 1, 8, 0, 0);
        var l = new LimitadorTentativas(3, TimeSpan.FromMinutes(1));
        l.RegistrarFalha(t0); l.RegistrarFalha(t0);
        Assert.False(l.Bloqueado(t0));
        l.RegistrarFalha(t0);
        Assert.True(l.Bloqueado(t0));
        Assert.Equal(TimeSpan.FromMinutes(1), l.Restante(t0));
        Assert.False(l.Bloqueado(t0.AddMinutes(1)));
        for (var i = 0; i < 3; i++) l.RegistrarFalha(t0.AddMinutes(1));
        Assert.Equal(TimeSpan.FromMinutes(2), l.Restante(t0.AddMinutes(1)));
        l.RegistrarSucesso();
        Assert.False(l.Bloqueado(t0.AddMinutes(1)));
        Assert.Equal(0, l.Falhas);
    }

    [Fact]
    public void Limitador_restaurado_mantem_o_bloqueio()
    {
        var t0 = new DateTime(2026, 1, 1, 8, 0, 0);
        var l = LimitadorTentativas.Restaurar(3, t0.AddSeconds(30));
        Assert.True(l.Bloqueado(t0));
        Assert.False(l.Bloqueado(t0.AddSeconds(31)));
    }
}
