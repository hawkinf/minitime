using System.Windows;
using System.Windows.Controls;

namespace MiniTime.App.Views;

/// <summary>
/// Inicialização geral do relógio (envio de data/hora, cartões, faixa horária, alarmes e horário de verão).
/// Ainda não disponível: depende da validação dos comandos do protocolo com o relógio real.
/// </summary>
public sealed class InicializacaoView : UserControl
{
    public InicializacaoView()
    {
        var raiz = new StackPanel { Margin = new Thickness(16), MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left };
        raiz.Children.Add(Ui.Titulo("Inicialização geral / Status do relógio"));
        raiz.Children.Add(new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xF4, 0xD6)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0xB8, 0x4C)),
            BorderThickness = new Thickness(1), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 12),
            Child = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = "Ainda não disponível nesta versão. Estes comandos (acerto de data/hora, envio de cartões, faixa horária, alarmes e horário de verão, leitura de status) " +
                       "dependem de validar o protocolo com o relógio ligado ao computador. A coleta de marcações (menu Comunicação) já usa o protocolo reconstruído e registra o tráfego serial para ajuste.",
            },
        });
        raiz.Children.Add(new TextBlock { Text = "O programa antigo enviava ao relógio:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        foreach (var t in new[]
                 {
                     "Data e hora do computador", "Números dos cartões cadastrados (o Mini Point guarda até 50)", "Faixa horária em que aceita marcações",
                     "Faixa horária dos toques de alarme", "Informações de horário de verão", "Quantidade de dígitos do cartão e checagem",
                 })
            raiz.Children.Add(new TextBlock { Text = "• " + t, Margin = new Thickness(12, 0, 0, 2) });
        Content = raiz;
    }
}
