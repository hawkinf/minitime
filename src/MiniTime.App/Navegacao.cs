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
        new("_Utilitários", "_Importar dados do MDB antigo…", "importar", () => new ImportacaoMdbView()),
    ];
}
