using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using MiniTime.App.Services;
using MiniTime.Core.Models;
using MiniTime.Core.Seguranca;

namespace MiniTime.App.Views;

/// <summary>Cadastro de usuários e senhas (menu Arquivos → Senhas).</summary>
public sealed class SenhasView : UserControl
{
    private readonly DataGrid _grid = new() { AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single, Height = 260, CanUserAddRows = false, HeadersVisibility = DataGridHeadersVisibility.Column, MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left };

    private static readonly string[] Niveis = ["Consulta", "Completo", "Administrador"];

    public SenhasView()
    {
        _grid.Columns.Add(new DataGridTextColumn { Header = "Usuário", Binding = new System.Windows.Data.Binding(nameof(Usuario.Nome)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Nível", Binding = new System.Windows.Data.Binding(nameof(Usuario.Nivel)) { Converter = new NivelConverter() }, Width = 140 });

        var raiz = new StackPanel { Margin = new Thickness(16) };
        raiz.Children.Add(Ui.Titulo("Senhas e usuários"));
        raiz.Children.Add(new TextBlock
        {
            Text = "Com ao menos um usuário cadastrado, o programa pede usuário e senha ao abrir. Consulta: só relatórios. Completo: cadastros, comunicação e apuração. Administrador: também usuários, backup e utilitários. Sem usuários o acesso é livre.",
            TextWrapping = TextWrapping.Wrap, MaxWidth = 620, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 10),
        });
        raiz.Children.Add(_grid);
        raiz.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0),
            Children = { Ui.Botao("Novo usuário…", (_, _) => Novo(), true), Ui.Botao("Alterar senha…", (_, _) => Alterar()), Ui.Botao("Nível…", (_, _) => MudarNivel()), Ui.Botao("Excluir", (_, _) => Excluir()) },
        });
        Content = raiz;
        Recarregar();
    }

    private sealed class NivelConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object? v, Type t, object? p, System.Globalization.CultureInfo c) => v is int n && n is >= 0 and <= 2 ? Niveis[n] : "?";
        public object ConvertBack(object? v, Type t, object? p, System.Globalization.CultureInfo c) => throw new NotSupportedException();
    }

    private void Recarregar() => _grid.ItemsSource = Sessao.Repos.Usuarios.Todos("Nome");

    private Usuario? Selecionado => _grid.SelectedItem as Usuario;

    private static int AdministradoresRestantes(string? excluindo = null)
        => Sessao.Repos.Usuarios.Todos().Count(u => u.Nivel == 2 && u.Nome != excluindo);

    private void Novo()
    {
        if (Dialogos_Usuario.Pedir("Novo usuário", pedirNome: true, pedirNivel: true) is not { } r) return;
        if (Sessao.Repos.Usuarios.Existe(r.Nome)) { Mensagens.Aviso("Já existe um usuário com este nome."); return; }
        // o primeiro usuário precisa ser administrador, senão ninguém conseguiria gerenciar os demais
        var nivel = Sessao.Repos.Usuarios.Contar() == 0 ? 2 : r.Nivel;
        Sessao.Repos.Usuarios.Inserir(new Usuario { Nome = r.Nome, SenhaHash = Senha.Hash(r.Senha), Nivel = nivel });
        Recarregar();
    }

    private void Alterar()
    {
        if (Selecionado is not { } u) { Mensagens.Aviso("Selecione um usuário."); return; }
        if (Dialogos_Usuario.Pedir($"Nova senha de {u.Nome}", pedirNome: false, pedirNivel: false) is not { } r) return;
        u.SenhaHash = Senha.Hash(r.Senha);
        Sessao.Repos.Usuarios.Atualizar(u);
        Mensagens.Info("Senha alterada.");
    }

    private void MudarNivel()
    {
        if (Selecionado is not { } u) { Mensagens.Aviso("Selecione um usuário."); return; }
        var atual = u.Nivel;
        var proximo = (atual + 1) % 3;
        if (atual == 2 && AdministradoresRestantes(u.Nome) == 0) { Mensagens.Aviso("É preciso manter ao menos um administrador."); return; }
        if (!Mensagens.Confirmar($"Mudar o nível de {u.Nome} de {Niveis[atual]} para {Niveis[proximo]}?")) return;
        u.Nivel = proximo;
        Sessao.Repos.Usuarios.Atualizar(u);
        Recarregar();
    }

    private void Excluir()
    {
        if (Selecionado is not { } u) { Mensagens.Aviso("Selecione um usuário."); return; }
        if (u.Nivel == 2 && AdministradoresRestantes(u.Nome) == 0 && Sessao.Repos.Usuarios.Contar() > 1)
        { Mensagens.Aviso("É preciso manter ao menos um administrador."); return; }
        if (!Mensagens.Confirmar($"Confirma a exclusão do usuário {u.Nome}?")) return;
        Sessao.Repos.Usuarios.Excluir(u.Nome);
        Recarregar();
    }
}

internal static class Dialogos_Usuario
{
    public sealed record Resposta(string Nome, string Senha, int Nivel);

