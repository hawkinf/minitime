using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MiniTime.Core.Models;
using MiniTime.Core.Util;

namespace MiniTime.Data.Importacao;

public sealed class MdbImportOptions
{
    public required string MdbPath { get; init; }
    /// <summary>Caminho do MiniTime.MdbReader.exe; null = localizar automaticamente.</summary>
    public string? ReaderPath { get; init; }
    /// <summary>Senha do MDB (Jet). Vazia = tenta sem senha; também lê MINITIME_MDB_PWD.</summary>
    public string Senha { get; init; } = "";
    /// <summary>true = apaga os dados atuais do SQLite antes de importar; false = acrescenta (duplicados vão para a quarentena).</summary>
    public bool Substituir { get; init; } = true;
    /// <summary>Data máxima aceita em marcações; posteriores vão para a quarentena. Padrão: agora + 1 dia.</summary>
    public DateTime? DataMaximaMarcacao { get; init; }
    public DateTime DataMinimaMarcacao { get; init; } = new(1990, 1, 1);
}

public sealed record MdbImportProgress(string Tabela, long Atual, long Total, string Mensagem);

public sealed class MdbTableResult
{
    public string Tabela { get; init; } = "";
    public long Origem { get; set; }
    public long Importados { get; set; }
    public long Quarentena { get; set; }
    public List<string> ColunasIgnoradas { get; } = [];
    public bool Confere => Importados + Quarentena == Origem;
}

public sealed class MdbImportReport
{
    public List<MdbTableResult> Tabelas { get; } = [];
    public List<string> Avisos { get; } = [];
    public TimeSpan Duracao { get; set; }
    public bool Sucesso => Tabelas.All(t => t.Confere);
    public long TotalImportados => Tabelas.Sum(t => t.Importados);
    public long TotalQuarentena => Tabelas.Sum(t => t.Quarentena);

    public string Resumo()
    {
        var sb = new StringBuilder();
        foreach (var t in Tabelas)
            sb.AppendLine($"{t.Tabela,-16} origem={t.Origem,7}  importados={t.Importados,7}  quarentena={t.Quarentena,4}  {(t.Confere ? "OK" : "DIVERGE")}");
        foreach (var a in Avisos) sb.AppendLine("Aviso: " + a);
        sb.AppendLine($"Duração: {Duracao.TotalSeconds:F1}s");
        return sb.ToString();
    }
}

