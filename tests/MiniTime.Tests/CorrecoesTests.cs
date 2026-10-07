using Microsoft.Data.Sqlite;
using MiniTime.Core.Apuracao;
using MiniTime.Core.Models;
using MiniTime.Core.Util;
using MiniTime.Data;
using MiniTime.Serial;

namespace MiniTime.Tests;

public class CorrecoesDadosTests
{
    private static string PastaTemp()
    {
        var p = Path.Combine(Path.GetTempPath(), "mt-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(p);
        return p;
    }

    private static void Limpar(string pasta)
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(pasta, true); } catch (IOException) { }
    }

    [Fact]
    public void Reparar_em_arquivo_guarda_copia_de_seguranca_antes_de_apagar()
    {
        var pasta = PastaTemp();
        try
        {
            var db = new MiniTimeDb(Path.Combine(pasta, "a.db"));
            var r = new Repositorios(db);
            var dh = new DateTime(2026, 7, 1, 9, 0, 0);
            for (var i = 0; i < 2; i++) r.Marcacoes.Inserir(new Marcacao { Cracha = CodigoCartao.Normalizar("1"), DataHora = dh, Tipo = 7 });
            var res = new ManutencaoService(db).Reparar();
            Assert.NotNull(res.CopiaSeguranca);
            Assert.True(File.Exists(res.CopiaSeguranca));
            // a cópia ainda tem as duas linhas; o banco só uma
            var copia = new Repositorios(new MiniTimeDb(res.CopiaSeguranca!));
            Assert.Equal(2, copia.Marcacoes.Contar());
            Assert.Equal(1, r.Marcacoes.Contar());
        }
        finally { Limpar(pasta); }
    }

    [Fact]
    public void Reparar_nao_corta_cartao_maior_que_16_posicoes_e_avisa()
    {
        var db = new MiniTimeDb(":memory:");
        var r = new Repositorios(db);
        var longo = "12345678901234567"; // 17
        r.Marcacoes.Inserir(new Marcacao { Cracha = longo, DataHora = new DateTime(2026, 7, 1, 9, 0, 0), Tipo = 7 });
        r.Marcacoes.Inserir(new Marcacao { Cracha = "7984", DataHora = new DateTime(2026, 7, 1, 9, 1, 0), Tipo = 7 });
        var res = new ManutencaoService(db).Reparar();
        Assert.Equal(1, r.Marcacoes.Contar("Cracha = @c", ("c", longo)));
        Assert.Equal(1, r.Marcacoes.Contar("Cracha = @c", ("c", "0000000000007984")));
        Assert.Contains(res.Problemas, p => p.Contains("mais de 16"));
        Assert.False(res.Integro);
    }

    [Fact]
    public void Restaurar_nao_deixa_temporario_para_tras()
    {
        var pasta = PastaTemp();
        try
        {
            var arq = Path.Combine(pasta, "a.db");
            var db = new MiniTimeDb(arq);
            var bkp = Path.Combine(pasta, "bkp.db");
            new ManutencaoService(db).Backup(bkp);
            ManutencaoService.Restaurar(bkp, arq);
            Assert.False(File.Exists(arq + ".restaurando"));
            Assert.Null(ManutencaoService.ValidarArquivoBackup(arq));
        }
        finally { Limpar(pasta); }
    }

    [Fact]
    public void Coleta_so_considera_duplicada_outra_coletada_no_mesmo_minuto()
    {
        var db = new MiniTimeDb(":memory:");
        var r = new Repositorios(db);
        var dh = new DateTime(2026, 7, 1, 9, 0, 0);
        r.Marcacoes.Inserir(new Marcacao { Cracha = CodigoCartao.Normalizar("1"), DataHora = dh, Tipo = TipoMarcacao.Manual });
        var svc = new ColetaService(db);
        var agora = new DateTime(2026, 8, 1);
        Assert.Equal(1, svc.Gravar([("1", dh)], 1, agora).Gravadas); // manual não bloqueia a coletada
        Assert.Equal(1, svc.Gravar([("1", dh)], 1, agora).Repetidas); // a segunda coletada é duplicata
        Assert.Equal(2, r.Marcacoes.Contar());
    }

    [Fact]
    public void Coletas_simultaneas_nao_duplicam()
    {
        var pasta = PastaTemp();
        try
        {
            var db = new MiniTimeDb(Path.Combine(pasta, "a.db"));
            var lote = Enumerable.Range(0, 60).Select(i => ("7984", new DateTime(2026, 7, 1, 8, 0, 0).AddMinutes(i))).ToArray();
            var agora = new DateTime(2026, 8, 1);
            Parallel.For(0, 4, _ => new ColetaService(db).Gravar(lote, 1, agora));
            Assert.Equal(60, new Repositorios(db).Marcacoes.Contar());
        }
        finally { Limpar(pasta); }
    }

    [Fact]
    public void Data_gravada_em_formato_iso_com_T_ainda_e_lida()
    {
        Assert.Equal(new DateTime(2026, 7, 1, 9, 5, 0), MiniTimeDb.ParseDataHora("2026-07-01T09:05:00"));
        Assert.Equal(new DateTime(2026, 7, 1), MiniTimeDb.ParseDataHora("2026-07-01"));
        Assert.Throws<FormatException>(() => MiniTimeDb.ParseDataHora("ontem"));
    }

    [Fact]
    public void Ordenacao_invalida_e_recusada()
    {
        var r = new Repositorios(new MiniTimeDb(":memory:"));
        Assert.Empty(r.Funcionarios.Todos("Nome DESC, Codigo"));
        Assert.Throws<ArgumentException>(() => r.Funcionarios.Todos("Nome; DROP TABLE Funcionario"));
        Assert.Throws<ArgumentException>(() => r.Funcionarios.Onde("1=1", "(SELECT 1)"));
    }
}

