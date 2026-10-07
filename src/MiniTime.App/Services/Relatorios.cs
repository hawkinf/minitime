using System.IO;
using System.Printing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;
using MiniTime.Core.Models;

namespace MiniTime.App.Services;

public sealed record ColunaRelatorio(string Titulo, double Largura, TextAlignment Alinhamento = TextAlignment.Left);

/// <summary>Relatório tabular simples (cabeçalho da empresa + título + tabela). Base de todas as listagens.</summary>
public sealed class RelatorioTabela
{
    public required string Titulo { get; init; }
    public string? Subtitulo { get; init; }
    public required IReadOnlyList<ColunaRelatorio> Colunas { get; init; }
    public List<string[]> Linhas { get; } = [];
    /// <summary>Linhas em destaque (índices em Linhas).</summary>
    public HashSet<int> Destaques { get; } = [];
    public List<string> Rodape { get; } = [];
    public bool Paisagem { get; init; }
}

public static class Relatorios
{
    private static readonly FontFamily Fonte = new("Segoe UI");

    public static FlowDocument Montar(IEnumerable<RelatorioTabela> paginas, Parametros empresa)
    {
        var doc = new FlowDocument
        {
            FontFamily = Fonte,
            FontSize = 10.5,
            PagePadding = new Thickness(40, 36, 40, 36),
            ColumnWidth = double.PositiveInfinity,
            PageWidth = 793,   // A4 retrato
            PageHeight = 1122,
        };
        var primeira = true;
        foreach (var r in paginas)
        {
            if (!primeira) doc.Blocks.Add(new Paragraph { BreakPageBefore = true, FontSize = 1, Margin = new Thickness(0) });
            primeira = false;
            doc.Blocks.Add(Cabecalho(empresa, r));
            doc.Blocks.Add(Tabela(r));
            foreach (var linha in r.Rodape) doc.Blocks.Add(new Paragraph(new Run(linha)) { Margin = new Thickness(0, 4, 0, 0) });
        }
        if (paginas.Any(p => p.Paisagem)) { doc.PageWidth = 1122; doc.PageHeight = 793; }
        return doc;
    }

    private static Block Cabecalho(Parametros e, RelatorioTabela r)
    {
        var sec = new Section();
        sec.Blocks.Add(new Paragraph(new Run(e.NomeCliente)) { FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0) });
        if (!string.IsNullOrWhiteSpace(e.CNPJ))
            sec.Blocks.Add(new Paragraph(new Run((e.TipoEmpresa == "F" ? "CPF: " : "CNPJ: ") + e.CNPJ)) { Margin = new Thickness(0), Foreground = Brushes.DimGray });
        sec.Blocks.Add(new Paragraph(new Run(r.Titulo)) { FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
        if (r.Subtitulo is not null)
            sec.Blocks.Add(new Paragraph(new Run(r.Subtitulo)) { Margin = new Thickness(0, 2, 0, 8) });
        return sec;
    }

    private static Table Tabela(RelatorioTabela r)
    {
        var t = new Table { CellSpacing = 0, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5) };
        foreach (var c in r.Colunas) t.Columns.Add(new TableColumn { Width = new GridLength(c.Largura, GridUnitType.Star) });
        var grupo = new TableRowGroup();
        t.RowGroups.Add(grupo);

        var cab = new TableRow { Background = new SolidColorBrush(Color.FromRgb(0xE4, 0xE9, 0xF0)), FontWeight = FontWeights.SemiBold };
        foreach (var c in r.Colunas) cab.Cells.Add(Celula(c.Titulo, c.Alinhamento == TextAlignment.Left ? TextAlignment.Left : TextAlignment.Center));
        grupo.Rows.Add(cab);

        for (var i = 0; i < r.Linhas.Count; i++)
        {
            var linha = new TableRow();
            if (r.Destaques.Contains(i)) linha.FontWeight = FontWeights.Bold;
            else if (i % 2 == 1) linha.Background = new SolidColorBrush(Color.FromRgb(0xF6, 0xF8, 0xFA));
            for (var k = 0; k < r.Colunas.Count; k++)
                linha.Cells.Add(Celula(k < r.Linhas[i].Length ? r.Linhas[i][k] : "", r.Colunas[k].Alinhamento));
            grupo.Rows.Add(linha);
        }
        return t;
    }

