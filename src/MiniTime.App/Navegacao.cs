using System.Windows.Controls;
using MiniTime.App.Views;

namespace MiniTime.App;

/// <summary>Item de menu: grupo, título, chave da aba (evita duplicar), fábrica da tela e nível mínimo de acesso.</summary>
public sealed record MenuEntrada(string Grupo, string Titulo, string Chave, Func<UserControl> Criar, int Nivel = 1);

public static class Navegacao
{
    internal static MainWindow Janela { get; set; } = null!;

    public static void Abrir(string titulo, string chave, Func<UserControl> criar) => Janela.AbrirAba(titulo, chave, criar);
    public static void Fechar(string chave) => Janela.FecharAba(chave);
    public static void FecharTodas() => Janela.FecharTodas();
    public static void Status(string texto) => Janela.Status(texto);
}

/// <summary>
/// Estrutura do menu principal. Nível de acesso: 0 = consulta (relatórios), 1 = completo, 2 = administrador.
/// </summary>
public static class Menus
{
    public static List<MenuEntrada> Itens() =>
    [
        new("_Arquivos", "_Cartões…", "cartoes", () => new FuncionariosView()),
        new("_Arquivos", "_Horários de Trabalho…", "horarios", () => new HorariosView()),
        new("_Arquivos", "_Jornadas…", "jornadas", () => new JornadasView()),
        new("_Arquivos", "_Feriados…", "feriados", () => new FeriadosView()),
        new("_Arquivos", "_Justificativas…", "justificativas", () => new JustificativasView()),
        new("_Arquivos", "_Alarmes (sirene)…", "alarmes", () => new AlarmesView()),
        new("_Arquivos", "C_onfigurações…", "config", () => new ConfiguracoesView()),
        new("_Arquivos", "_Relógio…", "relogio", () => new RelogioView()),
        new("_Arquivos", "Horário de _verão…", "verao", () => new VeraoView()),
        new("_Arquivos", "_Senhas…", "senhas", () => new SenhasView(), Nivel: 2),
        new("C_omunicação", "_Coleta / Monitoração…", "coleta", () => new ColetaView()),
        new("C_omunicação", "_Inicialização geral / Status…", "inicializacao", () => new InicializacaoView()),
        new("C_omunicação", "_Parâmetros do relógio…", "paramrelogio", () => new ParametrosRelogioView()),
        new("_Relatório", "_Espelho de ponto / Apuração…", "espelho", () => new EspelhoView(), Nivel: 0),
        new("_Relatório", "_Listagem de marcações…", "relmarcacoes", () => new MarcacoesRelatorioView(), Nivel: 0),
        new("_Relatório", "Relatórios de _cadastros…", "relcadastros", () => new ListagensView(), Nivel: 0),
        new("_Utilitários", "_Exportação de marcações…", "exportacao", () => new ExportacaoView()),
        new("_Utilitários", "_Parâmetros de exportação…", "paramexp", () => new ParametrosExportacaoView()),
        new("_Utilitários", "Importar marcações de _arquivo…", "importararquivo", () => new ImportacaoArquivoView()),
        new("_Utilitários", "_Importar dados do MDB antigo…", "importar", () => new ImportacaoMdbView(), Nivel: 2),
        new("_Utilitários", "_Backup e manutenção do banco…", "manutencao", () => new ManutencaoView(), Nivel: 2),
    ];
}
