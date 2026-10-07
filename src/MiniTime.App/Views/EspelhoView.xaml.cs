using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MiniTime.App.Services;
using MiniTime.Core.Apuracao;
using MiniTime.Core.Models;
using MiniTime.Core.Util;
using MiniTime.Data;

namespace MiniTime.App.Views;

public partial class EspelhoView : UserControl
{
    public sealed record FuncionarioItem(Funcionario F)
    {
        public string Rotulo => $"{F.CodigoCurto} - {F.Nome}";
    }

    public sealed class LinhaEspelho
    {
        public required DiaApurado Dia { get; init; }
        public string Data => Dia.Data.ToString("dd/MM", CultureInfo.InvariantCulture);
        public string Semana => CultureInfo.GetCultureInfo("pt-BR").DateTimeFormat.GetAbbreviatedDayName(Dia.Data.DayOfWeek);
        public string EntManha => Espelho.Hora(Dia.EntManha);
        public string SaiManha => Espelho.Hora(Dia.SaiManha);
        public string EntTarde => Espelho.Hora(Dia.EntTarde);
        public string SaiTarde => Espelho.Hora(Dia.SaiTarde);
        public string EntExtra => Espelho.Hora(Dia.EntExtra);
        public string SaiExtra => Espelho.Hora(Dia.SaiExtra);
        public string Trabalhado => Espelho.Duracao(Dia.Trabalhado);
        public string Atraso => Espelho.Duracao(Dia.Atraso);
        public string Extra => Espelho.Duracao(Dia.Extra);
        public string Abonado => Espelho.Duracao(Dia.Abonado);
        public string Obs { get; init; } = "";
        public Brush? Fundo { get; init; }
    }

    public sealed record LinhaMarcacao(Marcacao M, string Hora, string Origem, string Posicao, string Situacao, string Justificativa);

    private readonly ApuracaoService _servico = new(Sessao.Repos);
    private readonly Dictionary<int, Justificativa> _just = Sessao.Repos.Justificativas.Todos().ToDictionary(j => j.Codigo);
    private ResultadoApuracao? _resultado;
    private bool _carregando = true;

    public EspelhoView()
    {
        InitializeComponent();
        CmbFuncionario.ItemsSource = Sessao.Repos.Funcionarios.Todos("Nome").Select(f => new FuncionarioItem(f)).ToList();
        var (ini, fim) = ApuracaoService.PeriodoPadrao(DateTime.Today, Sessao.Repos.Parametros.Obter().DiaFechamento);
        // MINITIME_PERIODO=aaaa-mm-dd,aaaa-mm-dd fixa o período inicial (testes/suporte)
        if (Environment.GetEnvironmentVariable("MINITIME_PERIODO")?.Split(',') is [var a, var b]
            && DateTime.TryParse(a, CultureInfo.InvariantCulture, out var pa) && DateTime.TryParse(b, CultureInfo.InvariantCulture, out var pb))
            (ini, fim) = (pa, pb);
        DtIni.SelectedDate = ini;
        DtFim.SelectedDate = fim;
        _carregando = false;
        // abre no primeiro funcionário que tem marcações no período
        var comMarcacao = Sessao.Repos.Funcionarios.Todos("Nome")
            .FirstOrDefault(f => Sessao.Repos.Marcacoes.Contar("Cracha=@c AND DataHora>=@a AND DataHora<@b", ("c", f.Codigo), ("a", ini), ("b", fim.AddDays(1))) > 0);
        var itens = (List<FuncionarioItem>)CmbFuncionario.ItemsSource;
        CmbFuncionario.SelectedItem = itens.FirstOrDefault(i => i.F.Codigo == comMarcacao?.Codigo) ?? itens.FirstOrDefault();
    }

    private Funcionario? Selecionado => (CmbFuncionario.SelectedItem as FuncionarioItem)?.F;

    private void Filtro_Changed(object sender, EventArgs e)
    {
        if (!_carregando) Recalcular();
    }

