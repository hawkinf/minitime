using MiniTime.Core.Apuracao;
using MiniTime.Core.Models;
using MiniTime.Core.Util;

namespace MiniTime.Tests;

public class ApuracaoTests
{
    private const string Cartao = "0000000000007984";

    private static Horario H1() => new()
    {
        Codigo = 1, Descricao = "Almoço 11:30",
        DeSS1 = new(9, 0, 0), DeSS2 = new(11, 30, 0), DeSS3 = new(12, 30, 0), DeSS4 = new(18, 0, 0),
        TolManha = 5, TolTarde = 5, TolSaida = 5, RefObrig = 1, RefMinimo = 60, Intervalo = 1,
    };

    private static Horario HSabado() => new() { Codigo = 6, Descricao = "Sábado", DeSS1 = new(9, 0, 0), DeSS4 = new(13, 0, 0), TolManha = 5, TolTarde = 5, TolSaida = 5 };

    private static ContextoApuracao Ctx(Funcionario? f = null, Jornada? j = null, DateTime? hoje = null,
        IEnumerable<(int, int)>? feriados = null, IEnumerable<Ferias>? ferias = null, IEnumerable<Horario>? horarios = null)
        => new()
        {
            Funcionario = f ?? new Funcionario { Codigo = Cartao, Nome = "Teste", Horario = 1, AutHoraExtra = 0 },
            Jornada = j ?? new Jornada { Codigo = 1, Segunda = 1, Terca = 1, Quarta = 1, Quinta = 1, Sexta = 1, Sabado = 6 },
            Horarios = (horarios ?? [H1(), HSabado()]).ToDictionary(h => h.Codigo),
            Feriados = feriados?.ToList() ?? [],
            Ferias = ferias?.ToList() ?? [],
            Hoje = hoje ?? new DateTime(2026, 8, 31),
        };

    private static List<Marcacao> Batidas(string dia, params string[] horas)
        => horas.Select(h => new Marcacao { Cracha = Cartao, Tipo = TipoMarcacao.Coletada, DataHora = DateTime.Parse($"{dia} {h}") }).ToList();

    private static DiaApurado Dia(ContextoApuracao ctx, string dia, params string[] horas)
        => Apurador.Apurar(ctx, Batidas(dia, horas), DateTime.Parse(dia), DateTime.Parse(dia)).Dias.Single();

    [Fact]
    public void Dia_normal_dentro_da_tolerancia_nao_tem_atraso()
    {
        // 2026-07-01 (quarta) — caso real: 09:04 / 11:33 / 12:36 / 17:59
        var d = Dia(Ctx(), "2026-07-01", "09:04", "11:33", "12:36", "17:59");
        Assert.Equal(StatusDia.Normal, d.Status);
        Assert.Equal(480, d.Esperado);
        Assert.Equal(480, d.Trabalhado);
        Assert.Equal(0, d.Atraso);
        Assert.Equal(new[] { 1, 2, 3, 4 }, d.Marcacoes.Select(m => m.Posicao));
        Assert.All(d.Marcacoes, m => Assert.NotEqual(SituacaoMarcacao.Divergente, m.Situacao));
    }

    [Fact]
    public void Atraso_na_entrada_e_saida_antecipada_viram_divergencia()
    {
        // 2026-07-03: 09:25 / 11:30 / 12:29 / 17:51
        var d = Dia(Ctx(), "2026-07-03", "09:25", "11:30", "12:29", "17:51");
        Assert.Equal(25 + 9, d.Atraso);
        Assert.Equal(480 - 34, d.Trabalhado);
        Assert.Equal(TipoDivergencia.Atraso, d.Marcacoes[0].Divergencia);
        Assert.Equal(SituacaoMarcacao.Divergente, d.Marcacoes[0].Situacao);
        Assert.Equal(TipoDivergencia.SaidaAntecipada, d.Marcacoes[3].Divergencia);
        // retorno em 59 min vale o mínimo de 60 (12:30)
        Assert.Equal(12 * 60 + 30, d.Marcacoes[2].Considerada);
    }

