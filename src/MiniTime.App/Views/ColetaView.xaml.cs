using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MiniTime.App.Services;
using MiniTime.Core.Util;
using MiniTime.Data;
using MiniTime.Serial;

namespace MiniTime.App.Views;

public partial class ColetaView : UserControl
{
    public sealed record Linha(string CartaoCurto, string Nome, DateTime DataHora, string Cartao);

    private readonly ObservableCollection<Linha> _linhas = [];
    private CancellationTokenSource? _cts;

    public ColetaView()
    {
        InitializeComponent();
        GridColetadas.ItemsSource = _linhas;
        var terminal = Sessao.Repos.Terminais.Todos("Endereco").FirstOrDefault();
        TxtEndereco.Text = (terminal?.Endereco ?? 1).ToString();
        TxtVelocidade.Text = Sessao.Repos.Parametros.Obter().Velocidade + " bps, 8N1";
        AtualizarPortas(terminal is null ? null : $"COM{terminal.Porta}");
    }

    private void AtualizarPortas(string? preferida = null)
    {
        var portas = TransporteSerialPort.PortasDisponiveis().ToList();
        var escolhida = preferida ?? Sessao.Settings.PortaSerial ?? CmbPorta.Text;
        CmbPorta.ItemsSource = portas;
        CmbPorta.Text = !string.IsNullOrEmpty(escolhida) ? escolhida : portas.FirstOrDefault() ?? "";
        if (portas.Count == 0) TxtStatus.Text = "Nenhuma porta serial encontrada. Conecte o adaptador USB-serial e clique em Atualizar.";
    }

    private void BtnAtualizarPortas_Click(object sender, RoutedEventArgs e) => AtualizarPortas();

    private async void BtnColetar_Click(object sender, RoutedEventArgs e)
    {
        var porta = CmbPorta.Text.Trim();
        if (porta.Length == 0) { Mensagens.Aviso("Selecione a porta serial do relógio."); return; }
        if (!int.TryParse(TxtEndereco.Text, out var endereco) || endereco is < 1 or > 99) { Mensagens.Aviso("O número do relógio deve estar entre 1 e 99."); return; }
        var velocidade = Sessao.Repos.Parametros.Obter().Velocidade;
        var nomes = Sessao.Repos.Funcionarios.Todos().ToDictionary(f => f.Codigo, f => f.Nome);

        Sessao.Settings.PortaSerial = porta;
        Sessao.Settings.Salvar();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        Alternar(true);
        _linhas.Clear();
        TxtTrafego.Clear();
        TxtStatus.Text = $"Conectando em {porta}…";
        Barra.Value = 0;
        var mostrar = ChkTrafego.IsChecked == true;

        try
        {
            var coletados = await Task.Run(() =>
            {
                var lista = new List<RegistroColetado>();
                using var cli = new ClienteRelogio(new TransporteSerialPort(porta, velocidade), endereco);
                if (mostrar)
                    cli.Trafego += (dir, bytes) => Dispatcher.BeginInvoke(() =>
                    {
                        TxtTrafego.AppendText($"{DateTime.Now:HH:mm:ss.fff} {dir} {BitConverter.ToString(bytes).Replace('-', ' ')}{Environment.NewLine}");
                        TxtTrafego.ScrollToEnd();
                    });
                foreach (var r in cli.Coletar((a, t) => Dispatcher.BeginInvoke(() =>
                         {
                             Barra.Value = t == 0 ? 0 : 100.0 * a / t;
                             TxtStatus.Text = $"Coletando… {a} de {t}";
                         }), TimeSpan.FromSeconds(3), ct))
                {
                    lista.Add(r);
                    var cartao = CodigoCartao.Normalizar(r.Cartao);
                    Dispatcher.BeginInvoke(() => _linhas.Add(new Linha(CodigoCartao.Curto(cartao), nomes.GetValueOrDefault(cartao, "(cartão não cadastrado)"), r.DataHora, cartao)));
                }
                return lista;
            }, ct);

            var res = new ColetaService(Sessao.Db).Gravar(coletados.Select(r => (r.Cartao, r.DataHora)), endereco);
            Barra.Value = 100;
            ExportarMonitoracao(coletados, endereco);
            TxtStatus.Text = coletados.Count == 0
                ? "Não há marcações novas no relógio."
                : $"Coleta concluída: {res.Gravadas} gravada(s), {res.Repetidas} repetida(s), {res.Invalidas} com data inválida.";
            Log.Info($"Coleta {porta}: {res}");
        }
        catch (OperationCanceledException) { TxtStatus.Text = "Coleta cancelada."; }
        catch (TimeoutException ex) { TxtStatus.Text = ex.Message; Mensagens.Aviso(ex.Message); }
        catch (UnauthorizedAccessException ex) { Mensagens.Erro($"A porta {porta} está em uso por outro programa ou sem permissão.", ex); TxtStatus.Text = "Porta indisponível."; }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException)
        {
            Mensagens.Erro($"Não foi possível abrir a porta {porta}. Confira o adaptador USB-serial.", ex);
            TxtStatus.Text = "Falha ao abrir a porta.";
        }
        finally
        {
            Alternar(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>Se "Gerar arquivo na coleta" estiver ligado nos parâmetros de exportação, acrescenta as marcações ao arquivo.</summary>
    private static void ExportarMonitoracao(List<RegistroColetado> coletados, int endereco)
    {
        var p = Sessao.Repos.Parametros.Obter();
        if (!p.ExpMonitoracao || string.IsNullOrWhiteSpace(p.NomeArq) || coletados.Count == 0) return;
        try
        {
            var marcacoes = coletados.Select(r => new MiniTime.Core.Models.Marcacao
            {
                Cracha = CodigoCartao.Normalizar(r.Cartao), DataHora = r.DataHora, Terminal = endereco, Tipo = MiniTime.Core.Apuracao.TipoMarcacao.Coletada,
            });
            File.AppendAllText(p.NomeArq, MiniTime.Core.Exportacao.ExportadorMarcacoes.Gerar(marcacoes, new MiniTime.Core.Exportacao.OpcoesExportacao(p.NumCartao, p.NumDigAno)), System.Text.Encoding.ASCII);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Erro("Falha ao gravar o arquivo de monitoração: " + p.NomeArq, ex);
            Mensagens.Aviso("As marcações foram gravadas no banco, mas não foi possível gravar o arquivo de monitoração em " + p.NomeArq);
        }
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void Alternar(bool coletando)
    {
        BtnColetar.IsEnabled = !coletando;
        BtnCancelar.IsEnabled = coletando;
        CmbPorta.IsEnabled = !coletando;
    }
}
