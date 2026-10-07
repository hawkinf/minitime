using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MiniTime.App.Services;
using MiniTime.Core.Apuracao;
using MiniTime.Core.Models;
using MiniTime.Core.Util;

namespace MiniTime.App.Views;

/// <summary>Listagens dos cadastros (cartões, horários, jornadas, feriados, férias, alarmes, justificativas).</summary>
public sealed class ListagensView : UserControl
{
    private readonly ComboBox _tipo = new() { Width = 260, Margin = new Thickness(0, 0, 14, 0) };
    private readonly TextBox _de = new() { Width = 90 };
    private readonly TextBox _ate = new() { Width = 90 };
    private readonly TextBox _nome = new() { Width = 220 };

    private static readonly string[] Tipos = ["Cartões (funcionários)", "Horários de trabalho", "Jornadas", "Feriados", "Férias", "Alarmes (sirene)", "Justificativas"];

    public ListagensView()
    {
        var raiz = new StackPanel { Margin = new Thickness(16) };
        raiz.Children.Add(new TextBlock { Text = "Relatórios de cadastros", Style = (Style)Application.Current.FindResource("Titulo") });
        var linha1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        linha1.Children.Add(new TextBlock { Text = "Relatório:", Style = (Style)Application.Current.FindResource("Rotulo") });
        _tipo.ItemsSource = Tipos;
        _tipo.SelectedIndex = 0;
        linha1.Children.Add(_tipo);
        raiz.Children.Add(linha1);

        var linha2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        linha2.Children.Add(new TextBlock { Text = "Código de:", Style = (Style)Application.Current.FindResource("Rotulo") });
        linha2.Children.Add(_de);
        linha2.Children.Add(new TextBlock { Text = "até:", Style = (Style)Application.Current.FindResource("Rotulo"), Margin = new Thickness(12, 0, 8, 0) });
        linha2.Children.Add(_ate);
        linha2.Children.Add(new TextBlock { Text = "Descrição/Nome contém:", Style = (Style)Application.Current.FindResource("Rotulo"), Margin = new Thickness(16, 0, 8, 0) });
        linha2.Children.Add(_nome);
        raiz.Children.Add(linha2);
        raiz.Children.Add(new TextBlock { Text = "Deixe os filtros em branco para listar todos os registros.", Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 0, 0, 12) });

