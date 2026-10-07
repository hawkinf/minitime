using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MiniTime.App.Services;
using MiniTime.Data.Importacao;

namespace MiniTime.App.Views;

public partial class ImportacaoMdbView : UserControl
{
    private CancellationTokenSource? _cts;

    public ImportacaoMdbView()
    {
        InitializeComponent();
        var padrao = @"C:\Program Files (x86)\Dimep\MiniTime\DIMEP.MDB";
        if (File.Exists(padrao)) TxtArquivo.Text = padrao;
        TxtSenha.Password = Sessao.Settings.SenhaMdb ?? "";
        if (MdbReaderLocator.Localizar() is null)
            TxtRelatorio.Text = "ATENÇÃO: o leitor de MDB (MdbReader\\MiniTime.MdbReader.exe) não foi encontrado ao lado do programa.";
    }

    private void BtnProcurar_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Selecione o DIMEP.MDB", Filter = "Banco Access (*.mdb)|*.mdb|Todos os arquivos|*.*" };
        if (File.Exists(TxtArquivo.Text)) dlg.InitialDirectory = Path.GetDirectoryName(TxtArquivo.Text);
        if (dlg.ShowDialog() == true) TxtArquivo.Text = dlg.FileName;
    }

    private async void BtnImportar_Click(object sender, RoutedEventArgs e)
    {
        var arquivo = TxtArquivo.Text.Trim();
        if (!File.Exists(arquivo)) { Mensagens.Aviso("Selecione um arquivo MDB existente."); return; }
        var substituir = ChkSubstituir.IsChecked == true;
        if (substituir && Sessao.Repos.Funcionarios.Contar() + Sessao.Repos.Marcacoes.Contar() > 0 &&
            !Mensagens.Confirmar("Os dados atuais do banco serão SUBSTITUÍDOS pelos do MDB. Deseja continuar?"))
            return;

        _cts = new CancellationTokenSource();
        Alternar(true);
        TxtRelatorio.Clear();
        var progresso = new Progress<MdbImportProgress>(p =>
        {
            TxtProgresso.Text = p.Mensagem;
            Barra.Value = p.Total > 0 ? 100.0 * p.Atual / p.Total : 0;
        });

        try
        {
            var opt = new MdbImportOptions { MdbPath = arquivo, Substituir = substituir, Senha = TxtSenha.Password };
            Sessao.Settings.SenhaMdb = string.IsNullOrEmpty(TxtSenha.Password) ? null : TxtSenha.Password;
            Sessao.Settings.Salvar();
            var rel = await Task.Run(() => new MdbImporter(Sessao.Db).Importar(opt, progresso, _cts.Token));
            TxtRelatorio.Text = rel.Resumo() +
                (rel.TotalQuarentena > 0 ? "\nRegistros em quarentena ficam na tabela ImportacaoQuarentena (datas absurdas, duplicados)." : "");
            Barra.Value = 100;
            TxtProgresso.Text = "Importação concluída.";
            Log.Info("Importação MDB concluída:\n" + rel.Resumo());
            Mensagens.Info($"Importação concluída: {rel.TotalImportados:N0} registros importados, {rel.TotalQuarentena} em quarentena.");
        }
        catch (OperationCanceledException)
        {
            TxtProgresso.Text = "Importação cancelada. Nenhum dado foi alterado.";
        }
        catch (Exception ex)
        {
            TxtProgresso.Text = "Falha na importação. Nenhum dado foi alterado.";
            TxtRelatorio.Text = ex.Message;
            Mensagens.Erro("A importação falhou. Nenhum dado foi alterado.", ex);
        }
        finally
        {
            Alternar(false);
            _cts.Dispose();
            _cts = null;
        }
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void Alternar(bool executando)
    {
        BtnImportar.IsEnabled = !executando;
        BtnProcurar.IsEnabled = !executando;
        BtnCancelar.IsEnabled = executando;
    }
}