    [Fact]
    public void Dia_sem_batidas_de_intervalo_desconta_o_intervalo_previsto()
    {
        var d = Dia(Ctx(), "2026-07-07", "09:02", "17:54");
        Assert.Equal(StatusDia.Normal, d.Status);
        Assert.Equal(474, d.Trabalhado); // 9:00→17:54 = 534 − 60
        Assert.Equal(6, d.Atraso);
        Assert.Equal(new[] { 1, 4 }, d.Marcacoes.Select(m => m.Posicao));
    }

    [Fact]
    public void Batidas_duplicadas_antes_da_entrada_sao_ignoradas()
    {
        var d = Dia(Ctx(), "2026-07-08", "08:46", "08:47", "11:29", "12:31", "18:07", "18:09");
        Assert.Equal(480, d.Trabalhado);
        Assert.Equal(1, d.Marcacoes.Count(m => m.Posicao == Posicao.EntradaManha));
        Assert.Equal(1, d.Marcacoes.Count(m => m.Posicao == Posicao.Saida));
        Assert.True(d.Marcacoes.Count(m => m.Posicao == Posicao.Ignorada) >= 2);
    }

    [Fact]
    public void Hora_extra_so_conta_se_autorizada()
    {
        var sem = Ctx(new Funcionario { Codigo = Cartao, Horario = 1, AutHoraExtra = 0 });
        var com = Ctx(new Funcionario { Codigo = Cartao, Horario = 1, AutHoraExtra = 1 });
        var h = new[] { "09:00", "11:30", "12:30", "19:00" };
        Assert.Equal(0, Dia(sem, "2026-07-01", h).Extra);
        Assert.Equal(60, Dia(com, "2026-07-01", h).Extra);
        Assert.Equal(480, Dia(sem, "2026-07-01", h).Trabalhado);
    }

    [Fact]
    public void Falta_feriado_ferias_e_sem_expediente()
    {
        var ctx = Ctx(feriados: [(7, 9)],
            ferias: [new Ferias { Funcionario = Cartao, Inicio = new DateTime(2026, 7, 13), Fim = new DateTime(2026, 7, 17) }]);
        Assert.Equal(StatusDia.Falta, Dia(ctx, "2026-07-02").Status);          // quinta sem batidas
        Assert.Equal(480, Dia(ctx, "2026-07-02").Esperado);
        Assert.Equal(StatusDia.Feriado, Dia(ctx, "2026-09-07").Status);
        Assert.Equal(StatusDia.Ferias, Dia(ctx, "2026-07-14").Status);
        Assert.Equal(StatusDia.SemExpediente, Dia(ctx, "2026-07-05").Status);  // domingo
        Assert.Equal(StatusDia.Futuro, Dia(ctx, "2026-09-10").Status);
    }

    [Fact]
    public void Jornada_que_nao_marca_falta_e_dia_sem_marcacao()
    {
        var j = new Jornada { Codigo = 1, Segunda = 1, Terca = 1, Quarta = 1, Quinta = 1, Sexta = 1, NaoMarcarFalta = 1 };
        Assert.Equal(StatusDia.SemMarcacao, Dia(Ctx(j: j), "2026-07-02").Status);
    }

