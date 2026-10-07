# AGENT_RULES — minitime

Port do MiniTime (Dimep, VB6, controle de cartão de ponto via serial) para C# WPF + SQLite.
O código VB original não existe: `source/MiniTime.bas` é saída degradada do VB Decompiler.
Especificações reconstruídas em `docs/spec/`.

## Regras
1. Anti-regressão é a prioridade nº 1; commit ao fim de cada passo.
2. Lógica sem UI em `MiniTime.Core` / `MiniTime.Data` / `MiniTime.Serial` (testáveis). `MiniTime.App` só telas.
3. O MDB original nunca é aberto para escrita; importação trabalha sobre cópia ou em modo leitura.
4. Nunca mostrar exceção crua ao usuário; mensagem amigável + log.
5. Sem `Console.WriteLine` em produção.
6. A senha do MDB legado NUNCA vai para o repositório: vem de `--pwd`, da variável `MINITIME_MDB_PWD` ou das configurações locais do usuário.

## Validação antes de commit
- `dotnet build MiniTime.sln` 0 erros
- `dotnet test` verde

## Mapa
| Preciso mexer em | Onde |
|---|---|
| modelos, apuração, regras | `src/MiniTime.Core` |
| SQLite, migrations, repositórios, importação | `src/MiniTime.Data` |
| leitor do MDB (x86, net48, Jet) | `src/MiniTime.MdbReader` |
| protocolo do relógio | `src/MiniTime.Serial` |
| telas WPF | `src/MiniTime.App` |
| testes | `tests/MiniTime.Tests` |

## Commit
`<AREA>: <type> - <descrição>` (feat|fix|refactor|test|chore|mig)
