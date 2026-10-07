using System.Globalization;
using System.Windows;
using MiniTime.Core.Apuracao;
using MiniTime.Core.Models;
using MiniTime.Core.Util;

namespace MiniTime.App.Services;

/// <summary>Formatação do espelho de ponto (tela e impressão).</summary>
public static class Espelho
{
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    public static string Hora(int? minuto) => minuto is { } m ? $"{m % 1440 / 60:00}:{m % 60:00}" : "";

    /// <summary>Minutos → HH:MM; vazio para zero (exceto quando mostrarZero).</summary>
    public static string Duracao(int minutos, bool mostrarZero = false) => minutos == 0 && !mostrarZero ? "" : Core.Util.Hora.Minutos(minutos);

    public static string Observacao(DiaApurado d, IReadOnlyDictionary<(int, int), string> feriados)
    {
        var txt = d.Status switch
        {
            StatusDia.Falta => "FALTA",
            StatusDia.MeiaFalta => "1/2 FALTA",
            StatusDia.Pendencia => "PENDÊNCIA",
            StatusDia.Feriado => "FERIADO" + (feriados.TryGetValue((d.Data.Day, d.Data.Month), out var n) ? $" - {n}" : ""),
            StatusDia.Ferias => "FÉRIAS",
            StatusDia.Dsr => "DSR",
            StatusDia.Abonado => "ABONADO" + (d.JustificativaDia is { } j ? $" - {j}" : ""),
            StatusDia.SemMarcacao => "SEM MARCAÇÃO",
            _ => "",
        };
        if (d.Status == StatusDia.Normal || d.Status == StatusDia.Feriado)
        {
            var extras = new List<string>();
            if (d.Marcacoes.Any(m => m.Divergencia == TipoDivergencia.Atraso)) extras.Add("Atraso");
            if (d.Marcacoes.Any(m => m.Divergencia == TipoDivergencia.SaidaAntecipada)) extras.Add("Saída antecipada");
            if (d.Marcacoes.Any(m => m.Posicao == Posicao.Ignorada)) extras.Add("marcações desconsideradas");
            if (extras.Count > 0) txt = (txt.Length > 0 ? txt + " " : "") + string.Join(", ", extras);
        }
        return txt;
    }

    public static string Origem(Marcacao m) => m.Tipo switch
    {
        TipoMarcacao.Coletada => "Coletada",
        TipoMarcacao.Manual => m.Justificativa == TipoMarcacao.JustificativaAutomatica ? "Automática (legado)" : "Inserida",
        TipoMarcacao.Desprezada => "Desprezada",
        TipoMarcacao.Gerada => m.EntradaSaida == Posicao.StatusDia ? "Falta/abono (legado)" : "Divergência (legado)",
        _ => $"Tipo {m.Tipo}",
    };

    public static string NomePosicao(int p) => p switch
    {
        Posicao.EntradaManha => "Entrada",
        Posicao.SaidaIntervalo => "Saída p/ intervalo",
        Posicao.RetornoIntervalo => "Retorno do intervalo",
        Posicao.Saida => "Saída",
        Posicao.EntradaExtra => "Entrada extra",
        Posicao.SaidaExtra => "Saída extra",
        Posicao.Ignorada => "Desconsiderada",
        _ => "",
    };

    public static string NomeSituacao(int sit, int div) => sit switch
    {
        SituacaoMarcacao.Divergente => div == TipoDivergencia.Atraso ? "Atraso" : div == TipoDivergencia.SaidaAntecipada ? "Saída antecipada" : "Divergência",
        SituacaoMarcacao.Justificada => "Justificada",
        SituacaoMarcacao.Normal => "Normal",
        _ => "",
    };

    private static string Jornada(ResultadoApuracao r, IReadOnlyDictionary<int, Horario> horarios)
    {
        if (r.Jornada is null) return "Sem jornada";
        var h = r.Jornada.HorarioDoDia(DayOfWeek.Monday);
        return horarios.TryGetValue(h, out var hor) ? $"{r.Jornada.Descricao} ({hor.Descricao})" : r.Jornada.Descricao;
    }

    /// <summary>Uma página (funcionário) do espelho de ponto.</summary>
    public static RelatorioTabela Montar(ResultadoApuracao r, DateTime ini, DateTime fim,
        IReadOnlyDictionary<(int, int), string> feriados, IReadOnlyDictionary<int, Horario> horarios)
    {
        var f = r.Funcionario;
        var rel = new RelatorioTabela
        {
            Titulo = "ESPELHO DE PONTO",
            Subtitulo = $"Período: {ini:dd/MM/yyyy} a {fim:dd/MM/yyyy}   |   Cartão: {f.CodigoCurto}   |   {f.Nome}" +
                        (string.IsNullOrWhiteSpace(f.Cargo) ? "" : $"   |   Função: {f.Cargo}") + $"   |   Jornada: {Jornada(r, horarios)}",
            Colunas =
            [
                new("Data", 7), new("Dia", 5), new("Ent.", 6, TextAlignment.Center), new("Sai.", 6, TextAlignment.Center),
                new("Ent.", 6, TextAlignment.Center), new("Sai.", 6, TextAlignment.Center), new("Ext.E", 6, TextAlignment.Center),
                new("Ext.S", 6, TextAlignment.Center), new("Trab.", 7, TextAlignment.Center), new("Atraso", 6, TextAlignment.Center),
                new("Extra", 6, TextAlignment.Center), new("Abon.", 6, TextAlignment.Center), new("Observação", 22),
            ],
        };
        foreach (var d in r.Dias)
        {
            rel.Linhas.Add(
            [
                d.Data.ToString("dd/MM"), Br.DateTimeFormat.GetAbbreviatedDayName(d.Data.DayOfWeek),
                Hora(d.EntManha), Hora(d.SaiManha), Hora(d.EntTarde), Hora(d.SaiTarde), Hora(d.EntExtra), Hora(d.SaiExtra),
                Duracao(d.Trabalhado), Duracao(d.Atraso), Duracao(d.Extra), Duracao(d.Abonado), Observacao(d, feriados),
            ]);
        }
        var t = r.Totais;
        rel.Rodape.Add($"Esperado: {Duracao(t.Esperado, true)}   Trabalhado: {Duracao(t.Trabalhado, true)}   Atraso: {Duracao(t.Atraso, true)}   " +
                       $"Extra: {Duracao(t.Extra, true)}   Abonado: {Duracao(t.Abonado, true)}   Saldo: {(t.Saldo < 0 ? "-" : "")}{Core.Util.Hora.Minutos(Math.Abs(t.Saldo))}");
        rel.Rodape.Add($"Faltas: {t.Faltas}   1/2 faltas: {t.MeiasFaltas}   Pendências: {t.Pendencias}   DSR: {t.DiasDsr}" +
                       (t.AdicionalNoturno > 0 ? $"   Adicional noturno: {Core.Util.Hora.Minutos(t.AdicionalNoturno)}" : ""));
        rel.Rodape.Add("");
        rel.Rodape.Add("Assinatura do funcionário: ______________________________________     Data: ____/____/________");
        return rel;
    }
}
