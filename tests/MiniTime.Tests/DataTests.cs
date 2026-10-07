using MiniTime.Core.Models;
using MiniTime.Core.Util;
using MiniTime.Data;

namespace MiniTime.Tests;

public class DataTests
{
    private static Repositorios Novo() => new(new MiniTimeDb(":memory:"));

    [Fact]
    public void Schema_cria_parametros_padrao()
    {
        var r = Novo();
        var p = r.Parametros.Obter();
        Assert.Equal(19200, p.Velocidade);
        Assert.Equal("Mini Point", p.TipoRelogio);
    }

    [Fact]
    public void Funcionario_crud_com_horas_e_booleanos()
    {
        var r = Novo();
        var f = new Funcionario
        {
            Codigo = CodigoCartao.Normalizar("7984"), Nome = "Fulano", Horario = 1, AutHoraExtra = 1,
            HrMudancaData = new TimeSpan(4, 30, 0), HorarioNoturno = true,
        };
        r.Funcionarios.Inserir(f);
        var lido = r.Funcionarios.Obter("0000000000007984")!;
        Assert.Equal("Fulano", lido.Nome);
        Assert.Equal(new TimeSpan(4, 30, 0), lido.HrMudancaData);
        Assert.True(lido.HorarioNoturno);
        Assert.False(lido.HorarioDiurno);
        Assert.Null(lido.HrMudancaDataDiaLivre);

        lido.Nome = "Beltrano";
        r.Funcionarios.Atualizar(lido);
        Assert.Equal("Beltrano", r.Funcionarios.Obter(f.Codigo)!.Nome);
        Assert.Single(r.Funcionarios.PorNome("belt"));
        r.Funcionarios.Excluir(f.Codigo);
        Assert.Null(r.Funcionarios.Obter(f.Codigo));
    }

    [Fact]
    public void Marcacao_identity_e_periodo()
    {
        var r = Novo();
        var m = new Marcacao { Cracha = CodigoCartao.Normalizar("1"), DataHora = new DateTime(2026, 8, 3, 7, 58, 0), Terminal = 1 };
        r.Marcacoes.Inserir(m);
        Assert.True(m.Id > 0);
        r.Marcacoes.Inserir(new Marcacao { Cracha = m.Cracha, DataHora = new DateTime(2026, 9, 3, 7, 58, 0) });
        var ago = r.Marcacoes.Periodo(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31, 23, 59, 59), "1");
        Assert.Single(ago);
        Assert.True(r.Marcacoes.Existe("1", new DateTime(2026, 8, 3, 7, 58, 0)));
    }

    [Fact]
    public void Jornada_escolhe_horario_por_dia()
    {
        var j = new Jornada { Segunda = 1, Sabado = 2 };
        Assert.Equal(1, j.HorarioDoDia(DayOfWeek.Monday));
        Assert.Equal(2, j.HorarioDoDia(DayOfWeek.Saturday));
        Assert.Equal(0, j.HorarioDoDia(DayOfWeek.Sunday));
    }

    [Theory]
    [InlineData("7984", "0000000000007984")]
    [InlineData(" 12 ", "0000000000000012")]
    public void Cartao_normaliza(string entrada, string esperado) => Assert.Equal(esperado, CodigoCartao.Normalizar(entrada));
}

public class ColetaServiceTests
{
    [Fact]
    public void Coleta_grava_sem_duplicar_e_recusa_datas_absurdas()
    {
        var db = new MiniTimeDb(":memory:");
        var rep = new Repositorios(db);
        var svc = new ColetaService(db);
        var agora = new DateTime(2026, 10, 7, 12, 0, 0);
        var lote = new[]
        {
            ("7984", new DateTime(2026, 10, 7, 9, 0, 30)),
            ("7984", new DateTime(2026, 10, 7, 9, 0, 59)),   // mesma marcação no mesmo minuto → repetida
            ("7984", new DateTime(2150, 7, 14, 12, 2, 0)),   // data absurda
            ("0000000000008002", new DateTime(2026, 10, 7, 9, 5, 0)),
        };
        var r = svc.Gravar(lote, terminal: 1, agora);
        Assert.Equal(new ResultadoColeta(4, 2, 1, 1), r);
        Assert.Equal(2, rep.Marcacoes.Contar());
        Assert.Equal(2, rep.Backup.Contar());
        var segunda = svc.Gravar(lote, 1, agora);
        Assert.Equal(0, segunda.Gravadas);
        Assert.All(rep.Marcacoes.Todos(), m => { Assert.Equal(7, m.Tipo); Assert.Equal(16, m.Cracha.Length); });
    }
}