public class CorrecoesSerialTests
{
    /// <summary>Entrega um bloco de bytes pronto de uma vez, como uma porta que acumulou vários quadros.</summary>
    private sealed class BlocoUnico(byte[] bloco) : ITransporteSerial
    {
        private bool _entregue;
        public void Escrever(ReadOnlySpan<byte> bytes) { }
        public int Ler(Span<byte> destino, TimeSpan timeout)
        {
            if (_entregue) return 0;
            _entregue = true;
            bloco.CopyTo(destino);
            return bloco.Length;
        }
        public void LimparEntrada() { }
        public void Dispose() { }
    }

    [Fact]
    public void Quadros_que_chegam_juntos_nao_se_perdem()
    {
        var a = new Quadro(Quadro.ParaBcd(1), 0x10, [1]).Serializar(false);
        var b = new Quadro(Quadro.ParaBcd(1), 0x11, [2]).Serializar(false);
        using var cli = new ClienteRelogio(new BlocoUnico([.. a, .. b]), 1);
        Assert.Equal(0x10, cli.Receber(TimeSpan.FromMilliseconds(300))!.Comando);
        Assert.Equal(0x11, cli.Receber(TimeSpan.FromMilliseconds(300))!.Comando);
        Assert.Null(cli.Receber(TimeSpan.FromMilliseconds(120)));
    }

    private sealed class RespondeLixo : ITransporteSerial
    {
        private readonly Queue<byte> _saida = new();
        public void Escrever(ReadOnlySpan<byte> bytes) => _saida.Clear();
        public int Ler(Span<byte> destino, TimeSpan timeout)
        {
            if (_saida.Count == 0)
                foreach (var x in new Quadro(Quadro.ParaBcd(1), 0x55, [1, 2]).Serializar(false)) _saida.Enqueue(x);
            var n = 0;
            while (n < destino.Length && _saida.Count > 0) destino[n++] = _saida.Dequeue();
            return n;
        }
        public void LimparEntrada() { }
        public void Dispose() { }
    }

    [Fact]
    public void Resposta_inesperada_interrompe_a_coleta_com_erro_explicito()
    {
        using var cli = new ClienteRelogio(new RespondeLixo(), 1);
        Assert.Throws<InvalidDataException>(() => cli.Coletar(null, TimeSpan.FromMilliseconds(200)).ToList());
    }
}

public class DecodificadorTests
{
    [Fact]
    public void Decodifica_log_da_tela_de_coleta_com_direcao_e_instante()
    {
        var tx = BitConverter.ToString(new Quadro(Quadro.ParaBcd(1), 0x0E, []).Serializar()).Replace('-', ' ');
        var rx = BitConverter.ToString(new Quadro(Quadro.ParaBcd(1), 0x1F, "0002"u8.ToArray()).Serializar(false)).Replace('-', ' ');
        var r = Decodificador.Decodificar([$"14:05:01.123 TX {tx}", $"14:05:01.180 RX {rx}"]);
        Assert.Equal(0, r.ErrosChecksum);
        Assert.Equal(2, r.Quadros.Count);
        Assert.Equal(("TX", (byte)0x0E), (r.Quadros[0].Direcao, r.Quadros[0].Quadro.Comando));
        Assert.Equal("0002", r.Quadros[1].DadosComoTexto);
        Assert.Equal("14:05:01.180", r.Quadros[1].Instante);
        Assert.Contains("cmd=0x1F", r.Quadros[1].ToString());
    }

    [Fact]
    public void Quadro_dividido_em_varias_linhas_e_hex_colado_funcionam_e_checksum_ruim_e_contado()
    {
        var bytes = new Quadro(Quadro.ParaBcd(12), 0x20, [1, 2, 3]).Serializar(false);
        var hex = BitConverter.ToString(bytes).Replace("-", "");
        var r = Decodificador.Decodificar([$"RX {hex[..6]}", $"RX 0x{hex[6..8]},{hex[8..]}", "# comentário", ""]);
        Assert.Single(r.Quadros);
        Assert.Equal(new byte[] { 1, 2, 3 }, r.Quadros[0].Quadro.Dados);

        bytes[^2] ^= 0x01;
        var ruim = Decodificador.Decodificar([BitConverter.ToString(bytes).Replace('-', ' ')]);
        Assert.Empty(ruim.Quadros);
        Assert.Equal(1, ruim.ErrosChecksum);
    }

    [Fact]
    public void Resumo_agrupa_por_comando()
    {
        var a = BitConverter.ToString(new Quadro(Quadro.ParaBcd(1), 0x0F, []).Serializar()).Replace('-', ' ');
        var r = Decodificador.Decodificar([$"TX {a}", $"TX {a}"]);
        Assert.Contains("TX cmd=0x0F  x2", Decodificador.Resumo(r));
    }
}
