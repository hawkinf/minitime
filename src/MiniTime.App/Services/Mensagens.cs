using System.Windows;
using MiniTime.App.Services;

namespace MiniTime.App;

/// <summary>Diálogos padronizados; exceções nunca aparecem cruas para o usuário.</summary>
public static class Mensagens
{
    public static void Info(string texto) => MessageBox.Show(texto, "MiniTime", MessageBoxButton.OK, MessageBoxImage.Information);

    public static void Aviso(string texto) => MessageBox.Show(texto, "MiniTime", MessageBoxButton.OK, MessageBoxImage.Warning);

    public static void Erro(string texto, Exception? ex = null)
    {
        if (ex is not null) Log.Erro(texto, ex);
        MessageBox.Show(texto + (ex is null ? "" : "\n\nDetalhes foram gravados no log do programa."), "MiniTime", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public static bool Confirmar(string texto)
        => MessageBox.Show(texto, "MiniTime", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
