using System.Windows.Controls;
using MiniTime.App.Views;

namespace MiniTime.App;

/// <summary>Item de menu: grupo, título, chave da aba (evita duplicar) e fábrica da tela.</summary>
public sealed record MenuEntrada(string Grupo, string Titulo, string Chave, Func<UserControl> Criar);

public static class Navegacao
{
    internal static MainWindow Janela { get; set; } = null!;

    public static void Abrir(string titulo, string chave, Func<UserControl> criar) => Janela.AbrirAba(titulo, chave, criar);
    public static void Fechar(string chave) => Janela.FecharAba(chave);
    public static void Status(string texto) => Janela.Status(texto);
}

/// <summary>Estrutura do menu principal. Telas novas entram aqui.</summary>
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
        new("C_omunicação", "_Coleta / Monitoração…", "coleta", () => new ColetaView()),
        new("C_omunicação", "_Parâmetros do relógio…", "paramrelogio", () => new ParametrosRelogioView()),
        new("_Utilitários", "_Importar dados do MDB antigo…", "importar", () => new ImportacaoMdbView()),
    ];
}
