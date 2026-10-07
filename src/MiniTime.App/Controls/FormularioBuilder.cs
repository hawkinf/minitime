using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace MiniTime.App.Controls;

/// <summary>Constrói controles de formulário (rótulo + campo) a partir de uma lista de <see cref="Campo"/>.</summary>
public static class FormularioBuilder
{
    public static Dictionary<Campo, FrameworkElement> Construir(IEnumerable<Campo> campos, Grid form)
    {
        var controles = new Dictionary<Campo, FrameworkElement>();
        if (form.ColumnDefinitions.Count == 0)
        {
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 140 });
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        var linha = 0;
        string? grupoAtual = null;
        foreach (var c in campos)
        {
            if (c.Grupo is not null && c.Grupo != grupoAtual)
            {
                grupoAtual = c.Grupo;
                form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var t = new TextBlock { Text = c.Grupo, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4), Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("Primaria") };
                Grid.SetRow(t, linha); Grid.SetColumnSpan(t, 2);
                form.Children.Add(t);
                linha++;
            }
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var rotulo = new TextBlock { Text = c.Rotulo, Style = (Style)Application.Current.FindResource("Rotulo"), Margin = new Thickness(0, 4, 8, 4) };
            Grid.SetRow(rotulo, linha);
            form.Children.Add(rotulo);

            var ctl = CriarControle(c);
            ctl.Margin = new Thickness(0, 3, 0, 3);
            ctl.HorizontalAlignment = HorizontalAlignment.Left;
            if (c.Dica is not null) ctl.ToolTip = c.Dica;
            Grid.SetRow(ctl, linha); Grid.SetColumn(ctl, 1);
            form.Children.Add(ctl);
            controles[c] = ctl;
            linha++;
        }
        return controles;
    }

    private static FrameworkElement CriarControle(Campo c)
    {
        switch (c.Tipo)
        {
            case TipoCampo.Booleano:
            {
                var cb = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
                cb.SetBinding(ToggleButton_IsChecked, new Binding(c.Propriedade) { Mode = BindingMode.TwoWay, Converter = BoolIntConverter.Instance });
                return cb;
            }
            case TipoCampo.Data:
            {
                var dp = new DatePicker { Width = c.Largura };
                dp.SetBinding(DatePicker.SelectedDateProperty, new Binding(c.Propriedade) { Mode = BindingMode.TwoWay, ValidatesOnExceptions = true });
                return dp;
            }
            case TipoCampo.Escolha:
            {
                var cb = new ComboBox { Width = c.Largura, DisplayMemberPath = "Texto", SelectedValuePath = "Valor" };
                cb.ItemsSource = (c.Opcoes ?? []).Select(o => new Opcao(o.Valor, o.Texto)).ToList();
                cb.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedValueProperty, new Binding(c.Propriedade) { Mode = BindingMode.TwoWay });
                return cb;
            }
            case TipoCampo.EscolhaTexto:
            {
                var cb = new ComboBox { Width = c.Largura, DisplayMemberPath = "Texto", SelectedValuePath = "Valor" };
                cb.ItemsSource = (c.OpcoesTexto ?? []).Select(o => new OpcaoTexto(o.Valor, o.Texto)).ToList();
                cb.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedValueProperty, new Binding(c.Propriedade) { Mode = BindingMode.TwoWay });
                return cb;
            }
            default:
            {
                var tb = new TextBox { Width = c.Largura };
                if (c.MaxLength > 0) tb.MaxLength = c.MaxLength;
                var b = new Binding(c.Propriedade) { Mode = BindingMode.TwoWay, ValidatesOnExceptions = true, TargetNullValue = "", UpdateSourceTrigger = UpdateSourceTrigger.LostFocus };
                if (c.Tipo == TipoCampo.Hora) { b.Converter = HoraConverter.Instance; tb.Width = Math.Min(c.Largura, 80); tb.ToolTip ??= "HH:MM (ex.: 07:20 ou 0720)"; }
                if (c.Tipo == TipoCampo.Duracao) { b.Converter = MinutosConverter.Instance; tb.Width = Math.Min(c.Largura, 80); tb.ToolTip ??= "HH:MM (ex.: 44:00)"; }
                if (c.Tipo == TipoCampo.Inteiro)
                {
                    tb.Width = Math.Min(c.Largura, 100);
                    tb.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
                }
                tb.SetBinding(TextBox.TextProperty, b);
                tb.KeyDown += (_, e) => { if (e.Key == Key.Enter) tb.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); };
                return tb;
            }
        }
    }

    private static DependencyProperty ToggleButton_IsChecked => System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty;

    private sealed record Opcao(int Valor, string Texto);
    private sealed record OpcaoTexto(string Valor, string Texto);

}
