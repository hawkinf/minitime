using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MiniTime.App.Services;

namespace MiniTime.App.Controls;

/// <summary>Tela de um único registro (configurações): formulário + Gravar/Desfazer.</summary>
public abstract class EdicaoUnicaView<T> : UserControl where T : class, new()
{
    protected abstract string Titulo { get; }
    protected abstract IReadOnlyList<Campo> Campos { get; }
    protected abstract T Carregar();
    protected abstract void Salvar(T e);
    protected virtual string? Validar(T e) => null;
    protected virtual UIElement? Rodape => null;
    protected virtual string? Descricao => null;

    private readonly Grid _form = new() { Margin = new Thickness(0, 8, 0, 0) };
    private Dictionary<Campo, FrameworkElement> _controles = new();
    private T? _atual;

    protected T? Atual => _atual;

    protected void Montar()
    {
        var raiz = new StackPanel { Margin = new Thickness(16) };
        raiz.Children.Add(new TextBlock { Text = Titulo, Style = (Style)Application.Current.FindResource("Titulo") });
        if (Descricao is { } d) raiz.Children.Add(new TextBlock { Text = d, TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.Gray, MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left });
        _controles = FormularioBuilder.Construir(Campos, _form);
        raiz.Children.Add(_form);
        if (Rodape is { } r) raiz.Children.Add(r);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        var gravar = new Button { Content = "Gravar", Style = (Style)Application.Current.FindResource("BotaoPrimario") };
        var desfazer = new Button { Content = "Desfazer" };
        gravar.Click += (_, _) => AoGravar();
        desfazer.Click += (_, _) => Recarregar();
        botoes.Children.Add(gravar);
        botoes.Children.Add(desfazer);
        raiz.Children.Add(botoes);
        Content = new ScrollViewer { Content = raiz, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Recarregar();
    }

    protected void Recarregar()
    {
        _atual = Carregar();
        _form.DataContext = _atual;
        AoCarregar(_atual);
    }

    protected virtual void AoCarregar(T e) { }

    private void AoGravar()
    {
        if (_atual is null) return;
        Keyboard.ClearFocus();
        if (_controles.Values.Any(System.Windows.Controls.Validation.GetHasError))
        {
            Mensagens.Aviso("Corrija os campos destacados antes de gravar.");
            return;
        }
        if (Validar(_atual) is { } erro) { Mensagens.Aviso(erro); return; }
        try
        {
            Salvar(_atual);
            Navegacao.Status("Configurações gravadas.");
            Mensagens.Info("Dados gravados com sucesso.");
        }
        catch (Exception ex)
        {
            Mensagens.Erro("Não foi possível gravar.", ex);
        }
    }
}
