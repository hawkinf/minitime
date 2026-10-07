using Microsoft.Data.Sqlite;

namespace MiniTime.Data;

/// <summary>CRUD genérico por reflexão sobre uma entidade com [Table]/[Key].</summary>
public class Repository<T> where T : new()
{
    protected readonly MiniTimeDb Db;
    private static readonly MiniTimeDb.Mapa Mapa = MiniTimeDb.Mapa.De(typeof(T));

    public Repository(MiniTimeDb db) => Db = db;

    public string Tabela => Mapa.Tabela;

    // orderBy/where são fragmentos SQL escritos pelo código (nunca por usuários); o orderBy é validado para barrar uso indevido.
    private static readonly System.Text.RegularExpressions.Regex OrderByValido =
        new(@"^\s*[A-Za-z_][A-Za-z0-9_]*(\s+(ASC|DESC))?(\s*,\s*[A-Za-z_][A-Za-z0-9_]*(\s+(ASC|DESC))?)*\s*$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string Ordem(string? orderBy)
    {
        if (orderBy is null) return "";
        if (!OrderByValido.IsMatch(orderBy)) throw new ArgumentException($"Ordenação inválida: '{orderBy}'.", nameof(orderBy));
        return " ORDER BY " + orderBy;
    }

    public List<T> Todos(string? orderBy = null)
        => Db.Query<T>($"SELECT * FROM {Mapa.Tabela}" + Ordem(orderBy));

    public List<T> Onde(string where, string? orderBy = null, params (string, object?)[] ps)
        => Db.Query<T>($"SELECT * FROM {Mapa.Tabela} WHERE {where}" + Ordem(orderBy), ps);

    public T? Obter(params object[] chave)
    {
        if (chave.Length != Mapa.Chaves.Length) throw new ArgumentException("Chave incompleta.");
        var where = string.Join(" AND ", Mapa.Chaves.Select((k, i) => $"{k.Name} = @k{i}"));
        var ps = chave.Select((v, i) => ($"k{i}", (object?)v)).ToArray();
        return Db.Query<T>($"SELECT * FROM {Mapa.Tabela} WHERE {where}", ps).FirstOrDefault();
    }

    public long Contar(string? where = null, params (string, object?)[] ps)
        => Db.Scalar<long>($"SELECT COUNT(*) FROM {Mapa.Tabela}" + (where is null ? "" : " WHERE " + where), ps);

    public bool Existe(params object[] chave) => Obter(chave) is not null;

    public int Inserir(T e)
    {
        using var c = Db.Abrir();
        return Inserir(c, null, e);
    }

    /// <summary>Insere; se a entidade tem coluna identity, preenche-a com o rowid gerado.</summary>
    public int Inserir(SqliteConnection c, SqliteTransaction? tx, T e)
    {
        var cols = Mapa.Colunas.Where(p => p != Mapa.Identidade).ToArray();
        var sql = $"INSERT INTO {Mapa.Tabela}({string.Join(",", cols.Select(p => p.Name))}) VALUES({string.Join(",", cols.Select(p => "@" + p.Name))})";
        var n = MiniTimeDb.Execute(c, tx, sql, cols.Select(p => (p.Name, p.GetValue(e))).ToArray());
        if (Mapa.Identidade is { } id)
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT last_insert_rowid()";
            id.SetValue(e, Convert.ChangeType(cmd.ExecuteScalar(), id.PropertyType));
        }
        return n;
    }

    public int Atualizar(T e)
    {
        var cols = Mapa.Colunas.Where(p => !Mapa.Chaves.Contains(p)).ToArray();
        if (cols.Length == 0) return Existe(Mapa.Chaves.Select(k => k.GetValue(e)!).ToArray()) ? 1 : 0; // só chaves: nada a atualizar
        var sql = $"UPDATE {Mapa.Tabela} SET {string.Join(",", cols.Select(p => $"{p.Name} = @{p.Name}"))} WHERE {string.Join(" AND ", Mapa.Chaves.Select(k => $"{k.Name} = @{k.Name}"))}";
        return Db.Execute(sql, Mapa.Colunas.Select(p => (p.Name, p.GetValue(e))).ToArray());
    }

    public int Excluir(params object[] chave)
    {
        var where = string.Join(" AND ", Mapa.Chaves.Select((k, i) => $"{k.Name} = @k{i}"));
        return Db.Execute($"DELETE FROM {Mapa.Tabela} WHERE {where}", chave.Select((v, i) => ($"k{i}", (object?)v)).ToArray());
    }

    /// <summary>Próximo código livre para tabelas com chave inteira.</summary>
    public int ProximoCodigo()
        => (int)(Db.Scalar<long?>($"SELECT MAX({Mapa.Chaves[0].Name}) FROM {Mapa.Tabela}") ?? 0) + 1;
}