    private void Recalcular(DateTime? manterDia = null)
    {
        if (Selecionado is not { } f || DtIni.SelectedDate is not { } ini || DtFim.SelectedDate is not { } fim) return;
        if (fim < ini) { TxtTotais.Text = "Período inválido."; return; }
        if ((fim - ini).TotalDays > 400) { TxtTotais.Text = "Período muito longo (máximo de 400 dias)."; return; }
        try
        {
            _resultado = _servico.Apurar(f, ini, fim);
        }
        catch (Exception ex)
        {
            Mensagens.Erro("Não foi possível apurar o período.", ex);
            return;
        }
        var feriados = Sessao.Repos.Feriados.Todos().ToDictionary(x => (x.Dia, x.Mes), x => x.Descricao);
        var linhas = _resultado.Dias.Select(d => new LinhaEspelho { Dia = d, Obs = Espelho.Observacao(d, feriados), Fundo = Fundo(d) }).ToList();
        GridDias.ItemsSource = linhas;
        if (manterDia is { } dia) GridDias.SelectedItem = linhas.FirstOrDefault(l => l.Dia.Data == dia.Date);

        var t = _resultado.Totais;
        TxtTotais.Text =
            $"Esperado: {Espelho.Duracao(t.Esperado, true)}    Trabalhado: {Espelho.Duracao(t.Trabalhado, true)}    " +
            $"Atraso: {Espelho.Duracao(t.Atraso, true)}    Extra: {Espelho.Duracao(t.Extra, true)}    Abonado: {Espelho.Duracao(t.Abonado, true)}    " +
            $"Saldo: {(t.Saldo < 0 ? "-" : "")}{Hora.Minutos(Math.Abs(t.Saldo))}    " +
            $"Faltas: {t.Faltas}    1/2 faltas: {t.MeiasFaltas}    Pendências: {t.Pendencias}    DSR: {t.DiasDsr}" +
            (t.AdicionalNoturno > 0 ? $"    Adicional noturno: {Hora.Minutos(t.AdicionalNoturno)}" : "");
        Navegacao.Status($"{f.Nome}: {linhas.Count} dia(s) apurados.");
    }

    private static Brush? Fundo(DiaApurado d) => d.Status switch
    {
        StatusDia.Falta or StatusDia.MeiaFalta => new SolidColorBrush(Color.FromRgb(0xFB, 0xDD, 0xDA)),
        StatusDia.Pendencia => new SolidColorBrush(Color.FromRgb(0xFF, 0xF1, 0xC9)),
        StatusDia.Feriado or StatusDia.Ferias or StatusDia.Dsr => new SolidColorBrush(Color.FromRgb(0xE3, 0xEE, 0xFB)),
        StatusDia.Abonado => new SolidColorBrush(Color.FromRgb(0xE0, 0xF3, 0xE0)),
        StatusDia.SemExpediente or StatusDia.Futuro => new SolidColorBrush(Color.FromRgb(0xF1, 0xF1, 0xF1)),
        _ => d.Atraso > 0 ? new SolidColorBrush(Color.FromRgb(0xFF, 0xF4, 0xE0)) : null,
    };

