using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Microsoft.Data.Sqlite;
using MiniTime.Core.Models;

namespace MiniTime.Data;

/// <summary>
/// Acesso ao SQLite: abre conexões, aplica o schema e oferece Query/Execute com mapeamento
/// coluna→propriedade (mesmo nome, sem diferenciar maiúsculas).
/// </summary>
public sealed class MiniTimeDb
{
    public const string FormatoDataHora = "yyyy-MM-dd HH:mm:ss";
    public const string FormatoHora = @"hh\:mm";

    public string CaminhoArquivo { get; }
    private readonly string _connectionString;
    private SqliteConnection? _keepAlive; // necessário para ":memory:" compartilhado

    public MiniTimeDb(string caminhoArquivo)
    {
        CaminhoArquivo = caminhoArquivo;
        var memoria = caminhoArquivo == ":memory:";
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = memoria ? "file:minitime-mem-" + Guid.NewGuid().ToString("N") + "?mode=memory&cache=shared" : caminhoArquivo,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = !memoria,
        };
        _connectionString = csb.ToString();
        if (memoria) _keepAlive = Abrir();
        using var c = Abrir();
        Schema.Migrar(c);
    }

    public SqliteConnection Abrir()
    {
        var c = new SqliteConnection(_connectionString);
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;";
        try { cmd.ExecuteNonQuery(); } catch (SqliteException) { /* :memory: não suporta WAL */ }
        return c;
    }

    // ---------- consultas ----------

    public List<T> Query<T>(string sql, params (string Nome, object? Valor)[] ps) where T : new()
    {
        using var c = Abrir();
        return Query<T>(c, null, sql, ps);
    }

    public static List<T> Query<T>(SqliteConnection c, SqliteTransaction? tx, string sql, params (string Nome, object? Valor)[] ps) where T : new()
    {
        using var cmd = Criar(c, tx, sql, ps);
        using var r = cmd.ExecuteReader();
        var map = Mapa.De(typeof(T));
        var colProp = new PropertyInfo?[r.FieldCount];
        for (var i = 0; i < r.FieldCount; i++)
            map.PorColuna.TryGetValue(r.GetName(i), out colProp[i]);
        var lista = new List<T>();
        while (r.Read())
        {
            var o = new T();
            for (var i = 0; i < r.FieldCount; i++)
                if (colProp[i] is { } p)
                    p.SetValue(o, Converter(r.IsDBNull(i) ? null : r.GetValue(i), p.PropertyType));
            lista.Add(o);
        }
        return lista;
    }

    public T? Scalar<T>(string sql, params (string Nome, object? Valor)[] ps)
    {
        using var c = Abrir();
        using var cmd = Criar(c, null, sql, ps);
        var v = cmd.ExecuteScalar();
        return v is null or DBNull ? default : (T?)Converter(v, typeof(T));
    }

    public int Execute(string sql, params (string Nome, object? Valor)[] ps)
    {
        using var c = Abrir();
        return Execute(c, null, sql, ps);
    }

    public static int Execute(SqliteConnection c, SqliteTransaction? tx, string sql, params (string Nome, object? Valor)[] ps)
    {
        using var cmd = Criar(c, tx, sql, ps);
        return cmd.ExecuteNonQuery();
    }

    private static SqliteCommand Criar(SqliteConnection c, SqliteTransaction? tx, string sql, (string Nome, object? Valor)[] ps)
    {
        var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (nome, valor) in ps)
            cmd.Parameters.AddWithValue(nome.StartsWith('@') ? nome : "@" + nome, Valor(valor));
        return cmd;
    }

    // ---------- conversão ----------

    /// <summary>Converte valor .NET → valor gravável no SQLite.</summary>
    public static object Valor(object? v) => v switch
    {
        null => DBNull.Value,
        DateTime d => d.ToString(FormatoDataHora, CultureInfo.InvariantCulture),
        TimeSpan t => t.ToString(FormatoHora, CultureInfo.InvariantCulture),
        bool b => b ? 1 : 0,
        _ => v,
    };

    /// <summary>Converte valor lido do SQLite → tipo da propriedade.</summary>
    public static object? Converter(object? v, Type alvo)
    {
        var t = Nullable.GetUnderlyingType(alvo) ?? alvo;
        if (v is null or DBNull)
            return alvo.IsValueType && Nullable.GetUnderlyingType(alvo) is null ? Activator.CreateInstance(alvo) : null;
        if (t == typeof(string)) return Convert.ToString(v, CultureInfo.InvariantCulture);
        if (t == typeof(bool)) return v is string sb ? sb is "1" or "true" or "True" : Convert.ToInt64(v) != 0;
        if (t == typeof(DateTime))
            return v is string sd ? ParseDataHora(sd) : Convert.ToDateTime(v);
        if (t == typeof(TimeSpan))
            return v is string st ? ParseHora(st) : TimeSpan.FromMinutes(Convert.ToDouble(v));
        if (t == typeof(byte[])) return v as byte[];
        if (t.IsEnum) return Enum.ToObject(t, Convert.ToInt32(v));
        return Convert.ChangeType(v, t, CultureInfo.InvariantCulture);
    }

    /// <summary>Lê data/hora gravada pelo programa; aceita também ISO com "T" ou só a data (bancos editados à mão ou vindos de outras versões).</summary>
    public static DateTime ParseDataHora(string s)
    {
        if (DateTime.TryParseExact(s, FormatoDataHora, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d;
        throw new FormatException($"Data/hora inválida no banco: '{s}'");
    }

    public static TimeSpan ParseHora(string s)
    {
        if (TimeSpan.TryParseExact(s, FormatoHora, CultureInfo.InvariantCulture, out var t)) return t;
        if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out t)) return t;
        throw new FormatException($"Hora inválida: '{s}'");
    }

    // ---------- metadados de entidade ----------

    internal sealed class Mapa
    {
        private static readonly ConcurrentDictionary<Type, Mapa> Cache = new();
        public string Tabela = "";
        public PropertyInfo[] Colunas = [];
        public PropertyInfo[] Chaves = [];
        public PropertyInfo? Identidade;
        public Dictionary<string, PropertyInfo> PorColuna = new(StringComparer.OrdinalIgnoreCase);

        public static Mapa De(Type t) => Cache.GetOrAdd(t, static tipo =>
        {
            var props = tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<NotMappedAttribute>() is null).ToArray();
            return new Mapa
            {
                Tabela = tipo.GetCustomAttribute<TableAttribute>()?.Name ?? tipo.Name,
                Colunas = props,
                Chaves = props.Where(p => p.GetCustomAttribute<KeyAttribute>() is not null).ToArray(),
                Identidade = props.FirstOrDefault(p => p.GetCustomAttribute<IdentityAttribute>() is not null),
                PorColuna = props.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase),
            };
        });
    }
}
