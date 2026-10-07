using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using MiniTime.App.Services;
using MiniTime.Data;

namespace MiniTime.App.Controls;

/// <summary>int/bool ⇄ CheckBox (0/1 no banco).</summary>
public sealed class BoolIntConverter : IValueConverter
{
    public static readonly BoolIntConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch { bool b => b, int i => i != 0, long l => l != 0, _ => false };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is true;
        return Nullable.GetUnderlyingType(targetType) == typeof(bool) || targetType == typeof(bool) ? b : (b ? 1 : 0);
    }
}

/// <summary>
/// Tela de cadastro padrão: lista à esquerda, formulário à direita, barra Novo/Gravar/Excluir/Cancelar.
/// Cada cadastro só declara colunas, campos e regras de validação.
/// </summary>
public abstract class CadastroView<T> : UserControl where T : class, new()
{
    protected abstract Repository<T> Repo { get; }
    protected abstract string Titulo { get; }
    protected abstract IReadOnlyList<ColunaGrid> Colunas { get; }
    protected abstract IReadOnlyList<Campo> Campos { get; }
    protected abstract object[] Chave(T e);

    protected virtual string Ordem => "Codigo";
    protected virtual T NovoRegistro() => new();
    protected virtual string? Validar(T e, bool novo) => null;
    /// <summary>Retorne uma mensagem para impedir a exclusão (ex.: registro em uso).</summary>
    protected virtual string? AntesExcluir(T e) => null;
    protected virtual string Descricao(T e) => string.Join(" - ", Chave(e));
    /// <summary>Texto extra mostrado na confirmação de exclusão.</summary>
    protected virtual string? AvisoExclusao(T e) => null;
    /// <summary>Conteúdo extra abaixo dos campos (ex.: férias do funcionário).</summary>
    protected virtual UIElement? Extra => null;
    protected virtual void AoCarregar(T e) { }
    protected virtual void AoGravar(T e, bool novo) { }
    protected virtual void DepoisExcluir(T e) { }

