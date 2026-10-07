using MiniTime.Core.Models;

namespace MiniTime.Core.Apuracao;

public sealed class ContextoApuracao
{
    public required Funcionario Funcionario { get; init; }
    public Jornada? Jornada { get; init; }
    public required IReadOnlyDictionary<int, Horario> Horarios { get; init; }
    public IReadOnlyCollection<(int Dia, int Mes)> Feriados { get; init; } = [];
    public IReadOnlyList<Ferias> Ferias { get; init; } = [];
    public IReadOnlyDictionary<int, Justificativa> Justificativas { get; init; } = new Dictionary<int, Justificativa>();
    /// <summary>Dias posteriores a esta data ficam como "Futuro" (sem falta).</summary>
    public DateTime Hoje { get; init; } = DateTime.Today;
}

/// <summary>
/// Motor de apuração do ponto: classifica as batidas de cada dia, aplica tolerâncias e calcula
/// horas trabalhadas, atrasos, extras, faltas e abonos. É puro (sem acesso a banco) e determinístico.
///
/// Regras (reconstruídas do sistema legado — ver docs/spec/apuracao.md):
///  • batidas "coletadas" (Tipo 7) e inseridas manualmente (Tipo 0 com justificativa ≠ -1) valem;
///    linhas geradas pelo legado (Tipo 256, Tipo 0 automático) e desprezadas (Tipo 263) são ignoradas;
///  • entrada/saída dentro da tolerância do horário são "arredondadas" para o horário previsto;
///  • o retorno do intervalo respeita o tempo mínimo cadastrado (RefMinimo);
///  • hora extra só conta se o funcionário é autorizado e o excesso supera a tolerância de extra.
/// </summary>
public static class Apurador
{
    public static ResultadoApuracao Apurar(ContextoApuracao ctx, IEnumerable<Marcacao> marcacoes, DateTime ini, DateTime fim)
    {
        var f = ctx.Funcionario;
        var res = new ResultadoApuracao { Funcionario = f, Jornada = ctx.Jornada };
        var minhas = marcacoes.Where(m => m.Cracha == f.Codigo).ToList();

        var batidas = minhas.Where(EhBatida).OrderBy(m => m.DataHora).ToList();
        var statusDia = minhas.Where(m => m.Tipo == TipoMarcacao.Gerada && m.EntradaSaida == Posicao.StatusDia).ToList();
        var justSlot = minhas.Where(m => m.Tipo == TipoMarcacao.Gerada && m.EntradaSaida is >= 1 and <= 4 && m.Justificativa > 0).ToList();

        for (var d = ini.Date; d <= fim.Date; d = d.AddDays(1))
        {
            var dia = ApurarDia(ctx, d, batidas, statusDia, justSlot);
            res.Dias.Add(dia);
            Somar(res.Totais, dia);
        }
        return res;
    }

    /// <summary>Marcação que representa uma batida real do funcionário.</summary>
    public static bool EhBatida(Marcacao m) => m.Tipo switch
    {
        TipoMarcacao.Coletada => true,
        TipoMarcacao.Manual => m.Justificativa != TipoMarcacao.JustificativaAutomatica && m.EntradaSaida != Posicao.StatusDia,
        _ => false,
    };

    private static void Somar(TotaisApuracao t, DiaApurado d)
    {
        if (d.Status == StatusDia.Futuro) return;
        t.Esperado += d.Esperado;
        t.Trabalhado += d.Trabalhado;
        t.Atraso += d.Atraso;
        t.Extra += d.Extra;
        t.Abonado += d.Abonado;
        t.AdicionalNoturno += d.AdicionalNoturno;
        switch (d.Status)
        {
            case StatusDia.Falta: t.Faltas++; break;
            case StatusDia.MeiaFalta: t.MeiasFaltas++; break;
            case StatusDia.Dsr: t.DiasDsr++; break;
            case StatusDia.Pendencia: t.Pendencias++; break;
        }
    }

