using MiniTime.Core.Apuracao;
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

public class ApuracaoServiceTests
{
    private static (Repositorios r, ApuracaoService s, Funcionario f) Cenario()
    {
        var db = new MiniTimeDb(":memory:");
        var r = new Repositorios(db);
        r.Horarios.Inserir(new Horario { Codigo = 1, Descricao = "H", DeSS1 = new(9, 0, 0), DeSS2 = new(11, 30, 0), DeSS3 = new(12, 30, 0), DeSS4 = new(18, 0, 0), TolManha = 5, TolTarde = 5, TolSaida = 5, Intervalo = 1, RefMinimo = 60 });
        r.Jornadas.Inserir(new Jornada { Codigo = 1, Segunda = 1, Terca = 1, Quarta = 1, Quinta = 1, Sexta = 1 });
        r.Justificativas.Inserir(new Justificativa { Codigo = 1, Descricao = "Atestado", Tipo = 3 });
        var f = new Funcionario { Codigo = CodigoCartao.Normalizar("7984"), Nome = "Fulano", Horario = 1 };
        r.Funcionarios.Inserir(f);
        return (r, new ApuracaoService(r), f);
    }

    [Fact]
    public void Apura_grava_classificacao_e_justifica_dia()
    {
        var (r, s, f) = Cenario();
        var seg = new DateTime(2026, 7, 6);
        new ColetaService(r.Db).Gravar(
            new[] { "09:00", "11:30", "12:30", "18:00" }.Select(h => ("7984", seg + TimeSpan.Parse(h))), 1, new DateTime(2026, 8, 1));
        var res = s.Apurar(f, seg, seg.AddDays(1), hoje: new DateTime(2026, 8, 1));
        Assert.Equal(StatusDia.Normal, res.Dias[0].Status);
        Assert.Equal(StatusDia.Falta, res.Dias[1].Status);
        Assert.Equal(new[] { 1, 2, 3, 4 }, r.Marcacoes.Periodo(seg, seg.AddDays(1), f.Codigo).Select(m => m.EntradaSaida));

        s.JustificarDia(f, seg.AddDays(1), 1);
        var depois = s.Apurar(f, seg, seg.AddDays(1), hoje: new DateTime(2026, 8, 1));
        Assert.Equal(StatusDia.Abonado, depois.Dias[1].Status);
        Assert.Equal(480, depois.Totais.Abonado);
        Assert.Equal(0, depois.Totais.Saldo);
        s.RemoverJustificativaDia(f, seg.AddDays(1));
        Assert.Equal(StatusDia.Falta, s.Apurar(f, seg, seg.AddDays(1), hoje: new DateTime(2026, 8, 1)).Dias[1].Status);
    }

    [Fact]
    public void Incluir_desprezar_e_restaurar_marcacoes()
    {
        var (r, s, f) = Cenario();
        var dia = new DateTime(2026, 7, 7);
        var m = s.IncluirMarcacao(f, dia.AddHours(9).AddSeconds(30), 3);
        Assert.Throws<InvalidOperationException>(() => s.IncluirMarcacao(f, dia.AddHours(9), 3));
        Assert.Equal(TipoMarcacaoCodigo.Manual, r.Marcacoes.Obter(m.Id)!.Tipo);

        new ColetaService(r.Db).Gravar([("7984", dia.AddHours(18))], 1, new DateTime(2026, 8, 1));
        var coletada = r.Marcacoes.Periodo(dia, dia.AddDays(1), f.Codigo).Single(x => x.Tipo == 7);
        s.DesprezarOuExcluir(coletada);
        Assert.Equal(263, r.Marcacoes.Obter(coletada.Id)!.Tipo);
        s.DesprezarOuExcluir(m); // manual → exclui
        Assert.Null(r.Marcacoes.Obter(m.Id));
        s.RestaurarDesprezada(coletada);
        Assert.Equal(7, r.Marcacoes.Obter(coletada.Id)!.Tipo);
    }

    [Theory]
    [InlineData("2026-10-07", 25, "2026-08-26", "2026-09-25")]
    [InlineData("2026-09-26", 25, "2026-08-26", "2026-09-25")]
    [InlineData("2026-10-07", 31, "2026-10-01", "2026-10-31")]
    [InlineData("2026-10-03", 31, "2026-09-01", "2026-09-30")]
    public void Periodo_padrao_segue_o_fechamento(string hoje, int dia, string ini, string fim)
    {
        var (i, f) = ApuracaoService.PeriodoPadrao(DateTime.Parse(hoje), dia);
        Assert.Equal(DateTime.Parse(ini), i);
        Assert.Equal(DateTime.Parse(fim), f);
    }
}

internal static class TipoMarcacaoCodigo { public const int Manual = 0; }