    private readonly DataGrid _grid = new();
    private readonly TextBox _busca = new() { Width = 200 };
    private readonly Button _novo = new() { Content = "Novo", Style = null };
    private readonly Button _gravar = new() { Content = "Gravar" };
    private readonly Button _excluir = new() { Content = "Excluir" };
    private readonly Button _cancelar = new() { Content = "Cancelar" };
    private readonly Grid _form = new() { IsEnabled = false, Margin = new Thickness(0, 6, 0, 0) };
    private readonly TextBlock _estado = new() { Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Dictionary<Campo, FrameworkElement> _controles = new();
    private ICollectionView? _visao;
    private T? _editando;
    private bool _novoRegistro;

    protected T? Editando => _editando;
    protected bool EhNovo => _novoRegistro;

    /// <summary>Chamado pela subclasse no final do construtor.</summary>
    protected void Montar()
    {
        var raiz = new Grid { Margin = new Thickness(12) };
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        raiz.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        raiz.Children.Add(new TextBlock { Text = Titulo, Style = (Style)Application.Current.FindResource("Titulo") });

        var barra = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(barra, 1);
        _novo.Style = (Style)Application.Current.FindResource("BotaoPrimario");
        foreach (var b in new[] { _novo, _gravar, _excluir, _cancelar }) barra.Children.Add(b);
        barra.Children.Add(_estado);
        raiz.Children.Add(barra);

        var corpo = new Grid();
        corpo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 260 });
        corpo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        corpo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star), MinWidth = 320 });
        Grid.SetRow(corpo, 2);
        raiz.Children.Add(corpo);

        // lista
        var esquerda = new DockPanel();
        var painelBusca = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        painelBusca.Children.Add(new TextBlock { Text = "Localizar:", Style = (Style)Application.Current.FindResource("Rotulo") });
        painelBusca.Children.Add(_busca);
        DockPanel.SetDock(painelBusca, Dock.Top);
        esquerda.Children.Add(painelBusca);
        foreach (var c in Colunas)
        {
            var b = new Binding(c.Caminho) { StringFormat = c.Formato, Converter = c.Conversor };
            _grid.Columns.Add(new DataGridTextColumn { Header = c.Titulo, Binding = b, Width = c.Largura <= 0 ? new DataGridLength(1, DataGridLengthUnitType.Star) : c.Largura });
        }
        esquerda.Children.Add(_grid);
        corpo.Children.Add(esquerda);

        // formulário
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(scroll, 2);
        var direita = new StackPanel();
        ConstruirCampos();
        direita.Children.Add(_form);
        if (Extra is { } extra) direita.Children.Add(extra);
        scroll.Content = direita;
        corpo.Children.Add(scroll);
        Content = raiz;

        _novo.Click += (_, _) => AoNovo();
        _gravar.Click += (_, _) => AoGravarClick();
        _excluir.Click += (_, _) => AoExcluir();
        _cancelar.Click += (_, _) => AoCancelar();
        _grid.SelectionChanged += (_, _) => AoSelecionar();
        _busca.TextChanged += (_, _) => _visao?.Refresh();
        _grid.PreviewKeyDown += (_, e) => { if (e.Key == Key.Delete && _excluir.IsEnabled) AoExcluir(); };

        Recarregar();
        Estado(false);
    }

    private void ConstruirCampos()
    {
        foreach (var (campo, ctl) in FormularioBuilder.Construir(Campos, _form)) _controles[campo] = ctl;
    }

    // ---------- estado ----------

    private void Recarregar(object[]? selecionarChave = null)
    {
        var lista = Repo.Todos(Ordem);
        _visao = CollectionViewSource.GetDefaultView(lista);
        _visao.Filter = o => Passa((T)o);
        _grid.ItemsSource = _visao;
        if (selecionarChave is not null)
            _grid.SelectedItem = lista.FirstOrDefault(x => Chave(x).SequenceEqual(selecionarChave));
        Navegacao.Status($"{Titulo}: {lista.Count} registro(s)");
    }

    private bool Passa(T e)
    {
        var termo = _busca.Text.Trim();
        if (termo.Length == 0) return true;
        foreach (var c in Colunas)
        {
            var v = typeof(T).GetProperty(c.Caminho.Split('.')[0])?.GetValue(e);
            var s = v switch { TimeSpan t => Core.Util.Hora.Formatar(t), null => "", _ => Convert.ToString(v, CultureInfo.CurrentCulture) ?? "" };
            if (s.Contains(termo, StringComparison.CurrentCultureIgnoreCase)) return true;
        }
        return false;
    }

    private void Estado(bool editando)
    {
        _form.IsEnabled = editando;
        _gravar.IsEnabled = editando;
        _cancelar.IsEnabled = editando;
        _excluir.IsEnabled = !_novoRegistro && _editando is not null;
        _novo.IsEnabled = true;
        foreach (var (campo, ctl) in _controles)
            if (campo.SomenteNovo) ctl.IsEnabled = _novoRegistro;
        _estado.Text = !editando ? "Selecione um registro ou clique em Novo." : _novoRegistro ? "Novo registro" : "Editando registro";
    }

    private void Carregar(T e, bool novo)
    {
        _editando = e;
        _novoRegistro = novo;
        _form.DataContext = e;
        AoCarregar(e);
        Estado(true);
    }

    private void AoSelecionar()
    {
        if (_grid.SelectedItem is T sel) Carregar(Clonar(sel), false);
        else if (!_novoRegistro) { _editando = null; _form.DataContext = null; Estado(false); }
    }

    private void AoNovo()
    {
        _grid.SelectedItem = null;
        var e = NovoRegistro();
        Carregar(e, true);
        Estado(true);
        if (_controles.Values.FirstOrDefault(c => c.IsEnabled) is { } primeiro) primeiro.Focus();
    }

    private void AoCancelar()
    {
        _novoRegistro = false;
        if (_grid.SelectedItem is T sel) Carregar(Clonar(sel), false);
        else { _editando = null; _form.DataContext = null; Estado(false); }
    }

    private void AoGravarClick()
    {
        if (_editando is null) return;
        // força a atualização do campo em foco
        Keyboard.ClearFocus();
        if (_controles.Values.Any(Validation.GetHasError))
        {
            Mensagens.Aviso("Corrija os campos destacados antes de gravar.");
            return;
        }
        var erro = Validar(_editando, _novoRegistro);
        if (erro is not null) { Mensagens.Aviso(erro); return; }
        try
        {
            if (_novoRegistro)
            {
                if (Repo.Existe(Chave(_editando))) { Mensagens.Aviso("Já existe um registro com este código."); return; }
                Repo.Inserir(_editando);
            }
            else Repo.Atualizar(_editando);
            var novo = _novoRegistro;
            _novoRegistro = false;
            AoGravar(_editando, novo);
            Recarregar(Chave(_editando));
            Navegacao.Status("Registro gravado.");
        }
        catch (Exception ex)
        {
            Mensagens.Erro("Não foi possível gravar o registro.", ex);
        }
    }

    private void AoExcluir()
    {
        if (_editando is null || _novoRegistro) return;
        var bloqueio = AntesExcluir(_editando);
        if (bloqueio is not null) { Mensagens.Aviso(bloqueio); return; }
        if (!Mensagens.Confirmar($"Confirma a exclusão de {Descricao(_editando)}?" + (AvisoExclusao(_editando) is { } av ? "\n\n" + av : ""))) return;
        try
        {
            Repo.Excluir(Chave(_editando));
            DepoisExcluir(_editando);
            _editando = null;
            _form.DataContext = null;
            Recarregar();
            Estado(false);
            Navegacao.Status("Registro excluído.");
        }
        catch (Exception ex)
        {
            Mensagens.Erro("Não foi possível excluir o registro.", ex);
        }
    }

    private static T Clonar(T origem)
    {
        var copia = new T();
        foreach (var p in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite))
            p.SetValue(copia, p.GetValue(origem));
        return copia;
    }
}
