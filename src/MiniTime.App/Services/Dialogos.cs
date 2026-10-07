using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MiniTime.Core.Util;

namespace MiniTime.App.Services;

/// <summary>Diálogos modais pequenos montados em código.</summary>
public static class Dialogos
{
    private static Window Janela(string titulo, double largura = 380)
        => new()
        {
            Title = titulo, Width = largura, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            Owner = Application.Current.MainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 13, ShowInTaskbar = false,
        };

    /// <summary>Pede uma hora (HH:MM) e, opcionalmente, uma justificativa. Retorna null se cancelado.</summary>
    public static (TimeSpan Hora, int Justificativa)? PedirMarcacao(string titulo, DateTime dia, IReadOnlyList<(int Codigo, string Descricao)> justificativas)
    {
        var w = Janela(titulo);
        var painel = new StackPanel { Margin = new Thickness(16) };
        painel.Children.Add(new TextBlock { Text = $"Dia {dia:dd/MM/yyyy}", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        painel.Children.Add(new TextBlock { Text = "Hora (HH:MM):" });
        var txt = new TextBox { Margin = new Thickness(0, 2, 0, 8), Padding = new Thickness(4, 3, 4, 3) };
        painel.Children.Add(txt);
        painel.Children.Add(new TextBlock { Text = "Justificativa:" });
        var cmb = new ComboBox { Margin = new Thickness(0, 2, 0, 12), DisplayMemberPath = "Descricao", SelectedValuePath = "Codigo" };
        cmb.ItemsSource = new[] { new { Codigo = 0, Descricao = "(nenhuma)" } }.Concat(justificativas.Select(j => new { j.Codigo, j.Descricao })).ToList();
        cmb.SelectedIndex = 0;
        painel.Children.Add(cmb);
        TimeSpan? hora = null;
        var ok = new Button { Content = "OK", IsDefault = true, Margin = new Thickness(0, 0, 6, 0), MinWidth = 80, Padding = new Thickness(10, 4, 10, 4) };
        var cancelar = new Button { Content = "Cancelar", IsCancel = true, MinWidth = 80, Padding = new Thickness(10, 4, 10, 4) };
        ok.Click += (_, _) =>
        {
            if (!Hora.TentarLer(txt.Text, out var h)) { Mensagens.Aviso("Hora inválida. Use HH:MM (ex.: 07:20)."); return; }
            hora = h;
            w.DialogResult = true;
        };
        painel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancelar } });
        w.Content = painel;
        w.Loaded += (_, _) => txt.Focus();
        return w.ShowDialog() == true && hora is { } hh ? (hh, cmb.SelectedValue is int c ? c : 0) : null;
    }

    /// <summary>Escolhe uma justificativa. Retorna o código ou null se cancelado.</summary>
    public static int? EscolherJustificativa(string titulo, IReadOnlyList<(int Codigo, string Descricao)> justificativas)
    {
        if (justificativas.Count == 0)
        {
            Mensagens.Aviso("Cadastre ao menos uma justificativa no menu Arquivos.");
            return null;
        }
        var w = Janela(titulo);
        var painel = new StackPanel { Margin = new Thickness(16) };
        painel.Children.Add(new TextBlock { Text = "Justificativa:" });
        var cmb = new ComboBox { Margin = new Thickness(0, 2, 0, 12), DisplayMemberPath = "Descricao", SelectedValuePath = "Codigo" };
        cmb.ItemsSource = justificativas.Select(j => new { j.Codigo, j.Descricao }).ToList();
        cmb.SelectedIndex = 0;
        painel.Children.Add(cmb);
        var ok = new Button { Content = "OK", IsDefault = true, Margin = new Thickness(0, 0, 6, 0), MinWidth = 80, Padding = new Thickness(10, 4, 10, 4) };
        var cancelar = new Button { Content = "Cancelar", IsCancel = true, MinWidth = 80, Padding = new Thickness(10, 4, 10, 4) };
        ok.Click += (_, _) => w.DialogResult = true;
        painel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancelar } });
        w.Content = painel;
        return w.ShowDialog() == true && cmb.SelectedValue is int c ? c : null;
    }

    /// <summary>Pede um intervalo de datas. Retorna null se cancelado.</summary>
    public static (DateTime Ini, DateTime Fim)? PedirPeriodo(string titulo, DateTime ini, DateTime fim)
    {
        var w = Janela(titulo);
        var painel = new StackPanel { Margin = new Thickness(16) };
        var dIni = new DatePicker { SelectedDate = ini, Margin = new Thickness(0, 2, 0, 8) };
        var dFim = new DatePicker { SelectedDate = fim, Margin = new Thickness(0, 2, 0, 12) };
        painel.Children.Add(new TextBlock { Text = "De:" });
        painel.Children.Add(dIni);
        painel.Children.Add(new TextBlock { Text = "Até:" });
        painel.Children.Add(dFim);
        var ok = new Button { Content = "OK", IsDefault = true, Margin = new Thickness(0, 0, 6, 0), MinWidth = 80, Padding = new Thickness(10, 4, 10, 4) };
        var cancelar = new Button { Content = "Cancelar", IsCancel = true, MinWidth = 80, Padding = new Thickness(10, 4, 10, 4) };
        ok.Click += (_, _) =>
        {
            if (dIni.SelectedDate is null || dFim.SelectedDate is null || dFim.SelectedDate < dIni.SelectedDate) { Mensagens.Aviso("Período inválido."); return; }
            w.DialogResult = true;
        };
        painel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancelar } });
        w.Content = painel;
        return w.ShowDialog() == true ? (dIni.SelectedDate!.Value, dFim.SelectedDate!.Value) : null;
    }
}