    private void GridDias_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.Item is LinhaEspelho l && l.Fundo is { } b) e.Row.Background = b;
        else e.Row.ClearValue(BackgroundProperty);
    }

    // ------------------------------------------------------------ detalhe do dia

    private DiaApurado? DiaSelecionado => (GridDias.SelectedItem as LinhaEspelho)?.Dia;

    private void GridDias_SelectionChanged(object sender, SelectionChangedEventArgs e) => CarregarMarcacoes();

    private void CarregarMarcacoes()
    {
        if (DiaSelecionado is not { } dia || Selecionado is not { } f) { GridMarcacoes.ItemsSource = null; TxtDiaSel.Text = ""; return; }
        TxtDiaSel.Text = $"Dia {dia.Data:dd/MM/yyyy}";
        var apuradas = dia.Marcacoes.ToDictionary(m => m.Origem.Id);
        var todas = Sessao.Repos.Marcacoes.Periodo(dia.Data.AddHours(-12), dia.Data.AddDays(1).AddHours(12), f.Codigo)
            .Where(m => m.DataHora.Date == dia.Data || apuradas.ContainsKey(m.Id))
            .OrderBy(m => m.DataHora)
            .Select(m =>
            {
                apuradas.TryGetValue(m.Id, out var a);
                return new LinhaMarcacao(m, m.DataHora.ToString("HH:mm"), Espelho.Origem(m),
                    a is null ? "" : Espelho.NomePosicao(a.Posicao),
                    a is null ? "" : Espelho.NomeSituacao(a.Situacao, a.Divergencia),
                    _just.TryGetValue(m.Justificativa, out var j) ? j.Descricao : "");
            }).ToList();
        GridMarcacoes.ItemsSource = todas;
    }

    private IReadOnlyList<(int, string)> ListaJust() => _just.Values.OrderBy(j => j.Codigo).Select(j => (j.Codigo, $"{j.Codigo:00} - {j.Descricao}")).ToList();

    private void BtnIncluir_Click(object sender, RoutedEventArgs e)
    {
        if (Selecionado is not { } f || DiaSelecionado is not { } dia) { Mensagens.Aviso("Selecione um dia."); return; }
        if (Dialogos.PedirMarcacao("Incluir marcação", dia.Data, ListaJust()) is not { } r) return;
        try
        {
            _servico.IncluirMarcacao(f, dia.Data + r.Hora, r.Justificativa);
            Recalcular(dia.Data);
        }
        catch (InvalidOperationException ex) { Mensagens.Aviso(ex.Message); }
        catch (Exception ex) { Mensagens.Erro("Não foi possível incluir a marcação.", ex); }
    }

    private void BtnDesprezar_Click(object sender, RoutedEventArgs e)
    {
        if (GridMarcacoes.SelectedItem is not LinhaMarcacao l || DiaSelecionado is not { } dia) { Mensagens.Aviso("Selecione uma marcação."); return; }
        if (l.M.Tipo == TipoMarcacao.Desprezada) { Mensagens.Aviso("Esta marcação já está desprezada."); return; }
        var msg = l.M.Tipo == TipoMarcacao.Coletada
            ? "A marcação coletada do relógio será desprezada (deixa de contar nos cálculos, mas pode ser restaurada). Continuar?"
            : "A marcação inserida será excluída. Continuar?";
        if (!Mensagens.Confirmar(msg)) return;
        _servico.DesprezarOuExcluir(l.M);
        Recalcular(dia.Data);
    }

    private void BtnRestaurar_Click(object sender, RoutedEventArgs e)
    {
        if (GridMarcacoes.SelectedItem is not LinhaMarcacao l || DiaSelecionado is not { } dia) { Mensagens.Aviso("Selecione uma marcação desprezada."); return; }
        if (l.M.Tipo != TipoMarcacao.Desprezada) { Mensagens.Aviso("A marcação selecionada não está desprezada."); return; }
        _servico.RestaurarDesprezada(l.M);
        Recalcular(dia.Data);
    }

    private void BtnJustificarDia_Click(object sender, RoutedEventArgs e)
    {
        if (Selecionado is not { } f || DiaSelecionado is not { } dia) { Mensagens.Aviso("Selecione um dia."); return; }
        if (Dialogos.EscolherJustificativa($"Justificar {dia.Data:dd/MM/yyyy}", ListaJust()) is not { } cod) return;
        _servico.JustificarDia(f, dia.Data, cod);
        Recalcular(dia.Data);
    }

    private void BtnRemoverJust_Click(object sender, RoutedEventArgs e)
    {
        if (Selecionado is not { } f || DiaSelecionado is not { } dia) { Mensagens.Aviso("Selecione um dia."); return; }
        _servico.RemoverJustificativaDia(f, dia.Data);
        Recalcular(dia.Data);
    }

    private void BtnRestaurarPeriodo_Click(object sender, RoutedEventArgs e)
    {
        if (Selecionado is not { } f || DtIni.SelectedDate is not { } ini || DtFim.SelectedDate is not { } fim) return;
        if (!Mensagens.Confirmar("Esta função apaga todas as marcações inseridas e as justificativas do período e refaz os cálculos com os dados coletados do relógio. Tem certeza que deseja restaurar as marcações do relógio?")) return;
        _servico.RestaurarPeriodo(f, ini, fim);
        Recalcular();
    }

    // ------------------------------------------------------------ impressão

    private void BtnImprimir_Click(object sender, RoutedEventArgs e)
    {
        if (Selecionado is null || DtIni.SelectedDate is not { } ini || DtFim.SelectedDate is not { } fim) return;
        Imprimir([Selecionado], ini, fim);
    }

    private void BtnImprimirTodos_Click(object sender, RoutedEventArgs e)
    {
        if (DtIni.SelectedDate is not { } ini || DtFim.SelectedDate is not { } fim) return;
        Imprimir(Sessao.Repos.Funcionarios.Todos("Nome"), ini, fim);
    }

    private void Imprimir(IEnumerable<Funcionario> funcionarios, DateTime ini, DateTime fim)
    {
        try
        {
            var empresa = Sessao.Repos.Parametros.Obter();
            var feriados = Sessao.Repos.Feriados.Todos().ToDictionary(x => (x.Dia, x.Mes), x => x.Descricao);
            var paginas = funcionarios
                .Select(f => Espelho.Montar(_servico.Apurar(f, ini, fim), ini, fim, feriados, Sessao.Repos.Horarios.Todos().ToDictionary(h => h.Codigo)))
                .ToList();
            if (paginas.Count == 0) { Mensagens.Aviso("Não há funcionários cadastrados."); return; }
            Relatorios.Visualizar("Espelho de Ponto", Relatorios.Montar(paginas, empresa), paginas);
        }
        catch (Exception ex)
        {
            Mensagens.Erro("Não foi possível gerar o espelho de ponto.", ex);
        }
    }
}