/// <summary>
/// Importa o DIMEP.MDB legado para o SQLite. O MDB é copiado para uma pasta temporária (o original nunca é tocado),
/// lido por um processo x86 (MiniTime.MdbReader.exe) e gravado numa única transação: ou entra tudo, ou nada.
/// </summary>
public sealed class MdbImporter(MiniTimeDb db)
{
    // Tabelas do MDB que viram entidades; as demais (Tmp_*) são temporárias de relatório e são ignoradas.
    private static readonly Dictionary<string, Type> Destinos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Parametros"] = typeof(Parametros),
        ["Horarios"] = typeof(Horario),
        ["Jornadas"] = typeof(Jornada),
        ["Funcionario"] = typeof(Funcionario),
        ["Feriados"] = typeof(Feriado),
        ["Ferias"] = typeof(Ferias),
        ["Justificativas"] = typeof(Justificativa),
        ["Sirene"] = typeof(Sirene),
        ["SireneBioLite"] = typeof(SireneBioLite),
        ["Terminal"] = typeof(Terminal),
        ["Templates"] = typeof(TemplateBio),
        ["Marcacao"] = typeof(Marcacao),
        ["Backup"] = typeof(MarcacaoBackup),
    };

    private static readonly HashSet<string> ColunasCartao = new(StringComparer.OrdinalIgnoreCase)
        { "Cracha", "Funcionario", "CodigoCartao" };

    public MdbImportReport Importar(MdbImportOptions opt, IProgress<MdbImportProgress>? progresso = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var report = new MdbImportReport();
        if (!File.Exists(opt.MdbPath)) throw new FileNotFoundException("Arquivo MDB não encontrado.", opt.MdbPath);
        var reader = MdbReaderLocator.Localizar(opt.ReaderPath)
            ?? throw new FileNotFoundException("MiniTime.MdbReader.exe não encontrado (leitor x86 do MDB).");

        var tmpDir = Path.Combine(Path.GetTempPath(), "MiniTimeImport-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        try
        {
            var copia = Path.Combine(tmpDir, "origem.mdb");
            progresso?.Report(new("", 0, 0, "Copiando o MDB para pasta temporária…"));
            File.Copy(opt.MdbPath, copia);

            using var proc = Iniciar(reader, copia, opt.Senha);
            var erros = new StringBuilder();
            proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) erros.AppendLine(e.Data); };
            proc.BeginErrorReadLine();

            using var c = db.Abrir();
            using var tx = c.BeginTransaction();
            try
            {
                if (opt.Substituir) Limpar(c, tx);
                Ler(proc, c, tx, opt, report, progresso, ct);
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                    throw new InvalidOperationException("Falha ao ler o MDB: " + erros.ToString().Trim());

                foreach (var t in report.Tabelas.Where(t => !t.Confere))
                    throw new InvalidOperationException($"Conferência falhou na tabela {t.Tabela}: origem {t.Origem}, importados {t.Importados}, quarentena {t.Quarentena}.");

                report.Duracao = sw.Elapsed;
                MiniTimeDb.Execute(c, tx, "INSERT INTO ImportacaoLog(DataHora, Origem, Resumo) VALUES(@d,@o,@r)",
                    ("d", DateTime.Now), ("o", opt.MdbPath), ("r", report.Resumo()));
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                if (!proc.HasExited) proc.Kill(true);
                throw;
            }
            return report;
        }
        finally
        {
            try { Directory.Delete(tmpDir, true); } catch (IOException) { /* temporário; ignora */ }
        }
    }

    private static Process Iniciar(string reader, string mdb, string senha)
    {
        var psi = new ProcessStartInfo(reader)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add(mdb);
        if (!string.IsNullOrEmpty(senha))
        {
            psi.ArgumentList.Add("--pwd");
            psi.ArgumentList.Add(senha);
        }
        return Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o leitor do MDB.");
    }

    private static void Limpar(SqliteConnection c, SqliteTransaction tx)
    {
        foreach (var t in Destinos.Values.Select(t => t.GetCustomAttribute<TableAttribute>()!.Name))
            MiniTimeDb.Execute(c, tx, $"DELETE FROM {t}");
        MiniTimeDb.Execute(c, tx, "DELETE FROM ImportacaoQuarentena");
        MiniTimeDb.Execute(c, tx, "DELETE FROM sqlite_sequence");
    }

    private void Ler(Process proc, SqliteConnection c, SqliteTransaction tx, MdbImportOptions opt,
        MdbImportReport report, IProgress<MdbImportProgress>? progresso, CancellationToken ct)
    {
        var maxima = opt.DataMaximaMarcacao ?? DateTime.Now.AddDays(1);
        MdbTableResult? atual = null;
        Destino? destino = null;
        long lidas = 0;
        var ignorarTabela = false;
        var ordem = new List<string>();

        while (proc.StandardOutput.ReadLine() is { } linha)
        {
            ct.ThrowIfCancellationRequested();
            if (linha.Length == 0) continue;
            using var doc = JsonDocument.Parse(linha);
            var raiz = doc.RootElement;

            if (raiz.TryGetProperty("event", out var ev))
            {
                switch (ev.GetString())
                {
                    case "tables":
                        foreach (var t in raiz.GetProperty("tables").EnumerateArray())
                        {
                            var nome = t.GetProperty("name").GetString()!;
                            if (!Destinos.ContainsKey(nome)) report.Avisos.Add($"Tabela '{nome}' do MDB ignorada (temporária/sem destino).");
                            else ordem.Add(nome);
                        }
                        foreach (var esperado in Destinos.Keys.Where(k => !ordem.Contains(k, StringComparer.OrdinalIgnoreCase)))
                            report.Avisos.Add($"Tabela '{esperado}' não existe no MDB.");
                        break;
                    case "table":
                        var n = raiz.GetProperty("name").GetString()!;
                        ignorarTabela = !Destinos.TryGetValue(n, out var tipo);
                        if (ignorarTabela) { atual = null; destino = null; break; }
                        atual = new MdbTableResult { Tabela = n, Origem = raiz.GetProperty("count").GetInt64() };
                        report.Tabelas.Add(atual);
                        destino = new Destino(tipo!, c, tx);
                        lidas = 0;
                        progresso?.Report(new(n, 0, atual.Origem, $"Importando {n}…"));
                        break;
                }
                continue;
            }

            if (ignorarTabela || atual is null || destino is null) continue;
            var linhaJson = raiz.GetProperty("r");
            Gravar(destino, atual, linhaJson, opt, maxima, c, tx);
            lidas++;
            if (lidas % 2000 == 0) progresso?.Report(new(atual.Tabela, lidas, atual.Origem, $"Importando {atual.Tabela}…"));
        }
        progresso?.Report(new("", 0, 0, "Concluído."));
    }

    private void Gravar(Destino d, MdbTableResult res, JsonElement linha, MdbImportOptions opt, DateTime maxima,
        SqliteConnection c, SqliteTransaction tx)
    {
        var fonte = new Dictionary<string, JsonElement>();
        foreach (var p in linha.EnumerateObject()) fonte[Normalizar(p.Name)] = p.Value;

        foreach (var nome in fonte.Keys.Where(k => !d.PorNormalizado.ContainsKey(k)))
        {
            var original = linha.EnumerateObject().First(p => Normalizar(p.Name) == nome).Name;
            if (!res.ColunasIgnoradas.Contains(original)) res.ColunasIgnoradas.Add(original);
        }

        var valores = new object?[d.Colunas.Length];
        for (var i = 0; i < d.Colunas.Length; i++)
        {
            var prop = d.Colunas[i];
            if (prop.Identidade) { valores[i] = DBNull.Value; continue; }
            fonte.TryGetValue(Normalizar(prop.Info.Name), out var json);
            try { valores[i] = Converter(prop, json, res.Tabela); }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                Quarentena(c, tx, $"Valor inválido em {prop.Info.Name}: {ex.Message}", res, linha);
                return;
            }
        }

        // Regras de validação por tabela
        if (res.Tabela is "Marcacao" or "Backup")
        {
            var dh = (DateTime)valores[Array.FindIndex(d.Colunas, x => x.Info.Name == "DataHora")]!;
            if (dh < opt.DataMinimaMarcacao || dh > maxima)
            {
                Quarentena(c, tx, $"Data/hora fora do intervalo aceitável ({dh:yyyy-MM-dd HH:mm})", res, linha);
                return;
            }
        }
        if (res.Tabela == "Parametros") valores[Array.FindIndex(d.Colunas, x => x.Info.Name == "Id")] = 1;

        for (var i = 0; i < valores.Length; i++) d.Comando.Parameters[i].Value = MiniTimeDb.Valor(valores[i]);
        try
        {
            if (d.Comando.ExecuteNonQuery() == 0) Quarentena(c, tx, "Chave duplicada", res, linha);
            else res.Importados++;
        }
        catch (SqliteException ex)
        {
            Quarentena(c, tx, "Erro ao gravar: " + ex.Message, res, linha);
        }
    }

    private static void Quarentena(SqliteConnection c, SqliteTransaction tx, string motivo, MdbTableResult res, JsonElement dados)
    {
        MiniTimeDb.Execute(c, tx, "INSERT INTO ImportacaoQuarentena(Tabela, Motivo, Dados) VALUES(@t,@m,@d)",
            ("t", res.Tabela), ("m", motivo), ("d", dados.GetRawText()));
        res.Quarentena++;
    }

    private static object? Converter(Coluna col, JsonElement json, string tabela)
    {
        var p = col.Info;
        var alvo = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        var nulo = json.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null;

        if (nulo)
        {
            if (alvo == typeof(string)) return col.NaoNulo ? "" : null;
            return alvo.IsValueType && Nullable.GetUnderlyingType(p.PropertyType) is null ? Activator.CreateInstance(alvo) : null;
        }

        if (alvo == typeof(string))
        {
            var s = json.ValueKind == JsonValueKind.String ? json.GetString()! : json.GetRawText();
            if (ColunasCartao.Contains(p.Name) && (tabela is "Marcacao" or "Backup" or "Funcionario" or "Ferias" or "Templates"))
                return CodigoCartao.Normalizar(s);
            return p.Name is "Descricao" or "Nome" ? s.Trim() : s;
        }
        if (alvo == typeof(bool))
            return json.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => json.GetDouble() != 0,
                _ => throw new FormatException("booleano esperado"),
            };
        if (alvo == typeof(int)) return json.ValueKind == JsonValueKind.Number ? (int)Math.Round(json.GetDouble()) : int.Parse(json.GetString()!, CultureInfo.InvariantCulture);
        if (alvo == typeof(long)) return json.ValueKind == JsonValueKind.Number ? (long)Math.Round(json.GetDouble()) : long.Parse(json.GetString()!, CultureInfo.InvariantCulture);
        if (alvo == typeof(DateTime)) return DateTime.ParseExact(json.GetString()!, MiniTimeDb.FormatoDataHora, CultureInfo.InvariantCulture);
        if (alvo == typeof(TimeSpan))
            return DateTime.ParseExact(json.GetString()!, MiniTimeDb.FormatoDataHora, CultureInfo.InvariantCulture).TimeOfDay;
        if (alvo == typeof(byte[]))
        {
            var s = json.GetString()!;
            return s.StartsWith("b64:", StringComparison.Ordinal) ? Convert.FromBase64String(s[4..]) : Encoding.UTF8.GetBytes(s);
        }
        throw new InvalidCastException($"Tipo não suportado: {alvo.Name}");
    }

    private static string Normalizar(string s) => s.Replace("_", "").ToLowerInvariant();

    private sealed record Coluna(PropertyInfo Info, bool Identidade, bool NaoNulo);

    /// <summary>Comando INSERT OR IGNORE preparado para uma entidade.</summary>
    private sealed class Destino
    {
        public Coluna[] Colunas { get; }
        public Dictionary<string, Coluna> PorNormalizado { get; }
        public SqliteCommand Comando { get; }

        public Destino(Type tipo, SqliteConnection c, SqliteTransaction tx)
        {
            var mapa = MiniTimeDb.Mapa.De(tipo);
            var nulabilidade = new NullabilityInfoContext();
            Colunas = mapa.Colunas.Select(p => new Coluna(p, p == mapa.Identidade,
                p.PropertyType == typeof(string) && nulabilidade.Create(p).WriteState == NullabilityState.NotNull)).ToArray();
            PorNormalizado = Colunas.ToDictionary(x => Normalizar(x.Info.Name));
            Comando = c.CreateCommand();
            Comando.Transaction = tx;
            // As colunas identity recebem NULL (autoincrement); mantemos todas na lista para alinhar índices.
            Comando.CommandText = $"INSERT OR IGNORE INTO {mapa.Tabela}({string.Join(",", Colunas.Select(x => x.Info.Name))}) " +
                                  $"VALUES({string.Join(",", Colunas.Select((_, i) => "@p" + i))})";
            for (var i = 0; i < Colunas.Length; i++) Comando.Parameters.Add(new SqliteParameter("@p" + i, null));
        }
    }
}

public static class MdbReaderLocator
{
    public const string NomeExe = "MiniTime.MdbReader.exe";

    public static string? Localizar(string? explicito = null)
    {
        if (!string.IsNullOrWhiteSpace(explicito)) return File.Exists(explicito) ? explicito : null;
        var baseDir = AppContext.BaseDirectory;
        foreach (var cand in new[] { Path.Combine(baseDir, "MdbReader", NomeExe), Path.Combine(baseDir, NomeExe) })
            if (File.Exists(cand)) return cand;

        // Desenvolvimento/testes: sobe até achar src\MiniTime.MdbReader\bin\*\net48
        for (var dir = new DirectoryInfo(baseDir); dir is not null; dir = dir.Parent)
        {
            var pasta = Path.Combine(dir.FullName, "src", "MiniTime.MdbReader", "bin");
            if (!Directory.Exists(pasta)) continue;
            var achado = Directory.EnumerateFiles(pasta, NomeExe, SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (achado is not null) return achado;
        }
        return null;
    }
}