    // ---------------------------------------------------------------- dia

    private static DiaApurado ApurarDia(ContextoApuracao ctx, DateTime d, List<Marcacao> batidas, List<Marcacao> statusDia, List<Marcacao> justSlot)
    {
        var f = ctx.Funcionario;
        var dia = new DiaApurado { Data = d };

        var codHorario = ctx.Jornada?.HorarioDoDia(d.DayOfWeek) ?? 0;
        ctx.Horarios.TryGetValue(codHorario, out var h);
        if (h is { NaoTrabalha: not 0 }) h = null;
        dia.Horario = h;

        var noturno = (h is not null && (h.Noturno != 0)) || (h is null && f.HorarioNoturno);
        var (ini, fim) = Janela(f, d, h is null, noturno);
        var pts = batidas.Where(m => m.DataHora >= ini && m.DataHora < fim).OrderBy(m => m.DataHora).ToList();
        foreach (var m in pts)
            dia.Marcacoes.Add(new MarcacaoApurada { Origem = m, Minuto = (int)Math.Floor((m.DataHora - d).TotalMinutes) });

        var feriado = ctx.Feriados.Contains((d.Day, d.Month));
        var emFerias = ctx.Ferias.Any(x => x.Funcionario == f.Codigo && d >= x.Inicio.Date && d <= x.Fim.Date);
        var abono = statusDia.Where(m => m.DataHora.Date == d && m.Justificativa > 0).Select(m => m.Justificativa).FirstOrDefault();

        foreach (var s in justSlot.Where(m => m.DataHora.Date == d))
        {
            switch (s.EntradaSaida)
            {
                case Posicao.EntradaManha: dia.JustEntManha = s.Justificativa; break;
                case Posicao.SaidaIntervalo: dia.JustSaiManha = s.Justificativa; break;
                case Posicao.RetornoIntervalo: dia.JustEntTarde = s.Justificativa; break;
                case Posicao.Saida: dia.JustSaiTarde = s.Justificativa; break;
            }
        }

        // ---- dias sem obrigação de trabalhar
        if (emFerias || feriado || h is null)
        {
            dia.Status = emFerias ? StatusDia.Ferias
                : feriado ? StatusDia.Feriado
                : ctx.Jornada is { TrataDSR: not 0 } j && (int)d.DayOfWeek == j.DiaDSR - 1 ? StatusDia.Dsr
                : StatusDia.SemExpediente;
            TrabalhoEmDiaLivre(dia, f);
            return dia;
        }

        if (d > ctx.Hoje.Date) { dia.Status = StatusDia.Futuro; Classificar(dia, h, f, ctx, noturno); return dia; }

        if (dia.Marcacoes.Count == 0)
        {
            if (abono > 0)
            {
                dia.Status = StatusDia.Abonado;
                dia.Esperado = Esperado(h, noturno);
                dia.Abonado = dia.Esperado;
                dia.JustificativaDia = ctx.Justificativas.TryGetValue(abono, out var jj) ? jj.Descricao : null;
            }
            else
            {
                dia.Esperado = Esperado(h, noturno);
                dia.Status = ctx.Jornada is { NaoMarcarFalta: not 0 } ? StatusDia.SemMarcacao : StatusDia.Falta;
            }
            return dia;
        }

        Classificar(dia, h, f, ctx, noturno);
        if (abono > 0 && dia.Status is StatusDia.Falta or StatusDia.MeiaFalta or StatusDia.Pendencia)
        {
            dia.Abonado += Math.Max(0, dia.Esperado - dia.Trabalhado);
            dia.Status = StatusDia.Abonado;
            dia.JustificativaDia = ctx.Justificativas.TryGetValue(abono, out var jj) ? jj.Descricao : null;
        }
        return dia;
    }

