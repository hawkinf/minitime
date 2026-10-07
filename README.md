# MiniTime

Reescrita em **C# / WPF (.NET 8) + SQLite** do MiniTime (Dimep), um programa de controle de cartão de ponto
feito originalmente em VB6 para relógios ligados à porta serial (Mini Point). Agora funciona com adaptador
USB‑serial e banco SQLite, e traz um módulo para **importar o banco MDB (Access) antigo**.

> Projeto independente, feito para uso próprio. Não é afiliado à Dimep. O código VB original não faz parte
> deste repositório.

## Estado atual

| Área | Situação |
|---|---|
| Camada SQLite (esquema, migrations, repositórios) | pronta, com testes |
| Importador MDB → SQLite (leitura x86, quarentena, conferência de contagens) | pronto, testado com banco real |
| Shell WPF (abas, menu, tela de importação) | pronto |
| Cadastros, apuração, comunicação serial, relatórios, exportação | em andamento |

## Como compilar

```powershell
dotnet build MiniTime.sln
dotnet test
```

Requer o SDK .NET 8 (ou superior). O leitor de MDB (`MiniTime.MdbReader`, .NET Framework 4.8, x86) usa o
provedor Jet 4.0 que acompanha o Windows; ele é copiado para `MdbReader\` ao lado do executável.

## Importando o MDB antigo

Menu **Utilitários → Importar dados do MDB antigo**. O arquivo original nunca é alterado: é copiado para uma
pasta temporária e lido em modo somente leitura. A importação é atômica (tudo ou nada) e registros inválidos
(por exemplo marcações com data absurda) vão para a tabela `ImportacaoQuarentena` em vez de se perderem.

Se o MDB tiver senha, informe-a na tela (fica salva só no computador do usuário) ou na variável de ambiente
`MINITIME_MDB_PWD`. **Nenhuma senha é versionada neste repositório.**

## Estrutura

```
src/MiniTime.Core       modelos e regras
src/MiniTime.Data       SQLite + importador
src/MiniTime.MdbReader  leitor de MDB (net48, x86)
src/MiniTime.Serial     comunicação com o relógio
src/MiniTime.App        interface WPF
tests/MiniTime.Tests    testes xUnit
docs/spec               especificações reconstruídas
```
