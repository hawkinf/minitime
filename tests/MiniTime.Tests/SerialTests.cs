using MiniTime.Serial;

namespace MiniTime.Tests;

public class SerialTests
{
    [Theory]
    [InlineData(1, 0x01)]
    [InlineData(12, 0x12)]
    [InlineData(99, 0x99)]
    public void Endereco_vira_bcd(int n, byte bcd)
    {
        Assert.Equal(bcd, Quadro.ParaBcd(n));
        Assert.Equal(n, Quadro.DeBcd(bcd));
    }

    [Fact]
    public void Quadro_binario_serializa_e_recebe_de_volta()
    {
        var q = new Quadro(Quadro.ParaBcd(1), 0x0E, [0x41, 0x42]);
        var bytes = q.Serializar();
        Assert.Equal(new byte[] { 0xAA, 0xFF, 0xFF, 0xFE }, bytes[..4]);
        Assert.Equal(0xF0, bytes[^1]);
        var rx = new ReceptorQuadros(1);
        var r = rx.Alimentar(bytes).Single();
        Assert.Equal(q.Comando, r.Comando);
        Assert.Equal(q.Dados, r.Dados);
        Assert.False(r.Ascii);
    }

    [Fact]
    public void Quadro_ascii_usa_nibbles_de_0_a_interrogacao()
    {
        var q = new Quadro(Quadro.ParaBcd(12), 0x1F, [0x00, 0xA5, 0xFF], Ascii: true);
        var bytes = q.Serializar(comPreambulo: false);
        Assert.Equal(0xFD, bytes[0]);
        Assert.Equal((byte)'1', bytes[1]);
        Assert.Equal((byte)'2', bytes[2]);
        Assert.All(bytes[3..^1], b => Assert.InRange(b, (byte)0x30, (byte)0x3F));
        var r = new ReceptorQuadros(12).Alimentar(bytes).Single();
        Assert.True(r.Ascii);
        Assert.Equal(new byte[] { 0x00, 0xA5, 0xFF }, r.Dados);
        Assert.Equal(0x1F, r.Comando);
    }

    [Fact]
    public void Checksum_invalido_e_endereco_diferente_sao_descartados()
    {
        var bytes = new Quadro(Quadro.ParaBcd(1), 0x10, [1, 2, 3]).Serializar(false);
        bytes[^2] ^= 0x01; // estraga o checksum
        var rx = new ReceptorQuadros(1);
        Assert.Empty(rx.Alimentar(bytes));
        Assert.Equal(1, rx.ErrosChecksum);

        var outro = new Quadro(Quadro.ParaBcd(2), 0x10, [1]).Serializar(false);
        Assert.Empty(new ReceptorQuadros(1).Alimentar(outro));
    }

    [Fact]
    public void Receptor_ignora_lixo_e_junta_quadros_em_pedacos()
    {
        var q = new Quadro(Quadro.ParaBcd(1), 0x20, [9, 8, 7]).Serializar(false);
        var rx = new ReceptorQuadros(1);
        var lixo = new byte[] { 0x00, 0x55, 0xAA };
        Assert.Empty(rx.Alimentar(lixo));
        Assert.Empty(rx.Alimentar(q.AsSpan(0, 3)));
        Assert.Single(rx.Alimentar(q.AsSpan(3)));
    }

    [Fact]
    public void Registro_de_28_caracteres_vira_marcacao()
    {
        var r = RegistroColetado.Interpretar("0000000000007984" + "030826" + "0904" + "07")!;
        Assert.Equal("0000000000007984", r.Cartao);
        Assert.Equal(new DateTime(2026, 8, 3, 9, 4, 0), r.DataHora);
        Assert.Equal(7, r.Tipo);
        Assert.Null(RegistroColetado.Interpretar("0000000000007984" + "310226" + "0904" + "07")); // 31/02 inválido
        Assert.Null(RegistroColetado.Interpretar("curto"));
    }

    [Fact]
    public void Coleta_percorre_registros_ate_esgotar()
    {
        var relogio = new RelogioSimulado(1, ["0000000000007984030826" + "090407", "0000000000007984030826" + "180007"]);
        using var cli = new ClienteRelogio(relogio, 1);
        var progresso = new List<(int, int)>();
        var lidos = cli.Coletar((a, b) => progresso.Add((a, b)), TimeSpan.FromMilliseconds(300)).ToList();
        Assert.Equal(2, lidos.Count);
        Assert.Equal(new TimeSpan(18, 0, 0), lidos[1].DataHora.TimeOfDay);
        Assert.Equal((2, 2), progresso[^1]);
    }

    [Fact]
    public void Coleta_sem_resposta_gera_timeout_claro()
    {
        using var cli = new ClienteRelogio(new RelogioSimulado(1, [], mudo: true), 1);
        Assert.Throws<TimeoutException>(() => cli.Coletar(null, TimeSpan.FromMilliseconds(120)).ToList());
    }

    /// <summary>Relógio de mentira que segue a hipótese de protocolo (J → contagem; K → próximo registro).</summary>
    private sealed class RelogioSimulado(int endereco, string[] registros, bool mudo = false) : ITransporteSerial
    {
        private readonly Queue<byte> _saida = new();
        private readonly ReceptorQuadros _rx = new(endereco);
        private int _proximo;

        public void Escrever(ReadOnlySpan<byte> bytes)
        {
            if (mudo) return;
            foreach (var q in _rx.Alimentar(bytes))
            {
                Quadro resp;
                if (q.Comando == Comandos.PerguntaPendentes)
                    resp = new Quadro(q.Endereco, Comandos.RespostaContagem, System.Text.Encoding.ASCII.GetBytes($"{registros.Length:0000}"));
                else if (_proximo < registros.Length)
                    resp = new Quadro(q.Endereco, Comandos.ConfirmaProximo, System.Text.Encoding.ASCII.GetBytes(registros[_proximo++].PadRight(28, '0')[..28]));
                else
                    resp = new Quadro(q.Endereco, Comandos.ConfirmaProximo, []);
                foreach (var b in resp.Serializar(false)) _saida.Enqueue(b);
            }
        }

        public int Ler(Span<byte> destino, TimeSpan timeout)
        {
            var n = 0;
            while (n < destino.Length && _saida.Count > 0) destino[n++] = _saida.Dequeue();
            if (n == 0) Thread.Sleep(Math.Min(20, (int)timeout.TotalMilliseconds));
            return n;
        }

        public void LimparEntrada() => _saida.Clear();
        public void Dispose() { }
    }
}
