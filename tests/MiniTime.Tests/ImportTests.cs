using MiniTime.Data;
using MiniTime.Data.Importacao;

namespace MiniTime.Tests;

public class ImportTests
{
    private static string? MdbReal()
    {
        var env = Environment.GetEnvironmentVariable("MINITIME_TEST_MDB");
        if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
        var padrao = @"C:\Program Files (x86)\Dimep\MiniTime\DIMEP.MDB";
        return File.Exists(padrao) ? padrao : null;
    }

    [Fact]
    [Trait("Category", "Live")]
    public void Importa_mdb_real_e_confere_contagens()
    {
        var mdb = MdbReal();
        if (mdb is null || MdbReaderLocator.Localizar() is null || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MINITIME_MDB_PWD"))) return; // sem MDB/leitor nesta máquina

        var db = new MiniTimeDb(":memory:");
        var rep = new Repositorios(db);
        var rel = new MdbImporter(db).Importar(new MdbImportOptions { MdbPath = mdb, Senha = Environment.GetEnvironmentVariable("MINITIME_MDB_PWD") ?? "" });

        Assert.True(rel.Sucesso, rel.Resumo());
        var marc = rel.Tabelas.Single(t => t.Tabela == "Marcacao");
        Assert.Equal(marc.Origem, rep.Marcacoes.Contar() + marc.Quarentena);
        Assert.Equal(marc.Importados, rep.Marcacoes.Contar());
        Assert.True(rep.Funcionarios.Contar() > 0);
        Assert.Equal(1, rep.Parametros.Contar());
        Assert.Equal(19200, rep.Parametros.Obter().Velocidade);
        Assert.All(rep.Funcionarios.Todos(), f => Assert.Equal(16, f.Codigo.Length));
        Assert.Equal(marc.Quarentena, db.Scalar<long>("SELECT COUNT(*) FROM ImportacaoQuarentena WHERE Tabela='Marcacao'"));
        Assert.Equal(0, rep.Marcacoes.Contar("DataHora > @d", ("d", DateTime.Now.AddDays(2))));

        // Reimportar em modo "substituir" deve dar o mesmo resultado (idempotente)
        var rel2 = new MdbImporter(db).Importar(new MdbImportOptions { MdbPath = mdb, Senha = Environment.GetEnvironmentVariable("MINITIME_MDB_PWD") ?? "" });
        Assert.Equal(rel.TotalImportados, rel2.TotalImportados);
        Assert.Equal(marc.Importados, rep.Marcacoes.Contar());
    }

    [Fact]
    public void Arquivo_inexistente_gera_erro_claro()
    {
        var db = new MiniTimeDb(":memory:");
        Assert.Throws<FileNotFoundException>(() => new MdbImporter(db).Importar(new MdbImportOptions { MdbPath = @"C:\nao\existe.mdb" }));
    }
}

public class FerramentasDev
{
    /// <summary>Gera um SQLite real a partir do MDB para testar a interface (somente quando MINITIME_OUT_DB está definida).</summary>
    [Fact]
    [Trait("Category", "Tool")]
    public void Gera_banco_de_desenvolvimento()
    {
        var saida = Environment.GetEnvironmentVariable("MINITIME_OUT_DB");
        var mdb = Environment.GetEnvironmentVariable("MINITIME_TEST_MDB") ?? @"C:\Program Files (x86)\Dimep\MiniTime\DIMEP.MDB";
        if (string.IsNullOrEmpty(saida) || !File.Exists(mdb)) return;
        if (File.Exists(saida)) File.Delete(saida);
        var db = new MiniTimeDb(saida);
        new MdbImporter(db).Importar(new MdbImportOptions { MdbPath = mdb, Senha = Environment.GetEnvironmentVariable("MINITIME_MDB_PWD") ?? "" });
    }
}
