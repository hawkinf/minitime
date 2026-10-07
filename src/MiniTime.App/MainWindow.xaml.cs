using System.Windows;
using System.Windows.Controls;
using MiniTime.App.Services;
using MiniTime.App.Views;

namespace MiniTime.App;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, TabItem> _abas = new();

    public MainWindow()
    {
        InitializeComponent();
        Navegacao.Janela = this;
        MontarMenu();
        AtualizarBoasVindas();
        TxtBanco.Text = Sessao.Settings.CaminhoBanco;
        Abas.Items.CurrentChanged += (_, _) => AtualizarBoasVindas();
    }

    private void MontarMenu()
    {
        var itens = Menus.Itens();

        foreach (var grupo in itens.GroupBy(i => i.Grupo))
        {
            var topo = new MenuItem { Header = grupo.Key };
            foreach (var e in grupo)
            {
                var mi = new MenuItem { Header = e.Titulo };
                mi.Click += (_, _) => AbrirAba(e.Titulo.Replace("_", "").TrimEnd('…'), e.Chave, e.Criar);
                topo.Items.Add(mi);
            }
            MenuPrincipal.Items.Add(topo);
        }
    }

    public void AbrirAba(string titulo, string chave, Func<UserControl> criar)
    {
        if (!_abas.TryGetValue(chave, out var aba) || !Abas.Items.Contains(aba))
        {
            UserControl view;
            try { view = criar(); }
            catch (Exception ex)
            {
                Log.Erro($"Falha ao abrir '{titulo}'", ex);
                Mensagens.Erro("Não foi possível abrir esta tela.", ex);
                return;
            }
            aba = new TabItem { Content = view, Header = Cabecalho(titulo, chave) };
            _abas[chave] = aba;
            Abas.Items.Add(aba);
        }
        Abas.SelectedItem = aba;
    }

    private UIElement Cabecalho(string titulo, string chave)
    {
        var fechar = new Button { Content = "✕", Padding = new Thickness(4, 0, 4, 0), MinWidth = 0, Margin = new Thickness(8, 0, 0, 0), BorderThickness = new Thickness(0), Background = System.Windows.Media.Brushes.Transparent };
        fechar.Click += (_, _) => FecharAba(chave);
        var p = new StackPanel { Orientation = Orientation.Horizontal };
        p.Children.Add(new TextBlock { Text = titulo, VerticalAlignment = VerticalAlignment.Center });
        p.Children.Add(fechar);
        return p;
    }

    public void FecharAba(string chave)
    {
        if (_abas.Remove(chave, out var aba)) Abas.Items.Remove(aba);
        AtualizarBoasVindas();
    }

    public void Status(string texto) => TxtStatus.Text = texto;

    private void AtualizarBoasVindas()
    {
        PainelBoasVindas.Visibility = Abas.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var vazio = Sessao.Repos.Funcionarios.Contar() == 0;
        TxtBoasVindas.Text = vazio
            ? "O banco de dados está vazio. Importe o DIMEP.MDB do programa antigo para começar, ou cadastre os dados pelo menu."
            : "Use o menu acima para abrir as telas.";
        BtnImportarInicial.Visibility = vazio ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnImportarInicial_Click(object sender, RoutedEventArgs e)
        => AbrirAba("Importar MDB", "importar", () => new ImportacaoMdbView());
}
