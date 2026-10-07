namespace MiniTime.Core.Models;

[Table("Funcionario")]
public sealed class Funcionario
{
    /// <summary>Número do cartão/crachá, sempre com 16 posições (zeros à esquerda).</summary>
    [Key] public string Codigo { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? Cargo { get; set; }
    public string? RG { get; set; }
    /// <summary>Código da jornada (Jornadas.Codigo).</summary>
    public int Horario { get; set; }
    public int HorarioAlmoco { get; set; }
    public int AutHoraExtra { get; set; }
    public TimeSpan? HrMudancaData { get; set; }
    public bool HorarioNoturno { get; set; }
    public bool HorarioDiurno { get; set; }
    public TimeSpan? HrMudancaDataDiaLivre { get; set; }
    public int SentidoMudancaDataDiaLivre { get; set; }
    public string? Senha { get; set; }

    [NotMapped] public string CodigoCurto => Util.CodigoCartao.Curto(Codigo);
}

[Table("Horarios")]
public sealed class Horario
{
    [Key] public int Codigo { get; set; }
    public string Descricao { get; set; } = "";
    /// <summary>Entrada manhã.</summary>
    public TimeSpan? DeSS1 { get; set; }
    /// <summary>Saída manhã.</summary>
    public TimeSpan? DeSS2 { get; set; }
    /// <summary>Entrada tarde.</summary>
    public TimeSpan? DeSS3 { get; set; }
    /// <summary>Saída tarde.</summary>
    public TimeSpan? DeSS4 { get; set; }
    public int TolManha { get; set; }
    public int TolTarde { get; set; }
    public int TolSaida { get; set; }
    public int RefObrig { get; set; }
    public int RefMinimo { get; set; }
    public int Intervalo { get; set; }
    public int Noturno { get; set; }
    public int NaoTrabalha { get; set; }
    public int TolExtEnt { get; set; }
    public int TolExtInt { get; set; }
    public int TolExtSai { get; set; }
}

[Table("Jornadas")]
public sealed class Jornada
{
    [Key] public int Codigo { get; set; }
    public string Descricao { get; set; } = "";
    /// <summary>Código do horário (Horarios.Codigo) usado em cada dia da semana; 0 = sem horário.</summary>
    public int Segunda { get; set; }
    public int Terca { get; set; }
    public int Quarta { get; set; }
    public int Quinta { get; set; }
    public int Sexta { get; set; }
    public int Sabado { get; set; }
    public int Domingo { get; set; }
    public int NaoMarcarFalta { get; set; }
    public int TrataDSR { get; set; }
    public int JornSemanal { get; set; }
    public int DiaDSR { get; set; }

    public int HorarioDoDia(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => Segunda,
        DayOfWeek.Tuesday => Terca,
        DayOfWeek.Wednesday => Quarta,
        DayOfWeek.Thursday => Quinta,
        DayOfWeek.Friday => Sexta,
        DayOfWeek.Saturday => Sabado,
        _ => Domingo,
    };
}

[Table("Feriados")]
public sealed class Feriado
{
    [Key] public int Codigo { get; set; }
    public string Descricao { get; set; } = "";
    public int Dia { get; set; }
    public int Mes { get; set; }
}

[Table("Ferias")]
public sealed class Ferias
{
    [Key, Identity] public long Id { get; set; }
    public string Funcionario { get; set; } = "";
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }
}

[Table("Justificativas")]
public sealed class Justificativa
{
    [Key] public int Codigo { get; set; }
    public string Descricao { get; set; } = "";
    public int Tipo { get; set; }
}

[Table("Marcacao")]
public sealed class Marcacao
{
    [Key, Identity] public long Id { get; set; }
    public string Cracha { get; set; } = "";
    public DateTime DataHora { get; set; }
    public int Terminal { get; set; }
    public int EntradaSaida { get; set; }
    public int Situacao { get; set; }
    public int Tipo { get; set; }
    public int Divergencia { get; set; }
    public bool SaiuMarcacao { get; set; }
    public int Justificativa { get; set; }
}

/// <summary>Cópia bruta das marcações coletadas (antes da apuração).</summary>
[Table("Backup")]
public sealed class MarcacaoBackup
{
    [Key, Identity] public long Id { get; set; }
    public string Cracha { get; set; } = "";
    public DateTime DataHora { get; set; }
    public int Terminal { get; set; }
    public int EntradaSaida { get; set; }
    public int Situacao { get; set; }
    public int Tipo { get; set; }
    public int Divergencia { get; set; }
    public bool SaiuMarcacao { get; set; }
}

[Table("Sirene")]
public sealed class Sirene
{
    [Key] public int Codigo { get; set; }
    public string Descricao { get; set; } = "";
    public TimeSpan? Horario { get; set; }
    public int Duracao { get; set; }
    public bool Util { get; set; }
    public bool FimSem { get; set; }
}

[Table("SireneBioLite")]
public sealed class SireneBioLite
{
    [Key] public int Codigo { get; set; }
    public string Descricao { get; set; } = "";
    public TimeSpan? Horario { get; set; }
    public bool Util { get; set; }
    public bool FimSem { get; set; }
}

[Table("Terminal")]
public sealed class Terminal
{
    [Key] public int Endereco { get; set; }
    public int Porta { get; set; }
    public TimeSpan? FaixaInicio { get; set; }
    public TimeSpan? FaixaFim { get; set; }
    public string? VersaoFW { get; set; }
    public bool Impressora { get; set; }
    public bool PenDrive { get; set; }
}

[Table("Templates")]
public sealed class TemplateBio
{
    [Key] public string CodigoCartao { get; set; } = "";
    public string Dedo { get; set; } = "";
    public byte[]? Template { get; set; }
}

/// <summary>Linha única de configuração (tabela Parametros).</summary>
[Table("Parametros")]
public sealed class Parametros
{
    [Key] public int Id { get; set; } = 1;
    public string NomeCliente { get; set; } = "";
    public string? Digitos { get; set; }
    public string? UltUsuario { get; set; }
    public string? CNPJ { get; set; }
    public int DiaFechamento { get; set; }
    public int TolManha { get; set; }
    public int TolTarde { get; set; }
    public int TolSaida { get; set; }
    public int TipoExp { get; set; }
    public int NumCartao { get; set; } = 5;
    public int NumDigAno { get; set; } = 2;
    public int DigVerificador { get; set; }
    public int FiltroExp { get; set; }
    public int Velocidade { get; set; } = 19200;
    public int DisqVirtual { get; set; }
    public int Versao { get; set; } = 441;
    public bool HorarioVeraoAtivo { get; set; }
    public string? InicioHorarioVerao { get; set; }
    public string? FimHorarioVerao { get; set; }
    public string TipoRelogio { get; set; } = "Mini Point";
    public string QtdeDig { get; set; } = "04";
    public bool Checagem { get; set; }
    public bool ExpMonitoracao { get; set; }
    public string? NomeArq { get; set; }
    public string? TipoEmpresa { get; set; }
    public int DuracaoSireneBioLite { get; set; }
}