    [Fact]
    public void Falta_com_justificativa_vira_abono()
    {
        var ctx = Ctx();
        var marc = new List<Marcacao>
        {
            new() { Cracha = Cartao, Tipo = TipoMarcacao.Gerada, EntradaSaida = Posicao.StatusDia, Situacao = 3, Divergencia = 3,
                    DataHora = new DateTime(2026, 7, 2, 23, 0, 0), Justificativa = 1 },
        };
        var ctx2 = new ContextoApuracao
        {
            Funcionario = ctx.Funcionario, Jornada = ctx.Jornada, Horarios = ctx.Horarios, Hoje = ctx.Hoje,
            Justificativas = new Dictionary<int, Justificativa> { [1] = new() { Codigo = 1, Descricao = "Atestado" } },
        };
        var d = Apurador.Apurar(ctx2, marc, new DateTime(2026, 7, 2), new DateTime(2026, 7, 2)).Dias.Single();
        Assert.Equal(StatusDia.Abonado, d.Status);
        Assert.Equal(480, d.Abonado);
        Assert.Equal("Atestado", d.JustificativaDia);
    }

    [Fact]
    public void Linhas_geradas_pelo_legado_e_desprezadas_nao_contam_como_batida()
    {
        var marc = Batidas("2026-07-01", "09:00", "11:30", "12:30", "18:00");
        marc.Add(new Marcacao { Cracha = Cartao, Tipo = TipoMarcacao.Manual, Justificativa = -1, EntradaSaida = 1, DataHora = DateTime.Parse("2026-07-01 09:00") });
        marc.Add(new Marcacao { Cracha = Cartao, Tipo = TipoMarcacao.Desprezada, EntradaSaida = -1, DataHora = DateTime.Parse("2026-07-01 12:00") });
        marc.Add(new Marcacao { Cracha = Cartao, Tipo = TipoMarcacao.Gerada, EntradaSaida = Posicao.StatusDia, Situacao = 4, Divergencia = 3, DataHora = DateTime.Parse("2026-07-01 23:00") });
        var d = Apurador.Apurar(Ctx(), marc, new DateTime(2026, 7, 1), new DateTime(2026, 7, 1)).Dias.Single();
        Assert.Equal(4, d.Marcacoes.Count);
        Assert.Equal(StatusDia.Normal, d.Status);
    }

    [Fact]
    public void Batida_manual_com_justificativa_vale_como_batida()
    {
        var marc = Batidas("2026-07-01", "09:00", "11:30", "18:00");
        marc.Add(new Marcacao { Cracha = Cartao, Tipo = TipoMarcacao.Manual, Justificativa = 3, DataHora = DateTime.Parse("2026-07-01 12:30") });
        var d = Apurador.Apurar(Ctx(), marc, new DateTime(2026, 7, 1), new DateTime(2026, 7, 1)).Dias.Single();
        Assert.Equal(4, d.Marcacoes.Count(m => m.Posicao > 0));
        Assert.Equal(480, d.Trabalhado);
    }

    [Fact]
    public void Turno_noturno_atravessa_a_meia_noite_e_calcula_adicional()
    {
        var noturno = new Horario { Codigo = 20, Descricao = "Noite", DeSS1 = new(22, 0, 0), DeSS4 = new(6, 0, 0), Noturno = 1, TolManha = 5, TolSaida = 5 };
        var f = new Funcionario { Codigo = Cartao, Horario = 9, HorarioNoturno = true, HrMudancaData = new TimeSpan(12, 0, 0) };
        var j = new Jornada { Codigo = 9, Segunda = 20 };
        var ctx = Ctx(f, j, horarios: [noturno]);
        // a saída às 06:03 já é do dia 07/07, por isso as batidas levam data própria
        var marc = new List<Marcacao>
        {
            new() { Cracha = Cartao, Tipo = TipoMarcacao.Coletada, DataHora = DateTime.Parse("2026-07-06 21:58") },
            new() { Cracha = Cartao, Tipo = TipoMarcacao.Coletada, DataHora = DateTime.Parse("2026-07-07 06:03") },
        };
        var r = Apurador.Apurar(ctx, marc, new DateTime(2026, 7, 6), new DateTime(2026, 7, 6)).Dias.Single();
        Assert.Equal(480, r.Esperado);
        Assert.Equal(480, r.Trabalhado);
        Assert.Equal(2, r.Marcacoes.Count);
        Assert.Equal(StatusDia.Normal, r.Status);
        // 22:00–05:00 = 420 min noturnos → +60 min de hora reduzida
        Assert.Equal(60, r.AdicionalNoturno);
    }

