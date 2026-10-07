using MiniTime.Core.Models;

namespace MiniTime.Core.Apuracao;

/// <summary>Códigos persistidos em Marcacao.Tipo (valores herdados do sistema legado).</summary>
public static class TipoMarcacao
{
    /// <summary>Coletada do relógio (ou importada de arquivo).</summary>
    public const int Coletada = 7;
    /// <summary>Inserida manualmente pelo usuário.</summary>
    public const int Manual = 0;
    /// <summary>Gerada pela apuração do sistema legado (falta, divergência justificada). Ignorada pelo motor novo.</summary>
    public const int Gerada = 256;
    /// <summary>Marcação coletada que o usuário desprezou.</summary>
    public const int Desprezada = 263;

    /// <summary>Justificativa gravada pelo legado em marcações inseridas automaticamente.</summary>
    public const int JustificativaAutomatica = -1;
}

/// <summary>Posição da marcação no dia (Marcacao.EntradaSaida).</summary>
public static class Posicao
{
    public const int Ignorada = -1;
    public const int EntradaManha = 1;
    public const int SaidaIntervalo = 2;
    public const int RetornoIntervalo = 3;
    public const int Saida = 4;
    public const int EntradaExtra = 5;
    public const int SaidaExtra = 6;
    /// <summary>Registro de status do dia (falta/abono), não é uma batida.</summary>
    public const int StatusDia = 8;
}

/// <summary>Situação da marcação (Marcacao.Situacao).</summary>
public static class SituacaoMarcacao
{
    public const int NaoTratada = 0;
    public const int Normal = 1;
    public const int Justificada = 3;
    public const int Divergente = 4;
}

/// <summary>Tipo de divergência (Marcacao.Divergencia).</summary>
public static class TipoDivergencia
{
    public const int Nenhuma = 0;
    public const int Atraso = 1;
    public const int SaidaAntecipada = 2;
}

public enum StatusDia
{
    /// <summary>Dia de expediente com marcações.</summary>
    Normal,
    Falta,
    MeiaFalta,
    /// <summary>Marcações incompletas (ex.: entrada sem saída).</summary>
    Pendencia,
    SemExpediente,
    Dsr,
    Feriado,
    Ferias,
    /// <summary>Falta abonada por justificativa.</summary>
    Abonado,
    /// <summary>Dia útil sem marcações, mas a jornada não marca falta.</summary>
    SemMarcacao,
    Futuro,
}

/// <summary>Uma marcação depois de classificada pela apuração.</summary>
public sealed class MarcacaoApurada
{
    public required Marcacao Origem { get; init; }
    /// <summary>Hora da batida (minutos desde 00:00 do dia de apuração; pode passar de 1440 em turno noturno).</summary>
    public int Minuto { get; init; }
    public int Posicao { get; set; } = MiniTime.Core.Apuracao.Posicao.Ignorada;
    public int Situacao { get; set; }
    public int Divergencia { get; set; }
    /// <summary>Hora considerada nos cálculos (após tolerâncias); null se ignorada.</summary>
    public int? Considerada { get; set; }
    public int Justificativa { get; set; }
}

/// <summary>Resultado da apuração de um dia de um funcionário.</summary>
public sealed class DiaApurado
{
    public DateTime Data { get; init; }
    public StatusDia Status { get; set; }
    public Horario? Horario { get; set; }
    public List<MarcacaoApurada> Marcacoes { get; } = [];
    /// <summary>Descrição da justificativa que abonou o dia, se houver.</summary>
    public string? JustificativaDia { get; set; }

    // Horas reais (batidas) por coluna do espelho; null = sem batida.
    public int? EntManha { get; set; }
    public int? SaiManha { get; set; }
    public int? EntTarde { get; set; }
    public int? SaiTarde { get; set; }
    public int? EntExtra { get; set; }
    public int? SaiExtra { get; set; }

    /// <summary>Justificativa de cada coluna (código; 0 = nenhuma).</summary>
    public int JustEntManha { get; set; }
    public int JustSaiManha { get; set; }
    public int JustEntTarde { get; set; }
    public int JustSaiTarde { get; set; }

    /// <summary>Minutos esperados pela jornada.</summary>
    public int Esperado { get; set; }
    public int Trabalhado { get; set; }
    public int Atraso { get; set; }
    public int Extra { get; set; }
    public int Abonado { get; set; }
    public int AdicionalNoturno { get; set; }
    /// <summary>Total de minutos em que o dia teve divergência não justificada (atraso + saída antecipada).</summary>
    public int Divergencias { get; set; }
}

/// <summary>Totais do período para o cabeçalho/rodapé do espelho.</summary>
public sealed class TotaisApuracao
{
    public int Esperado { get; set; }
    public int Trabalhado { get; set; }
    public int Atraso { get; set; }
    public int Extra { get; set; }
    public int Abonado { get; set; }
    public int AdicionalNoturno { get; set; }
    public int Faltas { get; set; }
    public int MeiasFaltas { get; set; }
    public int DiasDsr { get; set; }
    public int Pendencias { get; set; }
    /// <summary>Saldo = trabalhado + abonado - esperado (positivo = crédito).</summary>
    public int Saldo => Trabalhado + Abonado - Esperado;
}

public sealed class ResultadoApuracao
{
    public required Funcionario Funcionario { get; init; }
    public Jornada? Jornada { get; init; }
    public List<DiaApurado> Dias { get; } = [];
    public TotaisApuracao Totais { get; } = new();
}