    /// <summary>Janela de tempo que pertence ao dia de apuração.</summary>
    private static (DateTime Ini, DateTime Fim) Janela(Funcionario f, DateTime d, bool diaLivre, bool noturno)
    {
        if (noturno && f.HrMudancaData is { } m) return (d + m, d.AddDays(1) + m);
        if (noturno) return (d, d.AddDays(1));
        if (diaLivre && f.HrMudancaDataDiaLivre is { } l && l != TimeSpan.Zero)
            return f.SentidoMudancaDataDiaLivre == 0 ? (d.AddDays(-1) + l, d + l) : (d + l, d.AddDays(1) + l);
        if (!diaLivre && f.HrMudancaData is { } hd && f.HorarioNoturno && hd != TimeSpan.Zero) return (d + hd, d.AddDays(1) + hd);
        return (d, d.AddDays(1));
    }

    private static int Esperado(Horario h, bool noturno)
    {
        var (e1, _, _, s4, si, ri) = Tempos(h);
        var total = s4 - e1;
        if (TemIntervalo(h)) total -= ri - si;
        return Math.Max(0, total);
    }

    private static bool TemIntervalo(Horario h) => h.Intervalo != 0 && h.DeSS2 is not null && h.DeSS3 is not null;

    /// <summary>Tempos do horário em minutos; turno noturno vira o dia (valores &gt; 1440).</summary>
    private static (int E1, int Si, int Ri, int S4, int SiN, int RiN) Tempos(Horario h)
    {
        int Min(TimeSpan? t) => t is { } v ? (int)v.TotalMinutes : 0;
        var e1 = Min(h.DeSS1);
        var s4 = Min(h.DeSS4);
        var si = Min(h.DeSS2);
        var ri = Min(h.DeSS3);
        if (h.Noturno != 0 || s4 <= e1)
        {
            s4 += 1440;
            if (si != 0 && si < e1) si += 1440;
            if (ri != 0 && ri < e1) ri += 1440;
        }
        return (e1, si, ri, s4, si, ri);
    }

    // ------------------------------------------------------- dia livre

    private static void TrabalhoEmDiaLivre(DiaApurado dia, Funcionario f)
    {
        // Batidas em dia sem expediente: pares entrada/saída; contam como extra só se autorizado.
        var m = dia.Marcacoes;
        for (var i = 0; i < m.Count; i++)
        {
            m[i].Posicao = i % 2 == 0 ? Posicao.EntradaExtra : Posicao.SaidaExtra;
            m[i].Situacao = SituacaoMarcacao.Normal;
            m[i].Considerada = m[i].Minuto;
        }
        if (m.Count > 0) dia.EntExtra = m[0].Minuto;
        if (m.Count > 1) dia.SaiExtra = m[^1].Minuto;
        var trabalhado = 0;
        for (var i = 0; i + 1 < m.Count; i += 2) trabalhado += m[i + 1].Minuto - m[i].Minuto;
        dia.Trabalhado = trabalhado;
        if (f.AutHoraExtra != 0) dia.Extra = trabalhado;
    }

    // ------------------------------------------------------- classificação

