using MiniTime.Core.Models;
using MiniTime.Core.Seguranca;
using MiniTime.Core.Util;
using MiniTime.Data;

namespace MiniTime.Tests;

public class ManutencaoTests
{
    [Fact]
    public void Backup_restaura_e_valida()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "mt-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        try
        {
            var arq = Path.Combine(pasta, "a.db");
            var db = new MiniTimeDb(arq);
            var r = new Repositorios(db);
            r.Funcionarios.Inserir(new Funcionario { Codigo = CodigoCartao.Normalizar("1"), Nome = "Um" });
            var bkp = Path.Combine(pasta, "bkp.db");
            new ManutencaoService(db).Backup(bkp);
            Assert.Null(ManutencaoService.ValidarArquivoBackup(bkp));
            r.Funcionarios.Inserir(new Funcionario { Codigo = CodigoCartao.Normalizar("2"), Nome = "Dois" });
            Assert.Equal(2, r.Funcionarios.Contar());

            var seguranca = ManutencaoService.Restaurar(bkp, arq);
            Assert.True(File.Exists(seguranca));
            var depois = new Repositorios(new MiniTimeDb(arq));
            Assert.Equal(1, depois.Funcionarios.Contar());

            File.WriteAllText(Path.Combine(pasta, "lixo.db"), "isto nao e sqlite");
            Assert.NotNull(ManutencaoService.ValidarArquivoBackup(Path.Combine(pasta, "lixo.db")));
            Assert.NotNull(ManutencaoService.ValidarArquivoBackup(Path.Combine(pasta, "nao-existe.db")));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(pasta, true); } catch (IOException) { }
        }
    }

    [Fact]
    public void Reparar_corrige_duplicadas_e_aponta_orfaos()
    {
        var db = new MiniTimeDb(":memory:");
        var r = new Repositorios(db);
        r.Funcionarios.Inserir(new Funcionario { Codigo = CodigoCartao.Normalizar("1"), Nome = "Um", Horario = 9 });
        var dh = new DateTime(2026, 7, 1, 9, 0, 0);
        for (var i = 0; i < 3; i++) r.Marcacoes.Inserir(new Marcacao { Cracha = CodigoCartao.Normalizar("1"), DataHora = dh, Tipo = 7 });
        r.Ferias.Inserir(new Ferias { Funcionario = CodigoCartao.Normalizar("9"), Inicio = dh, Fim = dh });
        var res = new ManutencaoService(db).Reparar();
        Assert.Equal(1, r.Marcacoes.Contar());
        Assert.Equal(0, r.Ferias.Contar());
        Assert.Contains(res.Correcoes, c => c.Contains("duplicadas"));
        Assert.Contains(res.Problemas, p => p.Contains("jornada"));
        new ManutencaoService(db).Reorganizar();
    }

    [Fact]
    public void Senha_hash_confere_e_nao_repete()
    {
        var a = Senha.Hash("segredo");
        var b = Senha.Hash("segredo");
        Assert.NotEqual(a, b);
        Assert.True(Senha.Confere("segredo", a));
        Assert.False(Senha.Confere("outra", a));
        Assert.False(Senha.Confere("segredo", "lixo"));
    }
}