    [Fact]
    public void Totais_somam_o_periodo()
    {
        var ctx = Ctx();
        var marc = Batidas("2026-07-01", "09:00", "11:30", "12:30", "18:00");
        marc.AddRange(Batidas("2026-07-03", "09:25", "11:30", "12:29", "17:51"));
        var r = Apurador.Apurar(ctx, marc, new DateTime(2026, 7, 1), new DateTime(2026, 7, 7));
        Assert.Equal(34, r.Totais.Atraso);
        Assert.Equal(480 * 2 - 34, r.Totais.Trabalhado);
        Assert.Equal(4, r.Totais.Faltas); // qui 02, sáb 04, seg 06 e ter 07 sem batidas (dom 05 é folga)
        Assert.Equal(2, r.Dias.Count(d => d.Status == StatusDia.Normal));
    }

    [Theory]
    [InlineData("07:20", true, 7, 20)]
    [InlineData("0720", true, 7, 20)]
    [InlineData("7:20", true, 7, 20)]
    [InlineData("25:00", false, 0, 0)]
    [InlineData("abc", false, 0, 0)]
    public void Hora_le_formatos_do_legado(string txt, bool ok, int h, int m)
    {
        Assert.Equal(ok, Hora.TentarLer(txt, out var t));
        if (ok) Assert.Equal(new TimeSpan(h, m, 0), t);
    }
}

public class ExportacaoTests
{
    [Fact]
    public void Linha_segue_o_exemplo_do_manual()
    {
        // manual: 05362002031018070000100 → cartão 0536, 20/02/03, 10:18, tipo 07, "00", relógio 001
        var m = new Marcacao { Cracha = "0000000000000536", DataHora = new DateTime(2003, 2, 20, 10, 18, 0), Tipo = 7, Terminal = 1 };
        Assert.Equal("05362002031018070000100", MiniTime.Core.Exportacao.ExportadorMarcacoes.Linha(m, new(4, 2)));
        Assert.Equal("053620022003101807" + "0000100", MiniTime.Core.Exportacao.ExportadorMarcacoes.Linha(m, new(4, 4)));
        Assert.StartsWith("00536", MiniTime.Core.Exportacao.ExportadorMarcacoes.Linha(m, new(5, 2)));
    }

    [Fact]
    public void So_exporta_coletadas_e_inseridas()
    {
        var ms = new[]
        {
            new Marcacao { Cracha = "0000000000007984", DataHora = new DateTime(2026, 7, 1, 9, 0, 0), Tipo = 7 },
            new Marcacao { Cracha = "0000000000007984", DataHora = new DateTime(2026, 7, 1, 12, 0, 0), Tipo = 0, Justificativa = 3 },
            new Marcacao { Cracha = "0000000000007984", DataHora = new DateTime(2026, 7, 1, 12, 5, 0), Tipo = 0, Justificativa = -1 },
            new Marcacao { Cracha = "0000000000007984", DataHora = new DateTime(2026, 7, 1, 18, 0, 0), Tipo = 263 },
            new Marcacao { Cracha = "0000000000007984", DataHora = new DateTime(2026, 7, 1, 23, 0, 0), Tipo = 256, EntradaSaida = 8 },
        };
        var txt = MiniTime.Core.Exportacao.ExportadorMarcacoes.Gerar(ms, new(4, 2));
        var linhas = txt.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, linhas.Length);
        Assert.Equal("798401072609000700000" + "00", linhas[0][..23].Length == 23 ? linhas[0].Substring(0, 21) + "00" : "");
        Assert.Contains("0000", linhas[1]);
        Assert.Equal("00", linhas[1].Substring(14, 2)); // tipo inserido
    }
}