    private static void Classificar(DiaApurado dia, Horario h, Funcionario f, ContextoApuracao ctx, bool noturno)
    {
        var (e1, _, _, s4, si, ri) = Tempos(h);
        var temInt = TemIntervalo(h);
        var durInt = temInt ? ri - si : 0;
        var minInt = Math.Max(h.RefMinimo, 0);
        var aut = f.AutHoraExtra != 0;
        var esperado = Esperado(h, noturno);
        dia.Esperado = esperado;

        var todas = dia.Marcacoes;
        if (todas.Count == 0) return;

        // 1) colapsa duplicatas nas pontas: tudo até e1 vira "entrada" (fica a primeira); tudo a partir de s4 vira "saída" (fica a última)
        var candidatas = new List<MarcacaoApurada>(todas);
        var antes = candidatas.Where(x => x.Minuto <= e1).ToList();
        var depois = candidatas.Where(x => x.Minuto >= s4 && !antes.Contains(x)).ToList();
        foreach (var x in antes.Skip(1)) { x.Posicao = Posicao.Ignorada; candidatas.Remove(x); }
        foreach (var x in depois.Take(Math.Max(0, depois.Count - 1))) { x.Posicao = Posicao.Ignorada; candidatas.Remove(x); }

        // 2) com mais de 4 batidas, mantém primeira, última e o par do intervalo
        if (candidatas.Count > 4)
        {
            var meio = candidatas.Skip(1).Take(candidatas.Count - 2).ToList();
            MarcacaoApurada? lo = temInt ? meio[0] : null;
            MarcacaoApurada? ret = null;
            if (lo is not null)
            {
                ret = meio.Skip(1).FirstOrDefault(x => x.Minuto - lo.Minuto >= minInt) ?? meio.Skip(1).FirstOrDefault();
            }
            var manter = new HashSet<MarcacaoApurada> { candidatas[0], candidatas[^1] };
            if (lo is not null) manter.Add(lo);
            if (ret is not null) manter.Add(ret);
            foreach (var x in candidatas.Where(x => !manter.Contains(x)).ToList()) { x.Posicao = Posicao.Ignorada; candidatas.Remove(x); }
        }

        // 3) atribui posições (1..4) minimizando a distância aos horários previstos
        var slotsPrevistos = temInt
            ? new[] { (Posicao.EntradaManha, e1), (Posicao.SaidaIntervalo, si), (Posicao.RetornoIntervalo, ri), (Posicao.Saida, s4) }
            : new[] { (Posicao.EntradaManha, e1), (Posicao.Saida, s4) };
        var atrib = MelhorAtribuicao(candidatas, slotsPrevistos);
        foreach (var x in candidatas) x.Posicao = Posicao.Ignorada;
        foreach (var (m, pos) in atrib) m.Posicao = pos;

        MarcacaoApurada? P(int pos) => todas.FirstOrDefault(x => x.Posicao == pos);
        var pEnt = P(Posicao.EntradaManha);
        var pLo = P(Posicao.SaidaIntervalo);
        var pRet = P(Posicao.RetornoIntervalo);
        var pSai = P(Posicao.Saida);

        var tolEnt = h.TolManha;
        var tolRet = h.TolTarde;
        var tolSai = h.TolSaida;
        var atraso = 0;
        var extra = 0;

        // ---- entrada
        int? entC = null;
        if (pEnt is not null)
        {
            var dif = pEnt.Minuto - e1;
            if (Math.Abs(dif) <= tolEnt) { entC = e1; pEnt.Situacao = SituacaoMarcacao.Normal; }
            else if (dif > 0)
            {
                entC = pEnt.Minuto; atraso += dif;
                pEnt.Situacao = SituacaoMarcacao.Divergente; pEnt.Divergencia = TipoDivergencia.Atraso;
            }
            else // chegou antes além da tolerância
            {
                entC = e1; pEnt.Situacao = SituacaoMarcacao.Normal;
                if (aut && -dif > h.TolExtEnt) { extra += -dif; pEnt.Posicao = Posicao.EntradaExtra; dia.EntExtra = pEnt.Minuto; }
            }
            pEnt.Considerada = entC;
        }

        // ---- intervalo
        int? loC = null, retC = null;
        if (pLo is not null) { loC = pLo.Minuto; pLo.Situacao = SituacaoMarcacao.Normal; pLo.Considerada = loC; }
        if (pRet is not null && pLo is not null)
        {
            var real = pRet.Minuto - pLo.Minuto;
            var previsto = Math.Max(durInt, minInt);
            int considerado;
            if (real < minInt) considerado = minInt;                  // voltou antes do mínimo: vale o mínimo
            else if (real <= previsto + tolRet) considerado = Math.Max(real >= previsto ? previsto : real, minInt);
            else { considerado = real; atraso += real - previsto; pRet.Situacao = SituacaoMarcacao.Divergente; pRet.Divergencia = TipoDivergencia.Atraso; }
            if (pRet.Situacao == SituacaoMarcacao.NaoTratada) pRet.Situacao = SituacaoMarcacao.Normal;
            retC = pLo.Minuto + considerado;
            pRet.Considerada = retC;
        }
        else if (pRet is not null) { retC = pRet.Minuto; pRet.Situacao = SituacaoMarcacao.Normal; pRet.Considerada = retC; }

        // ---- saída
        int? saiC = null;
        if (pSai is not null)
        {
            var dif = pSai.Minuto - s4;
            if (Math.Abs(dif) <= tolSai) { saiC = s4; pSai.Situacao = SituacaoMarcacao.Normal; }
            else if (dif < 0)
            {
                saiC = pSai.Minuto; atraso += -dif;
                pSai.Situacao = SituacaoMarcacao.Divergente; pSai.Divergencia = TipoDivergencia.SaidaAntecipada;
            }
            else
            {
                saiC = s4; pSai.Situacao = SituacaoMarcacao.Normal;
                if (aut && dif > h.TolExtSai) { extra += dif; pSai.Posicao = Posicao.SaidaExtra; dia.SaiExtra = pSai.Minuto; }
            }
            pSai.Considerada = saiC;
        }

        // ---- colunas do espelho (hora real da batida)
        dia.EntManha = pEnt?.Minuto ?? (dia.EntExtra is not null ? e1 : null);
        dia.SaiManha = pLo?.Minuto;
        dia.EntTarde = pRet?.Minuto;
        dia.SaiTarde = pSai?.Minuto ?? (dia.SaiExtra is not null ? s4 : null);

        // ---- status do dia
        var temE = entC is not null; var temS = saiC is not null;
        if (temE && temS)
        {
            var trab = saiC!.Value - entC!.Value;
            if (temInt)
            {
                if (loC is not null && retC is not null) trab -= retC.Value - loC.Value;
                else if (loC is null && retC is null) trab -= durInt; // não bateu o intervalo: desconta o previsto
                else dia.Status = StatusDia.Pendencia;
            }
            dia.Trabalhado = Math.Max(0, trab);
            if (dia.Status != StatusDia.Pendencia) dia.Status = StatusDia.Normal;
        }
        else
        {
            dia.Status = MeiaOuPendencia(dia, todas, h, temInt, si, ri);
            if (dia.Status == StatusDia.MeiaFalta) dia.Trabalhado = ParcialTrabalhado(todas, entC, loC, retC, saiC);
        }

        // ---- justificativas por coluna abonam o atraso correspondente
        var abonoSlots = 0;
        if (dia.JustEntManha > 0 && pEnt is { Divergencia: TipoDivergencia.Atraso }) { abonoSlots += Math.Max(0, pEnt.Minuto - e1); pEnt.Situacao = SituacaoMarcacao.Justificada; }
        if (dia.JustEntTarde > 0 && pRet is { Divergencia: TipoDivergencia.Atraso }) { abonoSlots += AtrasoRetorno(pLo, pRet, durInt, minInt); pRet.Situacao = SituacaoMarcacao.Justificada; }
        if ((dia.JustSaiTarde > 0 || dia.JustSaiManha > 0) && pSai is { Divergencia: TipoDivergencia.SaidaAntecipada }) { abonoSlots += Math.Max(0, s4 - pSai.Minuto); pSai.Situacao = SituacaoMarcacao.Justificada; }
        abonoSlots = Math.Min(abonoSlots, atraso);
        dia.Abonado += abonoSlots;
        dia.Atraso = atraso - abonoSlots;
        dia.Divergencias = dia.Atraso;
        dia.Extra = extra;

        foreach (var m in todas.Where(x => x.Posicao == Posicao.Ignorada)) m.Situacao = SituacaoMarcacao.Normal;

        if (noturno || h.Noturno != 0) dia.AdicionalNoturno = NoturnoMinutos(entC, loC, retC, saiC, temInt);
    }

