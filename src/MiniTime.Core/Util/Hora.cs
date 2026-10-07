using System.Globalization;

namespace MiniTime.Core.Util;

/// <summary>Leitura/formatação de horas no estilo do sistema legado (07:20, 0720, 7:20) e minutos ⇄ HH:MM.</summary>
public static class Hora
{
    public static bool TentarLer(string? texto, out TimeSpan hora)
    {
        hora = default;
        var s = (texto ?? "").Trim();
        if (s.Length == 0) return false;
        int h, m;
        if (s.Contains(':'))
        {
            var p = s.Split(':');
            if (p.Length != 2 || !int.TryParse(p[0], NumberStyles.None, CultureInfo.InvariantCulture, out h)
                || !int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out m)) return false;
        }
        else
        {
            if (s.Length is < 3 or > 4 || !s.All(char.IsDigit)) return false;
            var n = int.Parse(s, CultureInfo.InvariantCulture);
            h = n / 100; m = n % 100;
        }
        if (h is < 0 or > 23 || m is < 0 or > 59) return false;
        hora = new TimeSpan(h, m, 0);
        return true;
    }

    /// <summary>Minutos totais → "HH:MM" (horas podem passar de 24; negativo recebe '-').</summary>
    public static string Minutos(int minutos)
    {
        var sinal = minutos < 0 ? "-" : "";
        minutos = Math.Abs(minutos);
        return $"{sinal}{minutos / 60:00}:{minutos % 60:00}";
    }

    public static string Formatar(TimeSpan? t) => t is { } v ? $"{(int)v.TotalHours:00}:{v.Minutes:00}" : "";
}
