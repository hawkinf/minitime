namespace MiniTime.Serial;

/// <summary>
/// Quadro do protocolo serial dos relógios Dimep (reconstruído do código nativo do MiniTime.exe,
/// rotinas Geral.Recepcao e frmComunicacao.Checa_Msg).
///
/// Binário (início 0xFE):  FE | endereço (BCD) | comando | tamanho | dados[tamanho] | checksum | F0
/// ASCII   (início 0xFD):  FD | endereço (2 dígitos '0'-'9') | cada byte seguinte como 2 caracteres
///                         '0'..'?' (nibble = caractere &amp; 0x0F, alto primeiro) | F0
/// checksum = (XOR de endereço, comando, tamanho e dados) &amp; 0x7F.
/// Ao enviar, o PC antecede o quadro binário com AA FF FF.
/// </summary>
public sealed record Quadro(byte Endereco, byte Comando, byte[] Dados, bool Ascii = false)
{
    public const byte InicioBinario = 0xFE;
    public const byte InicioAscii = 0xFD;
    public const byte Fim = 0xF0;
    public static readonly byte[] Preambulo = [0xAA, 0xFF, 0xFF];

    /// <summary>Número decimal do relógio (1..99) → byte BCD (12 → 0x12).</summary>
    public static byte ParaBcd(int n) => (byte)(n / 10 * 16 + n % 10);

    public static int DeBcd(byte b) => (b >> 4) * 10 + (b & 0x0F);

    public static byte Checksum(byte endereco, byte comando, ReadOnlySpan<byte> dados)
    {
        var x = endereco ^ comando ^ dados.Length;
        foreach (var b in dados) x ^= b;
        return (byte)(x & 0x7F);
    }

    /// <summary>Bytes para enviar ao relógio (com preâmbulo).</summary>
    public byte[] Serializar(bool comPreambulo = true)
    {
        if (Dados.Length > 255) throw new ArgumentException("Dados do quadro excedem 255 bytes.");
        var lista = new List<byte>();
        if (comPreambulo) lista.AddRange(Preambulo);
        var chk = Checksum(Endereco, Comando, Dados);
        if (!Ascii)
        {
            lista.Add(InicioBinario);
            lista.Add(Endereco);
            lista.Add(Comando);
            lista.Add((byte)Dados.Length);
            lista.AddRange(Dados);
            lista.Add(chk);
        }
        else
        {
            lista.Add(InicioAscii);
            lista.Add((byte)('0' + (Endereco >> 4)));
            lista.Add((byte)('0' + (Endereco & 0x0F)));
            void Byte(byte b) { lista.Add((byte)(0x30 | (b >> 4))); lista.Add((byte)(0x30 | (b & 0x0F))); }
            Byte(Comando);
            Byte((byte)Dados.Length);
            foreach (var d in Dados) Byte(d);
            Byte(chk);
        }
        lista.Add(Fim);
        return [.. lista];
    }
}

/// <summary>Montador incremental de quadros recebidos (máquina de estados do Geral.Recepcao).</summary>
public sealed class ReceptorQuadros
{
    private enum Estado { Inicio, Endereco, Comando, Tamanho, Dados, Checksum, Fim, AsciiEnd1, AsciiEnd2, AsciiByteHi, AsciiByteLo }

    private readonly int? _enderecoEsperado;
    private Estado _e = Estado.Inicio;
    private bool _ascii;
    private byte _endereco, _comando, _tamanho, _chkCalculado;
    private readonly List<byte> _dados = [];
    private int _hi;
    private int _faseAscii; // 0 comando, 1 tamanho, 2 dados, 3 checksum

    /// <summary>Quadros descartados por checksum inválido (diagnóstico).</summary>
    public int ErrosChecksum { get; private set; }

    /// <param name="enderecoEsperado">Número decimal do relógio; quadros de outros endereços são descartados. null aceita todos.</param>
    public ReceptorQuadros(int? enderecoEsperado = null) => _enderecoEsperado = enderecoEsperado;

