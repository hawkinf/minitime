using System.Globalization;
using System.IO.Ports;

namespace MiniTime.Serial;

/// <summary>Canal de bytes (porta serial real ou simulada nos testes).</summary>
public interface ITransporteSerial : IDisposable
{
    void Escrever(ReadOnlySpan<byte> bytes);
    /// <summary>Lê bytes disponíveis; devolve 0 se nada chegou dentro do timeout.</summary>
    int Ler(Span<byte> destino, TimeSpan timeout);
    void LimparEntrada();
}

public sealed class TransporteSerialPort : ITransporteSerial
{
    private readonly SerialPort _porta;

    /// <summary>Abre a porta (N,8,1 como no programa original; velocidade vem dos parâmetros, padrão 19200).</summary>
    public TransporteSerialPort(string nomePorta, int velocidade = 19200)
    {
        _porta = new SerialPort(nomePorta, velocidade, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 200,
            WriteTimeout = 1000,
            DtrEnable = true,
            RtsEnable = true,
        };
        _porta.Open();
    }

    public static string[] PortasDisponiveis() => SerialPort.GetPortNames().OrderBy(p => p.Length).ThenBy(p => p).ToArray();

    public void Escrever(ReadOnlySpan<byte> bytes) => _porta.Write(bytes.ToArray(), 0, bytes.Length);

    public int Ler(Span<byte> destino, TimeSpan timeout)
    {
        _porta.ReadTimeout = Math.Max(1, (int)timeout.TotalMilliseconds);
        try
        {
            var buf = new byte[destino.Length];
            var n = _porta.Read(buf, 0, buf.Length);
            buf.AsSpan(0, n).CopyTo(destino);
            return n;
        }
        catch (TimeoutException) { return 0; }
        catch (InvalidOperationException ex) { throw new IOException("A porta serial foi fechada ou desconectada.", ex); }
    }

    public void LimparEntrada() => _porta.DiscardInBuffer();

    public void Dispose() { if (_porta.IsOpen) _porta.Close(); _porta.Dispose(); }
}

/// <summary>Registro de marcação coletado do relógio (texto de 28 caracteres).</summary>
public sealed record RegistroColetado(string Cartao, DateTime DataHora, int Tipo)
{
    /// <summary>
    /// Layout (posições 1-based, do decompilado de frmColetaRelogio): 1-16 cartão, 17-18 dia, 19-20 mês,
    /// 21-22 ano (2 dígitos), 23-24 hora, 25-26 minuto, 27-28 tipo.
    /// </summary>
    public static RegistroColetado? Interpretar(string texto)
    {
        if (texto.Length < 26) return null;
        static bool Num(string s) => s.All(char.IsDigit);
        var cartao = texto[..16];
        if (!Num(cartao)) return null;
        var campos = new[] { texto.Substring(16, 2), texto.Substring(18, 2), texto.Substring(20, 2), texto.Substring(22, 2), texto.Substring(24, 2) };
        if (!campos.All(Num)) return null;
        var (d, m, a, h, mi) = (int.Parse(campos[0]), int.Parse(campos[1]), int.Parse(campos[2]), int.Parse(campos[3]), int.Parse(campos[4]));
        var ano = a < 80 ? 2000 + a : 1900 + a;
        if (m is < 1 or > 12 || d < 1 || d > DateTime.DaysInMonth(ano, m) || h > 23 || mi > 59) return null;
        var tipo = texto.Length >= 28 && int.TryParse(texto.AsSpan(26, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var t) ? t : 0;
        return new RegistroColetado(cartao, new DateTime(ano, m, d, h, mi, 0), tipo);
    }
}

/// <summary>Comandos conhecidos (EXPERIMENTAL: reconstruídos do decompilado; validar com o relógio real).</summary>
public static class Comandos
{
    /// <summary>"J" — pergunta quantas marcações há para coletar (resposta 0x1F com 4 dígitos).</summary>
    public const byte PerguntaPendentes = 0x0E;
    /// <summary>"K" — confirma o registro recebido e pede o próximo.</summary>
    public const byte ConfirmaProximo = 0x0F;
    public const byte RespostaContagem = 0x1F;
}

/// <summary>Cliente de um relógio Dimep (Mini Point) ligado a uma porta serial.</summary>
public sealed class ClienteRelogio : IDisposable
{
    private readonly ITransporteSerial _t;
    private readonly ReceptorQuadros _rx;
    private readonly byte[] _buf = new byte[256];
    /// <summary>Quadros que chegaram juntos no mesmo bloco de bytes e ainda não foram entregues.</summary>
    private readonly Queue<Quadro> _pendentes = new();