        var gerar = new Button { Content = "Visualizar / Imprimir…", Style = (Style)Application.Current.FindResource("BotaoPrimario"), HorizontalAlignment = HorizontalAlignment.Left };
        gerar.Click += (_, _) => Gerar();
        raiz.Children.Add(gerar);
        Content = raiz;
    }

    private bool FiltraCodigo(string codigoNumerico)
    {
        if (!long.TryParse(codigoNumerico, out var c)) return true;
        if (long.TryParse(_de.Text, out var de) && c < de) return false;
        if (long.TryParse(_ate.Text, out var ate) && c > ate) return false;
        return true;
    }

    private bool FiltraTexto(string texto)
        => string.IsNullOrWhiteSpace(_nome.Text) || texto.Contains(_nome.Text.Trim(), StringComparison.CurrentCultureIgnoreCase);

    private void Gerar()
    {
        try
        {
            var rel = _tipo.SelectedIndex switch
            {
                0 => Cartoes(), 1 => Horarios(), 2 => Jornadas(), 3 => Feriados(), 4 => Ferias(), 5 => Alarmes(), _ => Justificativas(),
            };
            Relatorios.Visualizar(rel.Titulo, Relatorios.Montar([rel], Sessao.Repos.Parametros.Obter()), [rel]);
        }
        catch (Exception ex)
        {
            Mensagens.Erro("Não foi possível gerar o relatório.", ex);
        }
    }

    private static string H(TimeSpan? t) => Core.Util.Hora.Formatar(t);
    private static string Sim(int v) => v != 0 ? "Sim" : "Não";

    private RelatorioTabela Cartoes()
    {
        var jor = Sessao.Repos.Jornadas.Todos().ToDictionary(j => j.Codigo);
        var r = new RelatorioTabela
        {
            Titulo = "Relatório de Cartões",
            Colunas = [new("Cartão", 7), new("Nome", 26), new("Função", 14), new("Jornada", 20), new("H.Extra", 6, TextAlignment.Center), new("Almoço móvel", 8, TextAlignment.Center), new("Noturno", 6, TextAlignment.Center)],
        };
        foreach (var f in Sessao.Repos.Funcionarios.Todos("Codigo").Where(f => FiltraCodigo(f.CodigoCurto) && FiltraTexto(f.Nome)))
            r.Linhas.Add([f.CodigoCurto, f.Nome, f.Cargo ?? "", jor.TryGetValue(f.Horario, out var j) ? j.Descricao : "Sem horário", Sim(f.AutHoraExtra), Sim(f.HorarioAlmoco), f.HorarioNoturno ? "Sim" : "Não"]);
        r.Rodape.Add($"Total: {r.Linhas.Count} cartão(ões)");
        return r;
    }

    private RelatorioTabela Horarios()
    {
        var r = new RelatorioTabela
        {
            Titulo = "Relatório de Horários de Trabalho", Paisagem = true,
            Colunas = [new("Cód.", 4), new("Descrição", 22), new("Entrada", 6, TextAlignment.Center), new("Saída int.", 6, TextAlignment.Center), new("Retorno", 6, TextAlignment.Center), new("Saída", 6, TextAlignment.Center),
                       new("Tol. atraso (E/R/S)", 9, TextAlignment.Center), new("Tol. extra (E/I/S)", 9, TextAlignment.Center), new("Mín. int.", 6, TextAlignment.Center), new("Noturno", 6, TextAlignment.Center)],
        };
        foreach (var h in Sessao.Repos.Horarios.Todos("Codigo").Where(h => FiltraCodigo(h.Codigo.ToString()) && FiltraTexto(h.Descricao)))
            r.Linhas.Add([h.Codigo.ToString("00"), h.Descricao, H(h.DeSS1), H(h.DeSS2), H(h.DeSS3), H(h.DeSS4), $"{h.TolManha}/{h.TolTarde}/{h.TolSaida}", $"{h.TolExtEnt}/{h.TolExtInt}/{h.TolExtSai}", h.RefMinimo.ToString(), Sim(h.Noturno)]);
        return r;
    }

    private RelatorioTabela Jornadas()
    {
        var hor = Sessao.Repos.Horarios.Todos().ToDictionary(h => h.Codigo);
        string Dia(int c) => c == 0 ? "—" : hor.TryGetValue(c, out var h) ? $"{c:00}" : "?";
        var r = new RelatorioTabela
        {
            Titulo = "Relatório de Jornadas",
            Colunas = [new("Cód.", 4), new("Descrição", 24), new("Seg", 4, TextAlignment.Center), new("Ter", 4, TextAlignment.Center), new("Qua", 4, TextAlignment.Center), new("Qui", 4, TextAlignment.Center),
                       new("Sex", 4, TextAlignment.Center), new("Sáb", 4, TextAlignment.Center), new("Dom", 4, TextAlignment.Center), new("DSR", 4, TextAlignment.Center), new("Não marca falta", 6, TextAlignment.Center)],
        };
        foreach (var j in Sessao.Repos.Jornadas.Todos("Codigo").Where(j => FiltraCodigo(j.Codigo.ToString()) && FiltraTexto(j.Descricao)))
            r.Linhas.Add([j.Codigo.ToString("00"), j.Descricao, Dia(j.Segunda), Dia(j.Terca), Dia(j.Quarta), Dia(j.Quinta), Dia(j.Sexta), Dia(j.Sabado), Dia(j.Domingo), Sim(j.TrataDSR), Sim(j.NaoMarcarFalta)]);
        r.Rodape.Add("Os números nos dias da semana são os códigos dos horários de trabalho (— = sem expediente).");
        return r;
    }

    private RelatorioTabela Feriados()
    {
        var r = new RelatorioTabela { Titulo = "Relatório de Feriados", Colunas = [new("Cód.", 4), new("Dia/Mês", 6, TextAlignment.Center), new("Descrição", 30)] };
        foreach (var f in Sessao.Repos.Feriados.Todos("Mes, Dia").Where(f => FiltraCodigo(f.Codigo.ToString()) && FiltraTexto(f.Descricao)))
            r.Linhas.Add([f.Codigo.ToString("00"), $"{f.Dia:00}/{f.Mes:00}", f.Descricao]);
        return r;
    }

    private RelatorioTabela Ferias()
    {
        var nomes = Sessao.Repos.Funcionarios.Todos().ToDictionary(f => f.Codigo);
        var r = new RelatorioTabela { Titulo = "Relatório de Férias", Colunas = [new("Cartão", 7), new("Nome", 26), new("Início", 8, TextAlignment.Center), new("Fim", 8, TextAlignment.Center), new("Dias", 4, TextAlignment.Center)] };
        foreach (var f in Sessao.Repos.Ferias.Todos("Funcionario, Inicio"))
        {
            var func = nomes.GetValueOrDefault(f.Funcionario);
            var curto = CodigoCartao.Curto(f.Funcionario);
            if (!FiltraCodigo(curto) || !FiltraTexto(func?.Nome ?? "")) continue;
            r.Linhas.Add([curto, func?.Nome ?? "(excluído)", f.Inicio.ToString("dd/MM/yyyy"), f.Fim.ToString("dd/MM/yyyy"), ((int)(f.Fim - f.Inicio).TotalDays + 1).ToString()]);
        }
        return r;
    }

    private RelatorioTabela Alarmes()
    {
        var r = new RelatorioTabela { Titulo = "Relatório de Alarmes (sirene)", Colunas = [new("Cód.", 4), new("Descrição", 24), new("Horário", 6, TextAlignment.Center), new("Duração (s)", 6, TextAlignment.Center), new("Dias úteis", 6, TextAlignment.Center), new("Fim de semana", 7, TextAlignment.Center)] };
        foreach (var s in Sessao.Repos.Sirenes.Todos("Codigo").Where(s => FiltraCodigo(s.Codigo.ToString()) && FiltraTexto(s.Descricao)))
            r.Linhas.Add([s.Codigo.ToString("00"), s.Descricao, H(s.Horario), s.Duracao.ToString(), s.Util ? "Sim" : "Não", s.FimSem ? "Sim" : "Não"]);
        return r;
    }

    private RelatorioTabela Justificativas()
    {
        var r = new RelatorioTabela { Titulo = "Relatório de Justificativas", Colunas = [new("Cód.", 4), new("Descrição", 40)] };
        foreach (var j in Sessao.Repos.Justificativas.Todos("Codigo").Where(j => FiltraCodigo(j.Codigo.ToString()) && FiltraTexto(j.Descricao)))
            r.Linhas.Add([j.Codigo.ToString("00"), j.Descricao]);
        return r;
    }
}

