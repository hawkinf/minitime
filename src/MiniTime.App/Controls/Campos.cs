using System.Globalization;
using System.Windows.Data;
using MiniTime.Core.Util;

namespace MiniTime.App.Controls;

/// <summary>Converte "07:20", "0720" ou "7:20" ⇄ TimeSpan?. Vazio = null.</summary>
public sealed class HoraConverter : IValueConverter
{
    public static readonly HoraConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is TimeSpan t ? $"{(int)t.TotalHours:00}:{t.Minutes:00}" : "";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = (value as string ?? "").Trim();
        if (s.Length == 0) return null;
        if (!Hora.TentarLer(s, out var t))
            throw new FormatException("Hora inválida. Use HH:MM (ex.: 07:20 ou 0720).");
        return t;
    }
}

/// <summary>Minutos (int) ⇄ "HH:MM"; vazio = 0.</summary>
public sealed class MinutosConverter : IValueConverter
{
    public static readonly MinutosConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int m ? Hora.Minutos(m) : "";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = (value as string ?? "").Trim();
        if (s.Length == 0) return 0;
        var p = s.Split(':');
        if (p.Length == 2 && int.TryParse(p[0], out var h) && int.TryParse(p[1], out var m) && h >= 0 && m is >= 0 and < 60) return h * 60 + m;
        if (p.Length == 1 && int.TryParse(p[0], out var h2) && h2 >= 0) return h2 * 60;
        throw new FormatException("Duração inválida. Use HH:MM (ex.: 44:00).");
    }
}

public enum TipoCampo { Texto, Inteiro, Hora, Duracao, Booleano, Data, Escolha, EscolhaTexto }

/// <summary>Descrição de um campo do formulário de cadastro.</summary>
public sealed class Campo
{
    public required string Rotulo { get; init; }
    public required string Propriedade { get; init; }
    public TipoCampo Tipo { get; init; } = TipoCampo.Texto;
    public int Largura { get; init; } = 260;
    public int MaxLength { get; init; }
    /// <summary>Somente leitura ao editar um registro existente (ex.: o código).</summary>
    public bool SomenteNovo { get; init; }
    /// <summary>Para TipoCampo.Escolha: (valor, texto). O valor é int.</summary>
    public IReadOnlyList<(int Valor, string Texto)>? Opcoes { get; init; }
    /// <summary>Para TipoCampo.EscolhaTexto: (valor, texto) com valor string.</summary>
    public IReadOnlyList<(string Valor, string Texto)>? OpcoesTexto { get; init; }
    public string? Dica { get; init; }
    public string? Grupo { get; init; }
}

public sealed record ColunaGrid(string Titulo, string Caminho, double Largura = 100, string? Formato = null, IValueConverter? Conversor = null);