    private static TableCell Celula(string texto, TextAlignment alinhamento)
        => new(new Paragraph(new Run(texto)) { Margin = new Thickness(0), TextAlignment = alinhamento })
        {
            Padding = new Thickness(3, 1, 3, 1),
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 0, 0, 0.5),
        };

    /// <summary>Abre uma janela de visualização com botões Imprimir e Exportar CSV.</summary>
    public static void Visualizar(string titulo, FlowDocument doc, IEnumerable<RelatorioTabela>? paraCsv = null)
    {
        var viewer = new FlowDocumentReader { Document = doc, ViewingMode = FlowDocumentReaderViewingMode.Page };
        var barra = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        var imprimir = new Button { Content = "Imprimir…", Style = (Style)Application.Current.FindResource("BotaoPrimario") };
        imprimir.Click += (_, _) => Imprimir(doc, titulo);
        barra.Children.Add(imprimir);
        if (paraCsv is not null)
        {
            var csv = new Button { Content = "Exportar CSV…" };
            csv.Click += (_, _) => ExportarCsv(titulo, paraCsv);
            barra.Children.Add(csv);
        }
        var dock = new DockPanel();
        DockPanel.SetDock(barra, Dock.Top);
        dock.Children.Add(barra);
        dock.Children.Add(viewer);
        new Window
        {
            Title = titulo, Content = dock, Width = 900, Height = 760, Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, FontFamily = Fonte,
        }.Show();
    }

    public static void Imprimir(FlowDocument doc, string titulo)
    {
        try
        {
            var dlg = new PrintDialog();
            if (dlg.ShowDialog() != true) return;
            // o documento é clonado para não alterar a visualização na tela
            var copia = new FlowDocument { FontFamily = doc.FontFamily, FontSize = doc.FontSize, PagePadding = doc.PagePadding, ColumnWidth = double.PositiveInfinity };
            using var ms = new MemoryStream();
            var range = new TextRange(doc.ContentStart, doc.ContentEnd);
            range.Save(ms, DataFormats.XamlPackage);
            new TextRange(copia.ContentStart, copia.ContentEnd).Load(ms, DataFormats.XamlPackage);
            copia.PageWidth = dlg.PrintableAreaWidth;
            copia.PageHeight = dlg.PrintableAreaHeight;
            dlg.PrintDocument(((IDocumentPaginatorSource)copia).DocumentPaginator, titulo);
        }
        catch (PrintQueueException ex)
        {
            Mensagens.Erro("Erro na impressão. Verifique a conexão da impressora e tente novamente.", ex);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            Mensagens.Erro("Não há uma impressora disponível. Instale uma impressora e tente novamente.", ex);
        }
    }

    public static void ExportarCsv(string titulo, IEnumerable<RelatorioTabela> tabelas)
    {
        var dlg = new SaveFileDialog { Title = "Exportar CSV", Filter = "CSV (*.csv)|*.csv", FileName = $"{titulo}.csv" };
        if (dlg.ShowDialog() != true) return;
        var sb = new StringBuilder();
        foreach (var t in tabelas)
        {
            sb.AppendLine(string.Join(';', t.Colunas.Select(c => Esc(c.Titulo))));
            foreach (var l in t.Linhas) sb.AppendLine(string.Join(';', l.Select(Esc)));
            sb.AppendLine();
        }
        File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
        Navegacao.Status("CSV exportado: " + dlg.FileName);
    }

    private static string Esc(string s) => s.Contains(';') || s.Contains('"') || s.Contains('\n') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