/// <summary>Listagem de marcações por cartão, período e tipo de tratamento.</summary>
public sealed class MarcacoesRelatorioView : UserControl
{
    private readonly ComboBox _cartao = new() { Width = 300 };
    private readonly DatePicker _ini = new() { Width = 150 };
    private readonly DatePicker _fim = new() { Width = 150 };
    private readonly TextBox _horaDe = new() { Width = 60, Text = "00:00" };
    private readonly TextBox _horaAte = new() { Width = 60, Text = "23:59" };
    private readonly ComboBox _filtro = new() { Width = 260 };

    public MarcacoesRelatorioView()
    {
        var raiz = new StackPanel { Margin = new Thickness(16) };
        raiz.Children.Add(new TextBlock { Text = "Listagem de marcações", Style = (Style)Application.Current.FindResource("Titulo") });

        _cartao.ItemsSource = new[] { new Item(null, "(Todos os cartões)") }
            .Concat(Sessao.Repos.Funcionarios.Todos("Nome").Select(f => new Item(f.Codigo, $"{f.CodigoCurto} - {f.Nome}"))).ToList();
        _cartao.DisplayMemberPath = nameof(Item.Texto);
        _cartao.SelectedIndex = 0;
        _filtro.ItemsSource = new[] { "Todos os dados", "Dados não tratados (coletados do relógio)", "Dados tratados", "Dados tratados manualmente" };
        _filtro.SelectedIndex = 0;
        var (ini, fim) = ApuracaoService_Periodo();
        _ini.SelectedDate = ini; _fim.SelectedDate = fim;

        Linha(raiz, "Cartão:", _cartao);
        Linha(raiz, "Período:", _ini, new TextBlock { Text = "a", Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center }, _fim);
        Linha(raiz, "Hora:", _horaDe, new TextBlock { Text = "a", Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center }, _horaAte);
        Linha(raiz, "Filtro:", _filtro);

        var gerar = new Button { Content = "Visualizar / Imprimir…", Style = (Style)Application.Current.FindResource("BotaoPrimario"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0) };
        gerar.Click += (_, _) => Gerar();
        raiz.Children.Add(gerar);
        Content = raiz;
    }