    private static int AtrasoRetorno(MarcacaoApurada? lo, MarcacaoApurada ret, int durInt, int minInt)
        => lo is null ? 0 : Math.Max(0, ret.Minuto - lo.Minuto - Math.Max(durInt, minInt));

    private static StatusDia MeiaOuPendencia(DiaApurado dia, List<MarcacaoApurada> todas, Horario h, bool temInt, int si, int ri)
    {
        var uteis = todas.Where(x => x.Posicao > 0).ToList();
        if (temInt && uteis.Count == 2)
        {
            var (a, b) = (uteis[0].Minuto, uteis[1].Minuto);
            if (b <= (si + ri) / 2 + 30 || a >= (si + ri) / 2 - 30) return StatusDia.MeiaFalta;
        }
        return StatusDia.Pendencia;
    }

    private static int ParcialTrabalhado(List<MarcacaoApurada> todas, int? ent, int? lo, int? ret, int? sai)
    {
        var u = todas.Where(x => x.Posicao > 0 && x.Considerada is not null).OrderBy(x => x.Minuto).ToList();
        return u.Count == 2 ? Math.Max(0, u[1].Considerada!.Value - u[0].Considerada!.Value) : 0;
    }

    // ---------------------------------------------------- atribuição de slots

    /// <summary>Atribui as batidas (em ordem) a um subconjunto ordenado dos slots, minimizando a soma das distâncias.</summary>
    private static List<(MarcacaoApurada M, int Pos)> MelhorAtribuicao(List<MarcacaoApurada> batidas, (int Pos, int Min)[] slots)
    {
        var n = batidas.Count;
        var k = slots.Length;
        var resultado = new List<(MarcacaoApurada, int)>();
        if (n == 0) return resultado;
        if (n > k)
        {
            // sobrou batida: as primeiras ocupam os slots em ordem, o resto fica ignorado (não deve ocorrer após a redução)
            n = k;
            batidas = batidas.Take(k).ToList();
        }
        var melhor = int.MaxValue;
        int[]? melhorEsc = null;
        // enumera subconjuntos de tamanho n de k slots (k ≤ 4)
        for (var mask = 0; mask < (1 << k); mask++)
        {
            if (BitCount(mask) != n) continue;
            var esc = Enumerable.Range(0, k).Where(i => (mask & (1 << i)) != 0).ToArray();
            var custo = 0;
            for (var i = 0; i < n; i++) custo += Math.Abs(batidas[i].Minuto - slots[esc[i]].Min);
            if (custo < melhor) { melhor = custo; melhorEsc = esc; }
        }
        for (var i = 0; i < n; i++) resultado.Add((batidas[i], slots[melhorEsc![i]].Pos));
        return resultado;
    }

    private static int BitCount(int v) { var c = 0; while (v != 0) { c += v & 1; v >>= 1; } return c; }

    // -------------------------------------------------------- adicional noturno

    /// <summary>Minutos adicionais pela hora noturna reduzida (22:00–05:00; hora de 52min30s).</summary>
    private static int NoturnoMinutos(int? ent, int? lo, int? ret, int? sai, bool temInt)
    {
        if (ent is null || sai is null) return 0;
        var trechos = new List<(int A, int B)>();
        if (temInt && lo is not null && ret is not null) { trechos.Add((ent.Value, lo.Value)); trechos.Add((ret.Value, sai.Value)); }
        else trechos.Add((ent.Value, sai.Value));
        var noturnos = 0;
        foreach (var (a, b) in trechos)
            for (var k = 0; k <= 2; k++)
            {
                // faixa noturna: 22:00 do dia anterior até 05:00 do dia k
                var ini = Math.Max(a, k * 1440 - 120);
                var fim = Math.Min(b, k * 1440 + 300);
                if (fim > ini) noturnos += fim - ini;
            }
        return (int)Math.Round(noturnos * (60.0 / 52.5 - 1));
    }
}