    public static Resposta? Pedir(string titulo, bool pedirNome, bool pedirNivel)
    {
        var w = new Window
        {
            Title = titulo, Width = 380, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 13,
        };
        var p = new StackPanel { Margin = new Thickness(16) };
        var nome = new TextBox { Padding = new Thickness(4, 3, 4, 3), MaxLength = 20, Margin = new Thickness(0, 2, 0, 8) };
        var s1 = new PasswordBox { Padding = new Thickness(4, 3, 4, 3), MaxLength = 20, Margin = new Thickness(0, 2, 0, 8) };
        var s2 = new PasswordBox { Padding = new Thickness(4, 3, 4, 3), MaxLength = 20, Margin = new Thickness(0, 2, 0, 8) };
        var nivel = new ComboBox { ItemsSource = new[] { "Consulta", "Completo", "Administrador" }, SelectedIndex = 1, Margin = new Thickness(0, 2, 0, 8) };
        if (pedirNome) { p.Children.Add(new TextBlock { Text = "Usuário:" }); p.Children.Add(nome); }
        p.Children.Add(new TextBlock { Text = "Senha:" }); p.Children.Add(s1);
        p.Children.Add(new TextBlock { Text = "Repita a senha:" }); p.Children.Add(s2);
        if (pedirNivel) { p.Children.Add(new TextBlock { Text = "Nível de acesso:" }); p.Children.Add(nivel); }
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(10, 4, 10, 4) };
        var cancelar = new Button { Content = "Cancelar", IsCancel = true, MinWidth = 80, Padding = new Thickness(10, 4, 10, 4) };
        ok.Click += (_, _) =>
        {
            if (pedirNome && string.IsNullOrWhiteSpace(nome.Text)) { Mensagens.Aviso("Informe o nome do usuário."); return; }
            if (s1.Password.Length < 4) { Mensagens.Aviso("A senha deve ter pelo menos 4 caracteres."); return; }
            if (s1.Password != s2.Password) { Mensagens.Aviso("As senhas não conferem."); return; }
            w.DialogResult = true;
        };
        p.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0), Children = { ok, cancelar } });
        w.Content = p;
        return w.ShowDialog() == true ? new Resposta(nome.Text.Trim(), s1.Password, nivel.SelectedIndex) : null;
    }
}

/// <summary>Tela de acesso: mostrada ao abrir quando há usuários cadastrados.</summary>
public static class Login
{
    public static Usuario? Pedir()
    {
        var w = new Window
        {
            Title = "MiniTime - Acesso", Width = 340, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen, FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 13,
        };
        var p = new StackPanel { Margin = new Thickness(18) };
        var nome = new TextBox { Padding = new Thickness(4, 3, 4, 3), Margin = new Thickness(0, 2, 0, 8), Text = Sessao.Repos.Parametros.Obter().UltUsuario ?? "" };
        var senha = new PasswordBox { Padding = new Thickness(4, 3, 4, 3), Margin = new Thickness(0, 2, 0, 12) };
        p.Children.Add(new TextBlock { Text = "MiniTime", FontSize = 22, FontWeight = FontWeights.Light, Margin = new Thickness(0, 0, 0, 10) });
        p.Children.Add(new TextBlock { Text = "Usuário:" }); p.Children.Add(nome);
        p.Children.Add(new TextBlock { Text = "Senha:" }); p.Children.Add(senha);
        Usuario? entrou = null;
        var tentativas = 0;
        var ok = new Button { Content = "Entrar", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(10, 4, 10, 4) };
        var sair = new Button { Content = "Sair", IsCancel = true, MinWidth = 80, Padding = new Thickness(10, 4, 10, 4) };
        ok.Click += (_, _) =>
        {
            var u = Sessao.Repos.Usuarios.Obter(nome.Text.Trim());
            if (u is not null && Senha.Confere(senha.Password, u.SenhaHash)) { entrou = u; w.DialogResult = true; return; }
            senha.Clear();
            if (++tentativas >= 3) { Mensagens.Aviso("Número máximo de tentativas excedido."); w.DialogResult = false; return; }
            Mensagens.Aviso("Usuário ou senha inválidos.");
        };
        p.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, sair } });
        w.Content = p;
        w.Loaded += (_, _) => (string.IsNullOrEmpty(nome.Text) ? (Control)nome : senha).Focus();
        if (w.ShowDialog() != true || entrou is null) return null;
        var par = Sessao.Repos.Parametros.Obter();
        par.UltUsuario = entrou.Nome;
        Sessao.Repos.Parametros.Salvar(par);
        return entrou;
    }
}

public static class Sobre
{
    public static void Mostrar()
    {
        var versao = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";
        var p = new StackPanel { Margin = new Thickness(24) };
        p.Children.Add(new TextBlock { Text = "MiniTime", FontSize = 30, FontWeight = FontWeights.Light, Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("Primaria") });
        p.Children.Add(new TextBlock { Text = $"Versão {versao}", Margin = new Thickness(0, 0, 0, 10), Foreground = System.Windows.Media.Brushes.Gray });
        p.Children.Add(new TextBlock { Text = "Controle de cartão de ponto — reescrita em C# / WPF com banco SQLite do MiniTime original.", TextWrapping = TextWrapping.Wrap, MaxWidth = 360 });
        p.Children.Add(new TextBlock { Text = "https://github.com/hawkinf/minitime", Margin = new Thickness(0, 10, 0, 0), Foreground = System.Windows.Media.Brushes.RoyalBlue });
        var ok = new Button { Content = "OK", IsDefault = true, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 80, Margin = new Thickness(0, 16, 0, 0), Padding = new Thickness(10, 4, 10, 4) };
        p.Children.Add(ok);
        var w = new Window { Title = "Sobre o MiniTime", Content = p, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, Owner = Application.Current.MainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, FontFamily = new System.Windows.Media.FontFamily("Segoe UI") };
        ok.Click += (_, _) => w.Close();
        w.ShowDialog();
    }
}
