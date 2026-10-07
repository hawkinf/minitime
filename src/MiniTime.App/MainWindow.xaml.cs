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
        var nivel = Sessao.Usuario?.Nivel ?? 2; // sem usuários cadastrados o acesso é livre
        var itens = Menus.Itens().Where(i => i.Nivel <= nivel).ToList();

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

        var ajuda = new MenuItem { Header = "A_juda" };
        var sobre = new MenuItem { Header = "_Sobre o MiniTime…" };
        sobre.Click += (_, _) => Sobre.Mostrar();
        ajuda.Items.Add(sobre);
        MenuPrincipal.Items.Add(ajuda);

        var sair = new MenuItem { Header = "_Sair", InputGestureText = "Ctrl+R" };
        sair.Click += (_, _) => Close();
        MenuPrincipal.Items.Add(sair);
        InputBindings.Add(new System.Windows.Input.KeyBinding(new RelayCommand(Close), System.Windows.Input.Key.R, System.Windows.Input.ModifierKeys.Control));
        Closing += (_, e) =>
        {
            if (!Mensagens.Confirmar("Sair do Programa ?")) e.Cancel = true;
        };
    }

    private sealed class RelayCommand(Action acao) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => acao();
    }

    public void FecharTodas()
    {
        foreach (var aba in _abas.Values.ToList()) Abas.Items.Remove(aba);
        _abas.Clear();
        AtualizarBoasVindas();
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
        AtualizarBoasVindas();
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

    /// <summary>Abre uma tela pela chave do menu (usado por --abrir=chave na linha de comando).</summary>
    public void AbrirPorChave(string chave)
    {
        var e = Menus.Itens().FirstOrDefault(i => i.Chave == chave);
        if (e is not null) AbrirAba(e.Titulo.Replace("_", "").TrimEnd('…'), e.Chave, e.Criar);
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
