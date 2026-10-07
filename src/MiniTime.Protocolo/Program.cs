using System.IO.Ports;
using MiniTime.Serial;

// Ferramenta de engenharia reversa do protocolo do relógio (não faz parte do app).
//   minitime-protocolo decodificar <captura.txt> [--resumo]
//   minitime-protocolo monitorar <COM-PC→relógio> [<COM-relógio→PC>] [--vel 19200] [--saida log.txt]
return args switch
{
    ["decodificar", var arquivo, ..] => Decodificar(arquivo, args.Contains("--resumo")),
    ["monitorar", ..] => Monitorar(args[1..]),
    _ => Ajuda(),
};

static int Ajuda()
{
    Console.WriteLine("""
        minitime-protocolo decodificar <captura.txt> [--resumo]
            Decodifica um log hexadecimal (formato da tela de coleta, monitores de porta serial ou hex puro).
        minitime-protocolo monitorar <COMa> [<COMb>] [--vel 19200] [--saida log.txt]
            Escuta PASSIVAMENTE (nunca transmite) uma ou duas portas e decodifica o tráfego em tempo real.
            Com um "tap" de dois adaptadores USB-serial só com RX: COMa = linha TX do PC, COMb = linha TX do relógio.
            O log gravado em --saida pode ser reanalisado com "decodificar".
        """);
    return 2;
}

static int Decodificar(string arquivo, bool resumo)
{
    if (!File.Exists(arquivo)) { Console.Error.WriteLine("Arquivo não encontrado: " + arquivo); return 3; }
    var r = Decodificador.Decodificar(File.ReadLines(arquivo));
    if (!resumo) foreach (var q in r.Quadros) Console.WriteLine(q);
    Console.WriteLine();
    Console.Write(Decodificador.Resumo(r));
    return r.ErrosChecksum > 0 ? 1 : 0;
}

static int Monitorar(string[] a)
{
    var portas = a.TakeWhile(x => !x.StartsWith("--")).ToArray();
    var vel = int.TryParse(Opcao(a, "--vel"), out var v) ? v : 19200;
    var saida = Opcao(a, "--saida");
    if (portas.Length is < 1 or > 2) return Ajuda();
    var direcoes = portas.Length == 1 ? new[] { "RX" } : new[] { "TX", "RX" };
    using var log = saida is null ? null : new StreamWriter(saida, append: true) { AutoFlush = true };
    var trava = new object();
    var abertas = new List<SerialPort>();
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
    var receptores = direcoes.Select(_ => new ReceptorQuadros()).ToArray();
    try
    {
        for (var i = 0; i < portas.Length; i++)
        {
            var idx = i;
            var p = new SerialPort(portas[i], vel, Parity.None, 8, StopBits.One) { ReadTimeout = 200 };
            p.Open(); // só leitura: este programa nunca chama Write
            abertas.Add(p);
            p.DataReceived += (_, _) =>
            {
                var buf = new byte[p.BytesToRead];
                var n = p.Read(buf, 0, buf.Length);
                if (n <= 0) return;
                var dados = buf.AsSpan(0, n).ToArray();
                var hora = DateTime.Now.ToString("HH:mm:ss.fff");
                lock (trava)
                {
                    var linha = $"{hora} {direcoes[idx]} {BitConverter.ToString(dados).Replace('-', ' ')}";
                    log?.WriteLine(linha);
                    Console.WriteLine(linha);
                    foreach (var q in receptores[idx].Alimentar(dados))
                        Console.WriteLine("    => " + new QuadroCapturado(direcoes[idx], q, hora));
                }
            };
        }
        Console.WriteLine($"Monitorando {string.Join(" + ", portas)} a {vel} bps (8N1). Ctrl+C encerra.");
        cts.Token.WaitHandle.WaitOne();
        return 0;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
    {
        Console.Error.WriteLine("Erro de porta serial: " + ex.Message);
        return 4;
    }
    finally { foreach (var p in abertas) p.Dispose(); }
}

static string? Opcao(string[] a, string nome)
{
    var i = Array.IndexOf(a, nome);
    return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
}
