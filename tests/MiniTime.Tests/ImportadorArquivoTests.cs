using MiniTime.Core.Apuracao;
using MiniTime.Core.Exportacao;
using MiniTime.Core.Models;

namespace MiniTime.Tests;

public class ImportadorArquivoTests
{
    [Theory]
    [InlineData(4, 2)]
    [InlineData(5, 4)]
    [InlineData(6, 2)]
    public void Exporta_e_le_de_volta(int digitos, int ano)
    {
        var o = new OpcoesExportacao(digitos, ano);
        var ms = new[]
        {
            new Marcacao { Cracha = "0000000000007984", DataHora = new DateTime(2026, 7, 1, 9, 4, 0), Tipo = TipoMarcacao.Coletada, Terminal = 1 },
            new Marcacao { Cracha = "0000000000000012", DataHora = new DateTime(2026, 7, 2, 18, 0, 0), Tipo = TipoMarcacao.Manual, Justificativa = 3, Terminal = 2 },
        };
        var txt = ExportadorMarcacoes.Gerar(ms, o);
        var lido = ImportadorArquivo.Ler(txt.Split("\r\n"), digitos, ano);
        Assert.Empty(lido.Invalidas);
        Assert.Equal(2, lido.Linhas.Count);
        Assert.Equal("0000000000007984", lido.Linhas[0].Cartao);
        Assert.Equal(new DateTime(2026, 7, 1, 9, 4, 0), lido.Linhas[0].DataHora);
        Assert.Equal(7, lido.Linhas[0].Tipo);
        Assert.Equal(1, lido.Linhas[0].Relogio);
        Assert.Equal(0, lido.Linhas[1].Tipo);
        Assert.Equal(2, lido.Linhas[1].Relogio);
    }

    [Fact]
    public void Linhas_invalidas_sao_apontadas_com_o_numero()
    {
        var lido = ImportadorArquivo.Ler(["05362002031018070000100", "lixo", "05363102031018070000100", ""], 4, 2);
        Assert.Single(lido.Linhas);
        Assert.Equal(new[] { 2, 3 }, lido.Invalidas.Select(i => i.Numero));
    }
}