    private sealed record Item(string? Codigo, string Texto);

    private static (DateTime, DateTime) ApuracaoService_Periodo()
        => MiniTime.Data.ApuracaoService.PeriodoPadrao(DateTime.Today, Sessao.Repos.Parametros.Obter().DiaFechamento);

    private static void Linha(StackPanel pai, string rotulo, params UIElement[] controles)
    {
        var l = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        l.Children.Add(new TextBlock { Text = rotulo, Width = 70, VerticalAlignment = VerticalAlignment.Center });
        foreach (var c in controles) l.Children.Add(c);
        pai.Children.Add(l);
    }

    private void Gerar()
    {
        if (_ini.SelectedDate is not { } ini || _fim.SelectedDate is not { } fim || fim < ini) { Mensagens.Aviso("Período inválido."); return; }
        if (!Hora.TentarLer(_horaDe.Text, out var hDe) || !Hora.TentarLer(_horaAte.Text, out var hAte)) { Mensagens.Aviso("Hora inválida. Use HH:MM."); return; }
        var cracha = (_cartao.SelectedItem as Item)?.Codigo;
        try
        {
            var marcacoes = Sessao.Repos.Marcacoes.Periodo(ini.Date, fim.Date.AddDays(1).AddSeconds(-1), cracha)
                .Where(m => m.DataHora.TimeOfDay >= hDe && m.DataHora.TimeOfDay <= hAte + TimeSpan.FromSeconds(59))
                .Where(Passa).ToList();
            var nomes = Sessao.Repos.Funcionarios.Todos().ToDictionary(f => f.Codigo, f => f.Nome);
            var rel = new RelatorioTabela
            {
                Titulo = "Listagem de Marcações",
                Subtitulo = $"Período: {ini:dd/MM/yyyy} a {fim:dd/MM/yyyy}  |  Hora: {_horaDe.Text} a {_horaAte.Text}  |  {_filtro.SelectedItem}",
                Colunas = [new("Cartão", 7), new("Nome", 26), new("Data", 9, TextAlignment.Center), new("Hora", 6, TextAlignment.Center), new("Dia", 5), new("Origem", 14)],
            };
            foreach (var m in marcacoes.OrderBy(m => m.Cracha).ThenBy(m => m.DataHora))
                rel.Linhas.Add([CodigoCartao.Curto(m.Cracha), nomes.GetValueOrDefault(m.Cracha, ""), m.DataHora.ToString("dd/MM/yyyy"), m.DataHora.ToString("HH:mm"),
                                CultureInfo.GetCultureInfo("pt-BR").DateTimeFormat.GetAbbreviatedDayName(m.DataHora.DayOfWeek), Espelho.Origem(m)]);
            rel.Rodape.Add($"Total: {rel.Linhas.Count} marcação(ões)");
            Relatorios.Visualizar(rel.Titulo, Relatorios.Montar([rel], Sessao.Repos.Parametros.Obter()), [rel]);
        }
        catch (Exception ex)
        {
            Mensagens.Erro("Não foi possível gerar a listagem.", ex);
        }
    }

    private bool Passa(Marcacao m) => _filtro.SelectedIndex switch
    {
        1 => m.Tipo == TipoMarcacao.Coletada,
        2 => m.Tipo != TipoMarcacao.Coletada,
        3 => (m.Tipo == TipoMarcacao.Manual && m.Justificativa != TipoMarcacao.JustificativaAutomatica) || m.Tipo == TipoMarcacao.Desprezada,
        _ => true,
    };
}
