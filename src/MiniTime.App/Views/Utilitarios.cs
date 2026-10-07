using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using MiniTime.App.Controls;
using MiniTime.App.Services;
using MiniTime.Core.Apuracao;
using MiniTime.Core.Exportacao;
using MiniTime.Core.Models;
using MiniTime.Data;

namespace MiniTime.App.Views;

internal static class Ui
{
    public static StackPanel Linha(params UIElement[] itens)
    {
        var l = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        foreach (var i in itens) l.Children.Add(i);
        return l;
    }

    public static TextBlock Rotulo(string t, double largura = 0)
        => new() { Text = t, Style = (Style)Application.Current.FindResource("Rotulo"), Width = largura > 0 ? largura : double.NaN };

    public static Button Botao(string t, RoutedEventHandler h, bool primario = false)
    {
        var b = new Button { Content = t };
        if (primario) b.Style = (Style)Application.Current.FindResource("BotaoPrimario");
        b.Click += h;
        return b;
    }

    public static TextBlock Titulo(string t) => new() { Text = t, Style = (Style)Application.Current.FindResource("Titulo") };
}

/// <summary>Backup, restauração, reorganização e reparo do banco de dados.</summary>
public sealed class ManutencaoView : UserControl
{
    private readonly TextBox _log = new() { IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };

    public ManutencaoView()
    {
        var raiz = new DockPanel { Margin = new Thickness(16) };
        var topo = new StackPanel();
        topo.Children.Add(Ui.Titulo("Backup e manutenção do banco"));
        topo.Children.Add(new TextBlock { Text = "Banco em uso: " + Sessao.Settings.CaminhoBanco, Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
        topo.Children.Add(Ui.Linha(
            Ui.Botao("Fazer backup…", (_, _) => Backup(), true),
            Ui.Botao("Restaurar backup…", (_, _) => Restaurar()),
            Ui.Botao("Reorganizar", (_, _) => Reorganizar()),
            Ui.Botao("Reparar", (_, _) => Reparar())));
        DockPanel.SetDock(topo, Dock.Top);
        raiz.Children.Add(topo);
        raiz.Children.Add(_log);
        Content = raiz;
    }

    private void Escrever(string t) { _log.AppendText($"{DateTime.Now:HH:mm:ss}  {t}{Environment.NewLine}"); _log.ScrollToEnd(); }

    private void Backup()
    {
        var dlg = new SaveFileDialog { Title = "Salvar backup", Filter = "Banco MiniTime (*.db)|*.db", FileName = $"MiniTime-backup-{DateTime.Now:yyyyMMdd-HHmm}.db" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            new ManutencaoService(Sessao.Db).Backup(dlg.FileName);
            Escrever($"Backup gravado em {dlg.FileName} ({new FileInfo(dlg.FileName).Length / 1024:N0} KB).");
        }
        catch (Exception ex) { Mensagens.Erro("Não foi possível fazer o backup.", ex); }
    }

    private void Restaurar()
    {
        var dlg = new OpenFileDialog { Title = "Escolha o backup", Filter = "Banco MiniTime (*.db)|*.db|Todos|*.*" };
        if (dlg.ShowDialog() != true) return;
        var erro = ManutencaoService.ValidarArquivoBackup(dlg.FileName);
        if (erro is not null) { Mensagens.Aviso(erro); return; }
        if (!Mensagens.Confirmar("Todos os dados atuais serão substituídos pelos do backup (uma cópia de segurança do banco atual é guardada antes). Continuar?")) return;
        try
        {
            var seguranca = ManutencaoService.Restaurar(dlg.FileName, Sessao.Settings.CaminhoBanco);
            Sessao.Abrir(Sessao.Settings.CaminhoBanco);
            Escrever($"Backup restaurado. Cópia do banco anterior: {seguranca}");
            Mensagens.Info("Backup restaurado. As telas abertas serão fechadas para recarregar os dados.");
            Navegacao.FecharTodas();
        }
        catch (Exception ex) { Mensagens.Erro("Não foi possível restaurar o backup.", ex); }
    }

    private void Reorganizar()
    {
        try
        {
            var antes = new FileInfo(Sessao.Settings.CaminhoBanco).Length;
            new ManutencaoService(Sessao.Db).Reorganizar();
            SqliteConnection.ClearAllPools();
            var depois = new FileInfo(Sessao.Settings.CaminhoBanco).Length;
            Escrever($"Reorganização efetuada. Tamanho: {antes / 1024:N0} KB → {depois / 1024:N0} KB.");
        }
        catch (Exception ex) { Mensagens.Erro("Não foi possível efetuar a reorganização. Verifique se o arquivo do banco está aberto por outro programa.", ex); }
    }

    private void Reparar()
    {
        try
        {
            var r = new ManutencaoService(Sessao.Db).Reparar();
            Escrever(r.Integro ? "Banco íntegro." : "Foram encontrados problemas:");
            foreach (var c in r.Correcoes) Escrever("  corrigido: " + c);
            foreach (var p in r.Problemas) Escrever("  atenção: " + p);
            if (r.Correcoes.Count == 0 && r.Problemas.Count == 0) Escrever("Nada a corrigir.");
        }
        catch (Exception ex) { Mensagens.Erro("Não foi possível reparar o banco.", ex); }
    }
}

/// <summary>Parâmetros de exportação (dígitos do cartão e do ano, arquivo de monitoração).</summary>
public sealed class ParametrosExportacaoView : EdicaoUnicaView<Parametros>
{
    public ParametrosExportacaoView() => Montar();

    protected override string Titulo => "Parâmetros de exportação";
    protected override string? Descricao => "Layout do arquivo de marcações (MOVIMENT.TXT) usado pela exportação e, opcionalmente, gerado durante a coleta.";

    protected override IReadOnlyList<Campo> Campos =>
    [
        new() { Rotulo = "Nº de dígitos do cartão", Propriedade = nameof(Parametros.NumCartao), Tipo = TipoCampo.Escolha, Largura = 100,
                Opcoes = Enumerable.Range(4, 11).Select(i => (i, i.ToString())).ToList(),
                Dica = "Se o cartão tiver menos dígitos, o sistema completa com zeros à esquerda." },
        new() { Rotulo = "Nº de dígitos do ano", Propriedade = nameof(Parametros.NumDigAno), Tipo = TipoCampo.Escolha, Largura = 100, Opcoes = [(2, "2"), (4, "4")] },
        new() { Rotulo = "Gerar arquivo na coleta", Propriedade = nameof(Parametros.ExpMonitoracao), Tipo = TipoCampo.Booleano },
        new() { Rotulo = "Local e nome do arquivo", Propriedade = nameof(Parametros.NomeArq), MaxLength = 90, Largura = 420 },
    ];

    protected override Parametros Carregar() => Sessao.Repos.Parametros.Obter();
    protected override void Salvar(Parametros e) => Sessao.Repos.Parametros.Salvar(e);

    protected override string? Validar(Parametros e)
        => e.ExpMonitoracao && string.IsNullOrWhiteSpace(e.NomeArq) ? "Informe o local e o nome do arquivo de monitoração." : null;
}

/// <summary>Exporta as marcações do período (MOVIMENT.TXT).</summary>
public sealed class ExportacaoView : UserControl
{
    private readonly DatePicker _ini = new() { Width = 160 };
    private readonly DatePicker _fim = new() { Width = 160 };
    private readonly TextBox _arquivo = new() { Width = 420 };
    private readonly CheckBox _noturnos = new() { Content = "Somente funcionários com horário noturno" };
    private readonly TextBlock _resumo = new() { Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap };

    public ExportacaoView()
    {
        var p = Sessao.Repos.Parametros.Obter();
        var (ini, fim) = ApuracaoService.PeriodoPadrao(DateTime.Today, p.DiaFechamento);
        _ini.SelectedDate = ini;
        _fim.SelectedDate = fim;
        _arquivo.Text = string.IsNullOrWhiteSpace(p.NomeArq) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MOVIMENT.TXT") : p.NomeArq;

        var raiz = new StackPanel { Margin = new Thickness(16) };
        raiz.Children.Add(Ui.Titulo("Exportação de marcações"));
        raiz.Children.Add(Ui.Linha(Ui.Rotulo("Período:", 70), _ini, new TextBlock { Text = " a ", VerticalAlignment = VerticalAlignment.Center }, _fim));
        raiz.Children.Add(Ui.Linha(Ui.Rotulo("Arquivo:", 70), _arquivo, Ui.Botao("Procurar…", (_, _) => Procurar())));
        raiz.Children.Add(_noturnos);
        raiz.Children.Add(new TextBlock { Text = $"Layout: cartão com {p.NumCartao} dígitos, ano com {p.NumDigAno} dígitos (altere em Utilitários → Parâmetros de exportação).", Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 8, 0, 8) });
        raiz.Children.Add(Ui.Botao("Exportar", (_, _) => Exportar(), true));
        raiz.Children.Add(_resumo);
        Content = raiz;
    }

    private void Procurar()
    {
        var dlg = new SaveFileDialog { Title = "Arquivo de exportação", Filter = "Texto (*.txt)|*.txt|Todos|*.*", FileName = Path.GetFileName(_arquivo.Text) };
        if (dlg.ShowDialog() == true) _arquivo.Text = dlg.FileName;
    }

    private void Exportar()
    {
        if (_ini.SelectedDate is not { } ini || _fim.SelectedDate is not { } fim || fim < ini) { Mensagens.Aviso("Período inválido."); return; }
        var destino = _arquivo.Text.Trim();
        var dir = Path.GetDirectoryName(destino);
        if (string.IsNullOrEmpty(destino) || string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { Mensagens.Aviso("O diretório do arquivo não existe."); return; }
        if (File.Exists(destino) && !Mensagens.Confirmar("O arquivo já existe e será substituído. Continuar?")) return;
        try
        {
            var p = Sessao.Repos.Parametros.Obter();
            var marcacoes = Sessao.Repos.Marcacoes.Periodo(ini.Date, fim.Date.AddDays(1).AddSeconds(-1));
            var noturnos = Sessao.Repos.Funcionarios.Todos().Where(f => f.HorarioNoturno).Select(f => f.Codigo).ToHashSet();
            var txt = ExportadorMarcacoes.Gerar(marcacoes, new OpcoesExportacao(p.NumCartao, p.NumDigAno, _noturnos.IsChecked == true), noturnos);
            if (txt.Length == 0) { _resumo.Text = "Nenhum registro no período."; Mensagens.Aviso("Nenhum registro no período."); return; }
            File.WriteAllText(destino, txt, Encoding.ASCII);
            p.NomeArq = destino;
            Sessao.Repos.Parametros.Salvar(p);
            var n = txt.Count(c => c == '\n');
            _resumo.Text = $"Exportação concluída: {n:N0} marcação(ões) gravadas em {destino}.";
            Log.Info(_resumo.Text);
        }
        catch (Exception ex) { Mensagens.Erro("Não foi possível exportar o arquivo.", ex); }
    }
}

/// <summary>Importa marcações de um arquivo no layout do MOVIMENT.TXT (ex.: pen drive do relógio).</summary>
public sealed class ImportacaoArquivoView : UserControl
{
    private readonly TextBox _arquivo = new() { Width = 420 };
    private readonly TextBox _log = new() { IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MinHeight = 160 };

    public ImportacaoArquivoView()
    {
        var p = Sessao.Repos.Parametros.Obter();
        var raiz = new DockPanel { Margin = new Thickness(16) };
        var topo = new StackPanel();
        topo.Children.Add(Ui.Titulo("Importar marcações de arquivo"));
        topo.Children.Add(new TextBlock { Text = $"O arquivo deve ter o layout do MOVIMENT.TXT: cartão ({p.NumCartao} dígitos), data (ano com {p.NumDigAno} dígitos), hora, tipo, \"00\" e número do relógio. Marcações já existentes são ignoradas.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
        topo.Children.Add(Ui.Linha(Ui.Rotulo("Arquivo:", 70), _arquivo, Ui.Botao("Procurar…", (_, _) => Procurar())));
        topo.Children.Add(Ui.Botao("Importar", (_, _) => Importar(), true));
        DockPanel.SetDock(topo, Dock.Top);
        raiz.Children.Add(topo);
        raiz.Children.Add(_log);
        Content = raiz;
    }

    private void Procurar()
    {
        var dlg = new OpenFileDialog { Title = "Arquivo de marcações", Filter = "Texto (*.txt)|*.txt|Todos|*.*" };
        if (dlg.ShowDialog() == true) _arquivo.Text = dlg.FileName;
    }

    private void Importar()
    {
        if (!File.Exists(_arquivo.Text)) { Mensagens.Aviso("Selecione um arquivo existente."); return; }
        try
        {
            var p = Sessao.Repos.Parametros.Obter();
            var lido = ImportadorArquivo.Ler(File.ReadLines(_arquivo.Text), p.NumCartao, p.NumDigAno);
            var validas = lido.Linhas.Where(l => l.Tipo is 7 or 0).ToList();
            var svc = new ColetaService(Sessao.Db);
            var gravadas = 0; var repetidas = 0; var invalidas = lido.Invalidas.Count;
            foreach (var grupo in validas.GroupBy(l => l.Relogio))
            {
                var r = svc.Gravar(grupo.Select(l => (l.Cartao, l.DataHora)), grupo.Key);
                gravadas += r.Gravadas; repetidas += r.Repetidas; invalidas += r.Invalidas;
            }
            _log.Clear();
            _log.AppendText($"Linhas lidas: {lido.Linhas.Count + lido.Invalidas.Count}{Environment.NewLine}");
            _log.AppendText($"Marcações gravadas: {gravadas}{Environment.NewLine}Repetidas (ignoradas): {repetidas}{Environment.NewLine}Inválidas: {invalidas}{Environment.NewLine}");
            foreach (var (n, t, m) in lido.Invalidas.Take(50)) _log.AppendText($"  linha {n}: {m} → {t}{Environment.NewLine}");
            Log.Info($"Importação de arquivo {_arquivo.Text}: {gravadas} gravadas, {repetidas} repetidas, {invalidas} inválidas");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Mensagens.Erro("Não foi possível ler o arquivo.", ex);
        }
    }
}
