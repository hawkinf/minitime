using System.Globalization;
using MiniTime.App.Controls;
using MiniTime.App.Services;
using MiniTime.Core.Models;

namespace MiniTime.App.Views;

/// <summary>Empresa, fechamento do mês e padrões do sistema (menu Arquivos → Configurações).</summary>
public sealed class ConfiguracoesView : EdicaoUnicaView<Parametros>
{
    public ConfiguracoesView() => Montar();

    protected override string Titulo => "Configurações";
    protected override string? Descricao => "Estes dados aparecem no cabeçalho do espelho de ponto. O sistema atende uma única empresa ou pessoa física.";

    protected override IReadOnlyList<Campo> Campos =>
    [
        new() { Rotulo = "Nome da empresa", Propriedade = nameof(Parametros.NomeCliente), MaxLength = 50, Largura = 380 },
        new() { Rotulo = "Pessoa", Propriedade = nameof(Parametros.TipoEmpresa), Tipo = TipoCampo.EscolhaTexto, Largura = 160,
                OpcoesTexto = [("J", "Jurídica"), ("F", "Física"), ("O", "Outros")] },
        new() { Rotulo = "CNPJ / CPF / Outros", Propriedade = nameof(Parametros.CNPJ), MaxLength = 19, Largura = 200 },
        new() { Rotulo = "Dia do fechamento", Propriedade = nameof(Parametros.DiaFechamento), Tipo = TipoCampo.Inteiro,
                Dica = "Último dia do período de apuração (ex.: 25 → de 26 a 25). Use 31 para o mês fechado no último dia." },
        new() { Grupo = "Tolerâncias sugeridas para novos horários (min)", Rotulo = "Entrada manhã", Propriedade = nameof(Parametros.TolManha), Tipo = TipoCampo.Inteiro },
        new() { Grupo = "Tolerâncias sugeridas para novos horários (min)", Rotulo = "Retorno tarde", Propriedade = nameof(Parametros.TolTarde), Tipo = TipoCampo.Inteiro },
        new() { Grupo = "Tolerâncias sugeridas para novos horários (min)", Rotulo = "Saída", Propriedade = nameof(Parametros.TolSaida), Tipo = TipoCampo.Inteiro },
    ];

    protected override Parametros Carregar() => Sessao.Repos.Parametros.Obter();
    protected override void Salvar(Parametros e) => Sessao.Repos.Parametros.Salvar(e);

    protected override string? Validar(Parametros e)
    {
        if (string.IsNullOrWhiteSpace(e.NomeCliente)) return "Informe o nome da empresa.";
        if (e.DiaFechamento is < 1 or > 31 || e.DiaFechamento is 28 or 29 or 30)
            return "Dia de fechamento inválido: use de 1 a 27 ou 31 (os dias 28, 29 e 30 não existem em todos os meses).";
        return null;
    }
}

/// <summary>Relógio ligado ao computador: endereço, porta e faixa horária de marcações.</summary>
public sealed class RelogioView : EdicaoUnicaView<Terminal>
{
    public RelogioView() => Montar();

    protected override string Titulo => "Relógio";
    protected override string? Descricao => "Informe o número do relógio, a porta serial (ou a porta COM do adaptador USB-serial) e a faixa horária em que ele aceita marcações.";

    protected override IReadOnlyList<Campo> Campos
    {
        get
        {
            var portas = Enumerable.Range(1, 32).Select(i => (i, $"COM{i}")).ToList();
            return
            [
                new() { Rotulo = "Número do relógio", Propriedade = nameof(Terminal.Endereco), Tipo = TipoCampo.Inteiro, Dica = "1 a 99" },
                new() { Rotulo = "Porta serial", Propriedade = nameof(Terminal.Porta), Tipo = TipoCampo.Escolha, Opcoes = portas, Largura = 120 },
                new() { Rotulo = "Faixa horária: início", Propriedade = nameof(Terminal.FaixaInicio), Tipo = TipoCampo.Hora },
                new() { Rotulo = "Faixa horária: fim", Propriedade = nameof(Terminal.FaixaFim), Tipo = TipoCampo.Hora },
                new() { Rotulo = "Versão do firmware", Propriedade = nameof(Terminal.VersaoFW), MaxLength = 4, Largura = 80 },
                new() { Rotulo = "Possui impressora", Propriedade = nameof(Terminal.Impressora), Tipo = TipoCampo.Booleano },
                new() { Rotulo = "Possui pen drive", Propriedade = nameof(Terminal.PenDrive), Tipo = TipoCampo.Booleano },
            ];
        }
    }

    private int _enderecoOriginal;

    protected override Terminal Carregar()
    {
        var t = Sessao.Repos.Terminais.Todos("Endereco").FirstOrDefault()
                ?? new Terminal { Endereco = 1, Porta = 1, FaixaInicio = TimeSpan.Zero, FaixaFim = new TimeSpan(23, 59, 0) };
        _enderecoOriginal = t.Endereco;
        return t;
    }