    public int Endereco { get; }
    /// <summary>Hex de tudo que foi enviado/recebido (para a tela de diagnóstico).</summary>
    public event Action<string, byte[]>? Trafego;

    public ClienteRelogio(ITransporteSerial transporte, int endereco)
    {
        _t = transporte;
        Endereco = endereco;
        _rx = new ReceptorQuadros(endereco);
    }

    public void Enviar(Quadro q)
    {
        var bytes = q.Serializar();
        Trafego?.Invoke("TX", bytes);
        _t.Escrever(bytes);
    }

    public void Enviar(byte comando, params byte[] dados) => Enviar(new Quadro(Quadro.ParaBcd(Endereco), comando, dados));

    /// <summary>Espera um quadro do relógio até o timeout; null se nada válido chegou.</summary>
    public Quadro? Receber(TimeSpan timeout, CancellationToken ct = default)
    {
        var limite = DateTime.UtcNow + timeout;
        if (_pendentes.Count > 0) return _pendentes.Dequeue();
        while (DateTime.UtcNow < limite)
        {
            ct.ThrowIfCancellationRequested();
            var n = _t.Ler(_buf, TimeSpan.FromMilliseconds(100));
            if (n == 0) continue;
            Trafego?.Invoke("RX", _buf.AsSpan(0, n).ToArray());
            foreach (var q in _rx.Alimentar(_buf.AsSpan(0, n))) _pendentes.Enqueue(q);
            if (_pendentes.Count > 0) return _pendentes.Dequeue();
        }
        return null;
    }

    public Quadro? Transacionar(byte comando, byte[] dados, TimeSpan timeout, int tentativas = 3, CancellationToken ct = default)
    {
        for (var i = 0; i < tentativas; i++)
        {
            _rx.Reiniciar();
            _pendentes.Clear();
            Enviar(comando, dados);
            if (Receber(timeout, ct) is { } r) return r;
        }
        return null;
    }

    /// <summary>
    /// Coleta as marcações pendentes (EXPERIMENTAL): pergunta a contagem (0x0E), e a cada registro recebido
    /// confirma e pede o próximo (0x0F) até esgotar. Cada registro chega como texto de 28 caracteres.
    /// </summary>
    public IEnumerable<RegistroColetado> Coletar(Action<int, int>? progresso = null, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var espera = timeout ?? TimeSpan.FromSeconds(2);
        var resp = Transacionar(Comandos.PerguntaPendentes, [], espera, ct: ct)
            ?? throw new TimeoutException("O relógio não respondeu. Verifique o cabo, a porta e o endereço do relógio.");
        var total = 0;
        if (resp.Comando == Comandos.RespostaContagem && int.TryParse(System.Text.Encoding.ASCII.GetString(resp.Dados).Trim(), out var n)) total = n;
        var recebidos = 0;
        while (resp is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (resp.Dados.Length == 0) yield break;
            var texto = System.Text.Encoding.ASCII.GetString(resp.Dados);
            if (RegistroColetado.Interpretar(texto) is { } reg)
            {
                recebidos++;
                progresso?.Invoke(recebidos, Math.Max(total, recebidos));
                yield return reg;
                resp = Transacionar(Comandos.ConfirmaProximo, [], espera, ct: ct);
            }
            else if (resp.Comando == Comandos.RespostaContagem)
            {
                if (total == 0) yield break;
                resp = Transacionar(Comandos.ConfirmaProximo, [], espera, ct: ct);
            }
            else
                throw new InvalidDataException($"Resposta inesperada do relógio (comando 0x{resp.Comando:X2}, {resp.Dados.Length} byte(s)); a coleta foi interrompida após {recebidos} registro(s). Veja o log hexadecimal.");
        }
    }

    public void Dispose() => _t.Dispose();
}