    public void Reiniciar() { _e = Estado.Inicio; _dados.Clear(); }

    /// <summary>Alimenta um byte; devolve o quadro quando completo.</summary>
    public Quadro? Alimentar(byte b)
    {
        switch (_e)
        {
            case Estado.Inicio:
                if (b == Quadro.InicioBinario) { _ascii = false; _e = Estado.Endereco; }
                else if (b == Quadro.InicioAscii) { _ascii = true; _e = Estado.AsciiEnd1; }
                return null;

            // ---- binário
            case Estado.Endereco:
                if (!EnderecoOk(b)) { Reiniciar(); return null; }
                _endereco = b; _chkCalculado = b; _dados.Clear(); _e = Estado.Comando; return null;
            case Estado.Comando:
                _comando = b; _chkCalculado ^= b; _e = Estado.Tamanho; return null;
            case Estado.Tamanho:
                _tamanho = b; _chkCalculado ^= b; _e = b == 0 ? Estado.Checksum : Estado.Dados; return null;
            case Estado.Dados:
                _dados.Add(b); _chkCalculado ^= b;
                if (_dados.Count == _tamanho) _e = Estado.Checksum;
                return null;
            case Estado.Checksum:
                if (b != (_chkCalculado & 0x7F)) { ErrosChecksum++; Reiniciar(); return null; }
                _e = Estado.Fim; return null;
            case Estado.Fim:
                var ok = b == Quadro.Fim;
                _e = Estado.Inicio;
                return ok ? new Quadro(_endereco, _comando, [.. _dados], _ascii) : null;

            // ---- ASCII
            case Estado.AsciiEnd1:
                if (b is < (byte)'0' or > (byte)'9') { Reiniciar(); return null; }
                _hi = (b & 0x0F) * 16; _e = Estado.AsciiEnd2; return null;
            case Estado.AsciiEnd2:
                if (b is < (byte)'0' or > (byte)'9') { Reiniciar(); return null; }
                var end = _hi + (b & 0x0F);
                if (!EnderecoOk((byte)end)) { Reiniciar(); return null; }
                _endereco = (byte)end; _chkCalculado = _endereco; _dados.Clear(); _faseAscii = 0; _e = Estado.AsciiByteHi; return null;
            case Estado.AsciiByteHi:
                if (_faseAscii == 4)
                {
                    var fimOk = b == Quadro.Fim;
                    _e = Estado.Inicio;
                    return fimOk ? new Quadro(_endereco, _comando, [.. _dados], true) : null;
                }
                if (b is < 0x30 or > 0x3F) { Reiniciar(); return null; }
                _hi = (b & 0x0F) * 16; _e = Estado.AsciiByteLo; return null;
            case Estado.AsciiByteLo:
            {
                if (b is < 0x30 or > 0x3F) { Reiniciar(); return null; }
                var v = (byte)(_hi + (b & 0x0F));
                _e = Estado.AsciiByteHi;
                switch (_faseAscii)
                {
                    case 0: _comando = v; _chkCalculado ^= v; _faseAscii = 1; break;
                    case 1:
                        _tamanho = v; _chkCalculado ^= v; _faseAscii = v == 0 ? 3 : 2; break;
                    case 2:
                        _dados.Add(v); _chkCalculado ^= v;
                        if (_dados.Count == _tamanho) _faseAscii = 3;
                        break;
                    case 3:
                        if (v != (_chkCalculado & 0x7F)) { ErrosChecksum++; Reiniciar(); return null; }
                        _faseAscii = 4; break;
                }
                return null;
            }
        }
        return null;
    }

    public IEnumerable<Quadro> Alimentar(ReadOnlySpan<byte> bytes)
    {
        var lista = new List<Quadro>();
        foreach (var b in bytes) if (Alimentar(b) is { } q) lista.Add(q);
        return lista;
    }

    private bool EnderecoOk(byte b) => _enderecoEsperado is not { } n || b == Quadro.ParaBcd(n);
}
