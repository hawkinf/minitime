using System.IO;
using System.Text.Json;
using MiniTime.Data;

namespace MiniTime.App.Services;

/// <summary>Configurações locais do programa (não ficam no banco): caminho do banco, porta serial etc.</summary>
public sealed class AppSettings
{
    public string CaminhoBanco { get; set; } = Path.Combine(Pastas.Dados, "minitime.db");
    public string? PortaSerial { get; set; }
    /// <summary>Senha do MDB legado, guardada só neste computador (nunca no repositório).</summary>
    public string? SenhaMdb { get; set; }

    private static string Arquivo => Path.Combine(Pastas.Config, "settings.json");

    public static AppSettings Carregar()
    {
        try
        {
            if (File.Exists(Arquivo))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Arquivo)) ?? new();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            Log.Erro("Falha ao ler settings.json; usando padrão.", ex);
        }
        return new();
    }

    public void Salvar()
    {
        Directory.CreateDirectory(Pastas.Config);
        File.WriteAllText(Arquivo, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public static class Pastas
{
    public static string Dados { get; } = Garantir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiniTime"));
    public static string Config { get; } = Garantir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MiniTime"));
    public static string Logs { get; } = Garantir(Path.Combine(Dados, "logs"));

    private static string Garantir(string p) { Directory.CreateDirectory(p); return p; }
}

public static class Log
{
    private static readonly object Trava = new();
    private static string Arquivo => Path.Combine(Pastas.Logs, $"minitime-{DateTime.Now:yyyyMMdd}.log");

    public static void Info(string msg) => Escrever("INFO ", msg);
    public static void Erro(string msg, Exception? ex = null) => Escrever("ERRO ", ex is null ? msg : $"{msg}\n{ex}");

    private static void Escrever(string nivel, string msg)
    {
        try
        {
            lock (Trava) File.AppendAllText(Arquivo, $"{DateTime.Now:HH:mm:ss.fff} {nivel} {msg}{Environment.NewLine}");
        }
        catch (IOException) { /* log nunca derruba o programa */ }
    }
}

/// <summary>Estado global da sessão: banco aberto e repositórios.</summary>
public static class Sessao
{
    public static AppSettings Settings { get; private set; } = new();
    public static MiniTimeDb Db { get; private set; } = null!;
    public static Repositorios Repos { get; private set; } = null!;
    /// <summary>Usuário autenticado (null = acesso livre, nenhum usuário cadastrado).</summary>
    public static MiniTime.Core.Models.Usuario? Usuario { get; set; }

    public static void Iniciar()
    {
        Settings = AppSettings.Carregar();
        // MINITIME_DB permite apontar para outro banco (testes/suporte) sem mexer nas configurações.
        var forcado = Environment.GetEnvironmentVariable("MINITIME_DB");
        Abrir(string.IsNullOrWhiteSpace(forcado) ? Settings.CaminhoBanco : forcado);
    }

    public static void Abrir(string caminho)
    {
        var dir = Path.GetDirectoryName(caminho);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        Db = new MiniTimeDb(caminho);
        Repos = new Repositorios(Db);
        Settings.CaminhoBanco = caminho;
        Log.Info($"Banco aberto: {caminho}");
    }
}
