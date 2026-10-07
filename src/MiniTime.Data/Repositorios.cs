using MiniTime.Core.Models;
using MiniTime.Core.Util;

namespace MiniTime.Data;

public sealed class MarcacaoRepository(MiniTimeDb db) : Repository<Marcacao>(db)
{
    public List<Marcacao> Periodo(DateTime de, DateTime ate, string? cracha = null)
    {
        var sql = "DataHora >= @de AND DataHora <= @ate";
        var ps = new List<(string, object?)> { ("de", de), ("ate", ate) };
        if (cracha is not null) { sql += " AND Cracha = @c"; ps.Add(("c", CodigoCartao.Normalizar(cracha))); }
        return Onde(sql, "Cracha, DataHora", [.. ps]);
    }

    public bool Existe(string cracha, DateTime dataHora)
        => Contar("Cracha = @c AND DataHora = @d", ("c", CodigoCartao.Normalizar(cracha)), ("d", dataHora)) > 0;
}

public sealed class ParametrosRepository(MiniTimeDb db) : Repository<Parametros>(db)
{
    public Parametros Obter() => Obter(1) ?? new Parametros();
    public void Salvar(Parametros p)
    {
        p.Id = 1;
        if (Existe(1)) Atualizar(p); else Inserir(p);
    }
}

public sealed class FuncionarioRepository(MiniTimeDb db) : Repository<Funcionario>(db)
{
    public List<Funcionario> PorNome(string trecho)
        => Onde("Nome LIKE @n", "Nome", ("n", "%" + trecho + "%"));
}

/// <summary>Ponto único de acesso aos repositórios de uma base.</summary>
public sealed class Repositorios
{
    public MiniTimeDb Db { get; }
    public Repositorios(MiniTimeDb db)
    {
        Db = db;
        Funcionarios = new FuncionarioRepository(db);
        Horarios = new Repository<Horario>(db);
        Jornadas = new Repository<Jornada>(db);
        Feriados = new Repository<Feriado>(db);
        Ferias = new Repository<Ferias>(db);
        Justificativas = new Repository<Justificativa>(db);
        Marcacoes = new MarcacaoRepository(db);
        Backup = new Repository<MarcacaoBackup>(db);
        Sirenes = new Repository<Sirene>(db);
        SirenesBioLite = new Repository<SireneBioLite>(db);
        Terminais = new Repository<Terminal>(db);
        Templates = new Repository<TemplateBio>(db);
        Parametros = new ParametrosRepository(db);
        Usuarios = new Repository<Usuario>(db);
    }

    public FuncionarioRepository Funcionarios { get; }
    public Repository<Horario> Horarios { get; }
    public Repository<Jornada> Jornadas { get; }
    public Repository<Feriado> Feriados { get; }
    public Repository<Ferias> Ferias { get; }
    public Repository<Justificativa> Justificativas { get; }
    public MarcacaoRepository Marcacoes { get; }
    public Repository<MarcacaoBackup> Backup { get; }
    public Repository<Sirene> Sirenes { get; }
    public Repository<SireneBioLite> SirenesBioLite { get; }
    public Repository<Terminal> Terminais { get; }
    public Repository<TemplateBio> Templates { get; }
    public ParametrosRepository Parametros { get; }
    public Repository<Usuario> Usuarios { get; }
}
