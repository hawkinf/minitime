# MiniTime

Reescrita em **C# / WPF (.NET 10) + SQLite** do MiniTime (Dimep), um programa de controle de cartão de ponto
feito originalmente em VB6 para relógios ligados à porta serial (Mini Point). Agora funciona com adaptador
USB‑serial e banco SQLite, e traz um módulo para **importar o banco MDB (Access) antigo**.

> Projeto independente, feito para uso próprio. Não é afiliado à Dimep. O código VB original não faz parte
> deste repositório.

## O que já funciona

| Área | Situação |
|---|---|
| Importação do MDB antigo → SQLite (atômica, com quarentena e conferência de contagens) | pronto, testado com banco real |
| Cadastros: cartões (com férias), horários, jornadas, feriados, justificativas, alarmes, configurações, relógio, horário de verão | pronto |
| Apuração e espelho de ponto (tolerâncias, intervalo, extras, faltas, abonos, noturno), edição de marcações, justificativas, impressão/CSV | pronto (regras em `docs/spec/apuracao.md`) |
| Listagem de marcações e relatórios de cadastros | pronto |
| Exportação / importação de arquivo de marcações (MOVIMENT.TXT), backup/restauração, reorganização e reparo | pronto |
| Senhas e níveis de acesso | pronto |
| Coleta de marcações pela serial | **experimental**: protocolo reconstruído, falta validar com o relógio (log hex na tela) |
| Acerto de hora, envio de cartões/alarmes/faixa horária, status do relógio | não implementado (depende do protocolo) |
| BioLite / Promag | fora de escopo |

## Como compilar e publicar

```powershell
dotnet build MiniTime.sln
dotnet test
.\scripts\publicar.ps1        # gera dist\MiniTime-<versão>-win-x64.zip (autocontido)
```

O leitor de MDB (`MiniTime.MdbReader`, .NET Framework 4.8, x86) usa o provedor Jet 4.0 que acompanha o Windows; é
compilado junto e copiado para `MdbReader\` ao lado do executável.

## Importando o MDB antigo

Menu **Utilitários → Importar dados do MDB antigo**. O arquivo original nunca é alterado: é copiado para uma
pasta temporária e lido em modo somente leitura. A importação é atômica (tudo ou nada) e registros inválidos
(por exemplo marcações com data absurda) vão para a tabela `ImportacaoQuarentena` em vez de se perderem.

Se o MDB tiver senha, informe-a na tela (fica salva só no computador do usuário, em `%APPDATA%\MiniTime`, cifrada com DPAPI) ou na
variável de ambiente `MINITIME_MDB_PWD`. **Nenhuma senha é versionada neste repositório.**

## Estrutura

```
src/MiniTime.Core       modelos, apuração, exportação, segurança
src/MiniTime.Data       SQLite, repositórios, importador MDB, serviços
src/MiniTime.MdbReader  leitor de MDB (net48, x86)
src/MiniTime.Serial     protocolo e cliente do relógio
src/MiniTime.App        interface WPF
tests/MiniTime.Tests    testes xUnit
docs/spec               especificações reconstruídas (apuração, protocolo, banco)
```

## Dados e configuração

O banco fica em `%LOCALAPPDATA%\MiniTime\minitime.db`; logs em `%LOCALAPPDATA%\MiniTime\logs`.
A variável `MINITIME_DB` aponta o programa para outro arquivo de banco (testes/suporte).

## Segurança

- Enquanto **nenhum usuário** estiver cadastrado, o acesso é livre (como no programa original): crie um administrador em
  *Arquivos → Senhas* assim que possível. Com usuários, o login pede senha (mínimo de 6 caracteres) e bloqueia por alguns
  instantes após 3 falhas seguidas, mesmo se o programa for reaberto.
- Os níveis (Consulta / Completo / Administrador) valem também dentro das operações sensíveis (backup, restauração,
  reparo, importação, usuários), e não só nos itens de menu.
- O banco SQLite **não é criptografado** (contém nome, RG e modelos biométricos): proteja a pasta de dados e os backups.
- *Reparar* grava antes uma cópia `minitime-antes-do-reparo-*.db` ao lado do banco e não altera nada se a verificação de
  integridade falhar.