    protected override string? Validar(Terminal e)
        => e.Endereco is < 1 or > 99 ? "O número do relógio deve estar entre 1 e 99." : null;

    protected override void Salvar(Terminal e)
    {
        // Um único relógio é suportado: troca o registro anterior pelo novo.
        Sessao.Db.Execute("DELETE FROM Terminal");
        Sessao.Repos.Terminais.Inserir(e);
        _enderecoOriginal = e.Endereco;
    }
}

/// <summary>Parâmetros enviados ao relógio: quantidade de dígitos do cartão e checagem.</summary>
public sealed class ParametrosRelogioView : EdicaoUnicaView<Parametros>
{
    public ParametrosRelogioView() => Montar();

    protected override string Titulo => "Parâmetros do relógio";
    protected override string? Descricao => "Configurações transmitidas ao relógio. Para cartões de código de barras use 4, 5 ou 6 dígitos; para proximidade, 14 dígitos sem checagem.";

    protected override IReadOnlyList<Campo> Campos =>
    [
        new() { Rotulo = "Quantidade de dígitos", Propriedade = nameof(Parametros.QtdeDig), Tipo = TipoCampo.EscolhaTexto, Largura = 100,
                OpcoesTexto = [("04", "4"), ("05", "5"), ("06", "6"), ("14", "14")] },
        new() { Rotulo = "Checagem do último dígito", Propriedade = nameof(Parametros.Checagem), Tipo = TipoCampo.Booleano },
        new() { Rotulo = "Velocidade da porta (bps)", Propriedade = nameof(Parametros.Velocidade), Tipo = TipoCampo.Escolha, Largura = 120,
                Opcoes = [(9600, "9600"), (19200, "19200"), (38400, "38400"), (57600, "57600"), (115200, "115200")] },
    ];

    protected override Parametros Carregar() => Sessao.Repos.Parametros.Obter();
    protected override void Salvar(Parametros e) => Sessao.Repos.Parametros.Salvar(e);
}

/// <summary>Horário de verão: ativa/desativa e define início/fim (DD/MM).</summary>
public sealed class VeraoView : EdicaoUnicaView<VeraoView.Dados>
{
    public sealed class Dados
    {
        public bool Ativo { get; set; }
        public string Inicio { get; set; } = "";
        public string Fim { get; set; } = "";
    }

    public VeraoView() => Montar();

    protected override string Titulo => "Horário de verão";
    protected override string? Descricao => "Com a checagem ativa, o relógio adianta uma hora à 0h do dia inicial e atrasa uma hora à 0h do dia final (ex.: início 20/10, fim 20/02).";

    protected override IReadOnlyList<Campo> Campos =>
    [
        new() { Rotulo = "Checar horário de verão", Propriedade = nameof(Dados.Ativo), Tipo = TipoCampo.Booleano },
        new() { Rotulo = "Início (DD/MM)", Propriedade = nameof(Dados.Inicio), MaxLength = 5, Largura = 80 },
        new() { Rotulo = "Fim (DD/MM)", Propriedade = nameof(Dados.Fim), MaxLength = 5, Largura = 80 },
    ];

    // O banco guarda "DDMM" (ex.: "1610").
    private static string Formatar(string? ddmm)
        => ddmm is { Length: >= 4 } s && s.Take(4).All(char.IsDigit) ? $"{s[..2]}/{s.Substring(2, 2)}" : "";

    private static bool TentarLer(string txt, out string ddmm)
    {
        ddmm = "";
        var limpo = new string(txt.Where(char.IsDigit).ToArray());
        if (limpo.Length != 4) return false;
        var dia = int.Parse(limpo[..2], CultureInfo.InvariantCulture);
        var mes = int.Parse(limpo[2..], CultureInfo.InvariantCulture);
        if (mes is < 1 or > 12 || dia < 1 || dia > DateTime.DaysInMonth(2024, mes)) return false;
        ddmm = limpo;
        return true;
    }

    protected override Dados Carregar()
    {
        var p = Sessao.Repos.Parametros.Obter();
        return new Dados { Ativo = p.HorarioVeraoAtivo, Inicio = Formatar(p.InicioHorarioVerao), Fim = Formatar(p.FimHorarioVerao) };
    }

    protected override string? Validar(Dados e)
    {
        if (!e.Ativo && e.Inicio.Length == 0 && e.Fim.Length == 0) return null;
        if (!TentarLer(e.Inicio, out _)) return "Data de início inválida. Use DD/MM.";
        if (!TentarLer(e.Fim, out _)) return "Data de fim inválida. Use DD/MM.";
        return null;
    }

    protected override void Salvar(Dados e)
    {
        var p = Sessao.Repos.Parametros.Obter();
        p.HorarioVeraoAtivo = e.Ativo;
        p.InicioHorarioVerao = TentarLer(e.Inicio, out var i) ? i : null;
        p.FimHorarioVerao = TentarLer(e.Fim, out var f) ? f : null;
        Sessao.Repos.Parametros.Salvar(p);
    }
}
