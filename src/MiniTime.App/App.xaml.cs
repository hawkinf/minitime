using System.Windows;
using System.Windows.Threading;
using MiniTime.App.Services;

namespace MiniTime.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnErroNaoTratado;
        try
        {
            Sessao.Iniciar();
        }
        catch (Exception ex)
        {
            Mensagens.Erro("Não foi possível abrir o banco de dados.", ex);
            Shutdown(1);
            return;
        }
        new MainWindow().Show();
    }

    private static void OnErroNaoTratado(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Mensagens.Erro("Ocorreu um erro inesperado.", e.Exception);
        e.Handled = true;
    }
}
