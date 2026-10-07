# Parte 01 - Cadastros A (frmCadGeral, FrmErro, frmAbout, frmAlarmes, FrmIconDimep, frmJornadas, FrmLocalizar, FrmMarcDespr, frmFeriados)

Convenções: `Lnnn` = linha do S\MiniTime_utf8.txt. ALTA = literal/valor visível ou confirmado por res_strings/schema/DB; MÉDIA = inferido de forma coerente; BAIXA = palpite. Não existiam dumps em S\forms\ nem texto em S\help\ (pastas existem, mas vazias, conferido no início e no fim). Portanto TODOS os nomes de controles abaixo vêm do uso no código (MÉDIA), e Caption/valores vêm de res_strings (ALTA quando o ID está visível).

## Observações gerais de leitura do decompilado (valem para todos os forms desta parte)

- Idiomas recorrentes em todos os cadastros (frmAlarmes, frmJornadas, frmFeriados; frmHorarios/frmCartoes/frmJustificativa seguem o mesmo molde):
  - Variáveis de módulo (nomes do VBD): `global_52` = TipoAcao (0 = Incluir, 1 = Alterar, 2 = Excluir) [Inicio: L1266, L2384, L4027]; `global_56` = texto selecionado (código vindo do grid) [L1267]; "dirty flag" (registro alterado e não gravado) = `global_64` em frmAlarmes e `global_60` em frmJornadas/frmFeriados, posto em `&HFFFFFF` (True) em todo evento Change [L1020, L1185, L1955, L2205, L2235, L2325, L2338, L3636, L3684, L3732, L3845]; `global_72` (frmAlarmes) / `global_68` (frmJornadas, frmFeriados) = Recordset da tabela; em frmAlarmes `global_60` = NOME DA TABELA ("Sirene" ou "SireneBioLite") e `global_68` = código original [L1274]. ALTA para os papéis de TipoAcao e dirty; MÉDIA para os nomes.
  - Nomes verdadeiros dos procedimentos (via handlers de erro): Proc_N_24=ToInsert, N_25=ToUpdate, N_26 (alarmes)/N_25 (jornadas)/N_24 (feriados)=ToDelete, Procura (FindFirst), Consistir (validação), Habilita, Desabilita, Altera (UPDATE), Carrega (preenche campos), Insere (INSERT; sem handler nomeado, inferido do conteúdo), Limpa, Carrega_Msg. O VBD numerou alguns chamadores de forma enganosa (ex.: no fim de Insere chama-se "Proc_x_34/32(TxtCodigo.Text)", que na verdade é Carrega/Limpa conforme o form) - tratar como Carrega(codigo) após inserir (MÉDIA).
  - Modos de tela: ToInsert/ToUpdate/ToDelete trocam o Caption do form (IDs 1150/1151/1152 alarmes; 1250/1251/1252 jornadas; 1270/1271/1272 feriados), mostram/escondem `CmbCodigo` (visível só em Alterar), travam `TxtCodigo` (`Locked = True`, `TabStop = False` em Alterar; destravado e com SetFocus em Incluir) e ligam `cmdGravar.Default` (Incluir/Alterar) ou `cmdExcluir.Default` (Excluir). Em Excluir os campos são desabilitados (`fraControles.Enabled = False`). Ex.: alarmes L1340-1372, L1374-1414, L1416-1438. ALTA para o comportamento de Visible/Locked/TabStop/Default; MÉDIA para True/False exatos de Enabled dos botões (o VBD imprime `X.Enabled = X` quando é constante booleana False - interpretado como False).
  - Padrão de máscara/pad do código: `Format(PadNCodigo(txt, 2), "00")` na saída do campo Código [L1067, L2002, L3779]. `PadNCodigo(texto, n)` (módulo Geral, L51049-51075): `t = Trim(texto)`; se `n <= Len(t)` retorna `Right(t, n)` (ou seja, TRUNCA mantendo os n últimos caracteres); senão `String(n - Len(t), "0") & t`. ALTA.
  - `TrimNull(v)` (L50980): retorna "" se Null, senão `Trim(v)`. `ConvCheck(b)` (L51419): `IIf(b, "True", "False")` - os booleanos vão para o SQL do Access como literais True/False. `Geral_Confere_String_Sql` (L51391): percorre a string e troca `'` e `"` por concatenações Access (`' & Chr(39) & '` e `' & Chr(34) & '`) - ou seja, o SQL é montado como `'abc' & Chr(39) & 'def'`; no port usar parâmetros. ALTA.
  - SQL é executado por `global_006F007C` (objeto Database DAO global; `UnkVCall_5Ch` = Execute, `UnkVCall_BCh` = OpenRecordset, `UnkVCall_D4h` = FindFirst, `UnkVCall_E4h` = Bookmark/Move) - MÉDIA. Separador de colunas nas strings SQL é `global_00445C7C` (provavelmente `,` seguido de espaços/CRLF) e fechamento `)` é `global_00444E98`; `global_00445518` = " " (espaço) [uso em L709, L51140]. MÉDIA.
  - Teclas: Enter nos TextBox faz `SendKeys "{tab}"` [ex.: L1048-1050, L1213-1214, L1983-1985]; campos numéricos aceitam apenas ASCII 48-57 e 8 (backspace) [L1051, L1986, L3763]. ALTA.
  - Título comum dos avisos: `LoadResString 9` = 'Aviso!'; alguns usam 3 = 'Atenção'. Flags de MsgBox: 48 = exclamação+OK; 292 = (256+32+4) pergunta, Sim/Não, default Não; 260 = (256+4) Sim/Não, default Não, sem ícone; 308 = (256+48+4) exclamação, Sim/Não, default Não; 16 = crítico/OK; 64 = informação/OK. Resultado 6 = Sim, 7 = Não.
  - Todos os Form_Load centralizam o form via `Geral.Centraliza(Me, MdiPrincipal)` [L83, L957, L2295, L3996 etc.]; em Form_Unload dos cadastros: `frmCadGeral.Enabled = True` e libera o Recordset [L982, L2311, L4010] (o frmCadGeral fica desabilitado enquanto o cadastro filho está aberto). ALTA.
  - Show dos filhos: `Me.Show 3` (alarmes L1281, jornadas L2402, feriados L4042) - flag bruto 3 (decompilado), provavelmente modal sobre o frmCadGeral. MÉDIA.

---

### frmCadGeral - Formulário genérico de listagem/busca de cadastros (grid + Ordem + Localizar + Novo/Alterar/Excluir)  (linhas 7-654)  [Confiança geral: MÉDIA-ALTA]

- **Propósito**
  Tela única reutilizada por todos os cadastros do menu "Arquivos": mostra um grid com Código + Descrição (ou Nome), permite ordenar por coluna, localizar por prefixo, e abrir o formulário filho do cadastro (incluir/alterar/excluir). O parâmetro `TipoLista` escolhe qual cadastro. A cópia herdou nomes de `frmBusca` (handler de GotFocus se chama "frmBusca.TxtExpressao_GotFocus", L127). ALTA.

- **Mapeamento TipoLista -> cadastro** (Cadastro.InicioCadastro, linhas 6148-6554 do módulo `Cadastro`; também `CarregaID` L433-606 só trata os mesmos valores 0,1,2,3,4,11) ALTA:

| TipoLista | Form filho (`Frm`) | Caption da lista (res) | Colunas do grid / cabeçalho | Rotina de preenchimento (módulo Cadastro) |
|---|---|---|---|---|
| 0 | frmAlarmes | 1025 'Manutenção do Arquivo de Horário de Alarmes' (L5849-5849 região, L5826-5849) | Código (9821), Descrição (9824) | PreencheGridAlarmes L6326-6370 (campos "Codigo","Descricao" L6354,L6358) |
| 1 | frmCartoes | 1003 'Manutenção do Arquivo de Cartões' | Código (9821), Nome (9830) | PreencheGridCartoes L6372-6416 (campos "Codigo","Nome" L6396,L6400) - tabela Funcionario |
| 2 | frmHorarios | 1016 'Tabela de Horários de Trabalho' | Código, Descrição | L6418-6462 (campos "Codigo","Descricao") |
| 3 | frmJornadas | 1017 'Tabela de Jornadas' | Código, Descrição | PreencheGridJornadas L6464-6508 |
| 4 | frmFeriados | 1002 'Tabela de Feriados' | Código, Descrição | PreencheGridFeriados L6510-6554 |
| 11 | frmJustificativa | 4005 'Tabela de Justificativas' | Código, Descrição | SQL literal visível no handler: `SELECT Codigo, Descricao FROM Justificativas order by Codigo` (L6144 região, handler "Cadastro.InicioCadastro") |
| 100, 101, 102 | (não identificado) | - | - | tratados em PreencheGrid L325-331 (ver "Hipóteses/lacunas") |

  O SQL de carga de cada lista NÃO é legível (truncado). O padrão deduzido é `SELECT Codigo, <Descricao|Nome> FROM <tabela> [ORDER BY Codigo]` (o único SQL literal visível é o de Justificativas). Tabelas: Sirene (ou SireneBioLite conforme o modelo de relógio), Funcionario, Horarios, Jornadas, Feriados, Justificativas. MÉDIA.
  Caption do form e dos 3 botões de ordem são reajustados em `Proc_14_1` (PreencheGridCadastro, L6228-6324): para qualquer TipoLista torna visíveis `OptOrdem(0)` (caption 9821 'Código') e `OptOrdem(1)` (9824 'Descrição', ou 9830 'Nome' para TipoLista 1) e chama a rotina específica da lista. OptOrdem(2) existe mas não é usado aqui (L451-458 mexe nele só para HelpContextID). ALTA.
  Formatação do grid (todas as rotinas Proc_14_2..6): 2 colunas, larguras `Width * 0.28` e `Width * 0.67`, alinhamento coluna 1 (código) e 4; cabeçalho `TextMatrix(0,0)=9821 'Código'` e `TextMatrix(0,1)=Descrição/Nome` (L6335-6345 e repetições). ALTA.

- **Controles** (nomes pelo código; sem dump de form -> Conf. MÉDIA):

| Nome | Tipo provável | Rótulo/Caption | Tamanho/Formato | Default | Validações | Conf. |
|---|---|---|---|---|---|---|
| GrdCadGeral | MSFlexGrid (usa Rows, Row, Col, RowSel, ColSel, Sort, TextMatrix, ColWidth, Redraw, TopRow) | cabeçalhos: 'Código' (9821) / 'Descrição' (9824) ou 'Nome' (9830) | ColWidth 28% e 67% da largura | 1 linha de cabeçalho fixa (linha 0) | RowSel é forçado = Row no MouseUp (seleção única) [L185-188] | MÉDIA |
| vsbCadGeral | VScrollBar (barra vertical própria) | - | Max = Rows-1 (ou 1 se vazio) [L357]; Visible só se Rows >= 10 [L400-407] | - | Change sincroniza TopRow/Row do grid [L218-254]; EnterCell sincroniza vsb.Value=Row [L199-216] | MÉDIA |
| TxtExpressao | TextBox | (label LblExpressao = 1120 'Localizar') | sem MaxLength visível | vazio | GotFocus seleciona tudo [L120-133]; LostFocus pad de 16 zeros (ver regras) | MÉDIA |
| LblExpressao | Label | 'Localizar' (1120) [L624-625] | - | - | - | ALTA |
| fraOrdem | Frame | 'Ordem' (1130) [L626-627] | - | - | - | ALTA |
| OptOrdem(0), OptOrdem(1), (2) | OptionButton array | (0) 'Código' (9821); (1) 'Descrição' (9824) / 'Nome' (9830); (2) não usado | - | o mesmo índice que estava ativo (var `global_72` = última ordem) [L41, L359-368] | Click ordena o grid | MÉDIA |
| cmdCadastro(0) | CommandButton array | 'Novo' (1100) [L628-630] | - | - | desabilitado se software bloqueado (global_006F017E) ou capacidade máxima | ALTA (caption) |
| cmdCadastro(1) | CommandButton | 'Alterar' (1101) [L631-633] | - | - | desabilitado se grid vazio | ALTA (caption) |
| cmdCadastro(2) | CommandButton | 'Excluir' (1102) [L634-636] | - | - | desabilitado se grid vazio | ALTA (caption) |
| cmdCancelar | CommandButton | 'Sair' (1103) [L637-640] | - | - | - | ALTA |
| cmdBuscar | CommandButton | 'Procurar' (1104) [L641-643] | - | - | - | ALTA |
| fraControles | Frame (só HelpContextID) | - | - | - | - | BAIXA |

- **Botões e eventos**
  - `Inicio(pTipoLista)` [L275-287]: `Me.Show 3`; `CarregaID(pTipoLista)` (ajusta HelpContextID de cada controle conforme TipoLista; ids de ajuda truncados). O atribuir `TipoLista = pTipoLista` não aparece (provável, MÉDIA).
  - `Form_Load` [L80-93]: só Centraliza. `Form_Unload` [L95-107]: libera objeto. `Form_Activate` [L109-118]: chama `PreencheGrid` a cada ativação - por isso, ao fechar um cadastro filho (que volta a habilitar o frmCadGeral) a lista é recarregada. ALTA.
  - `PreencheGrid` [L289-431]: (1) `Carrega_Msg` (L621-653: define captions 1120,1130,1100-1104); (2) `Me.Enabled = False`, `Screen.MousePointer = 11` (ampulheta); (3) `Cadastro.InicioCadastro(TipoLista, Me)` (caption do form + qual `Frm`); `CarregaID`; abre o recordset da lista (`strSql`, truncado) ; (4) habilita/desabilita `cmdCadastro(i)` (L308-352): quando a tabela está vazia desabilita Alterar/Excluir; para TipoLista = 1 e para 100/101/102 há ramos especiais; se `MaxRegistros` (capacidade) desabilita Novo; se `global_006F017E = True` (software bloqueado por excesso de funcionários - msgs 9954-9957, L18610-18622) desabilita Novo/Alterar/Excluir [L411-417]; (5) `PreencheGridCadastro` (rotina por TipoLista); (6) ajusta `vsbCadGeral.Max`; (7) reativa a ordem atual: percorre `OptOrdem` e chama `OptOrdem_Click` do índice selecionado [L358-369]; (8) reposiciona na linha cuja coluna 0 = `Chave` (código selecionado antes de abrir o filho) [L370-381], senão topo; (9) esconde a vsb se Rows < 10; (10) `Screen.MousePointer` volta ao normal, `Me.Enabled = True`. ALTA para a sequência; MÉDIA para detalhes de enable.
  - `OptOrdem_Click(Index)` [L9-51]: se Rows >= 2: desliga Redraw, guarda Row/Col/RowSel/ColSel, define `RowSel`, `ColSel = Index` (coluna a ordenar = índice da opção: 0 = Código, 1 = Descrição/Nome), `Sort = 1` (ordenação ascendente genérica), depois procura a linha cujo `TextMatrix(r, 0)` = valor anteriormente selecionado e a seleciona; liga Redraw; `vsbCadGeral.Value = Row`; guarda `global_72 = Index`. A ordenação é do próprio grid em memória (NÃO há ORDER BY por coluna escolhida). MÉDIA-ALTA.
  - `cmdBuscar_Click` [L53-78] (Procurar): se `TipoLista = 1` -> `TxtExpressao.Text = Format(TxtExpressao.Text, String(16, "0"))` (L57; o caractere é o operando `var_B8`, truncado, mas o formato Format com zeros = `0000000000000000`; ver regras); `DoEvents`; percorre `r = 1 .. Rows-1` comparando `UCase(Mid(GrdCadGeral.TextMatrix(r, col), 1, Len(TxtExpressao.Text))) = UCase(TxtExpressao.Text)` (L63; `col` truncado - provável coluna da ordem ativa, 0 ou 1); no primeiro acerto seleciona a linha (`Row = r`, L68); busca de PREFIXO, case-insensitive. Sem acerto não faz nada (sem mensagem). MÉDIA (coluna comparada truncada).
  - `TxtExpressao_LostFocus` [L135-151]: se `OptOrdem(0).Value` (ordenando por Código) e `TipoLista = 1` aplica o mesmo `Format(..., String(16,"0"))` (L140). ALTA.
  - `cmdCadastro_Click(Index)` [L256-273]: `Frm.Inicio(Index, Trim(GrdCadGeral.TextMatrix(Row, 0)), TipoLista)` - ou seja `pTipoAcao = Index` (0 Novo, 1 Alterar, 2 Excluir), `pseltexto = código da linha atual (coluna 0)`, `pTipoLista = TipoLista`. Antes testa se o botão está habilitado e guarda a linha (`Chave`). Ao voltar, `Form_Activate` recarrega. ALTA.
  - `GrdCadGeral` evento 'B' [L168-179] (nome corrompido; é DblClick/Enter): chama `cmdCadastro_Click` (provável Alterar) quando há linhas; a condição é `Rows - 1 + 1 = 0` truncada (BAIXA para qual índice).
  - `GrdCadGeral_MouseUp` [L181-197]: se `Row <> RowSel`, força `Row`->... (mantém seleção de 1 linha). `GrdCadGeral_EnterCell` [L199-216]: `vsbCadGeral.Value = Row` quando Redraw. `vsbCadGeral_Change` [L218-254]: aplica `TopRow` etc. (rolagem manual; larguras). ALTA/MÉDIA.
  - `cmdCancelar_Click` [L153-166] = `Unload Me`. ALTA.
- **SQL reconstruído**: nenhum SQL de gravação neste form (somente leitura). Leitura (MÉDIA, truncado): `SELECT Codigo, Descricao FROM Sirene|SireneBioLite`, `SELECT Codigo, Nome FROM Funcionario`, `... FROM Horarios`, `... FROM Jornadas`, `... FROM Feriados`, `SELECT Codigo, Descricao FROM Justificativas order by Codigo` (ALTA para este último, L6144-ref. handler).
- **Mensagens**: nenhuma MsgBox neste form. Captions: 1120 'Localizar', 1130 'Ordem', 1100 'Novo', 1101 'Alterar', 1102 'Excluir', 1103 'Sair', 1104 'Procurar', 9821 'Código', 9824 'Descrição', 9830 'Nome' (ALTA).
- **Regras de negócio**
  1. TipoLista = 1 (Cartões/Funcionario): o código é texto de 16 posições com zeros à esquerda (Funcionario.Codigo é Text(16); amostra '0000000000007015'). O texto digitado em "Localizar" é normalizado com `Format(txt, "0000000000000000")` (String(16,"0")): `Format` do VB só preenche com zeros se o texto for numérico - texto não numérico (nome) volta inalterado; números com mais de ~15 dígitos passam por Double (cuidado). No port: se `^\d+$` -> `PadLeft(16,'0')`, senão manter. O tamanho 16 é FIXO nesta tela (não usa Parametros.QtdeDig/NumCartao/DigVerificador). ALTA (L57, L140).
  2. Aplicação do pad: na busca (Procurar) sempre que TipoLista=1; ao sair do campo só se a ordem ativa é "Código" (OptOrdem(0)).
  3. Busca por prefixo case-insensitive na coluna da ordem ativa; ordenação ascendente do grid em memória.
  4. Novo bloqueado quando há capacidade máxima (para Cartões o limite do software é 50 funcionários, msgs 9954-9957 e ToInsert do frmCartoes L23447-23460 com msgs 50/51) ou software bloqueado (`global_006F017E`). MÉDIA.
  5. Tela reabre/recarrega a lista a cada Activate e reposiciona na linha da última `Chave`.
- **Hipóteses/lacunas**: SQL exato de cada lista, coluna comparada em cmdBuscar, quais botões o evento 'B' aciona, significado de TipoLista 100/101/102 (provável lista de relógios BioLite/Promag ou terminais - não confirmado), valor de `MaxRegistros`, HelpContextIDs, MaxLength de TxtExpressao.

---

### FrmErro - Janela de erro não tratado  (linhas 655-698)  [Confiança geral: ALTA]

- **Propósito**: caixa modal exibida pelo manipulador global de erros `Geral.Erro` (rotina `Proc_45_14_6328E0`, L51118-51160) com texto formatado e botão 'Encerrar' que termina o programa. ALTA.
- **Controles**:

| Nome | Tipo provável | Rótulo/Caption | Tamanho/Máscara | Default | Validações | Conf. |
|---|---|---|---|---|---|---|
| (form) | Form | 1010 'Erro' [L658-659, L688-689] | - | - | - | ALTA |
| txtErro | TextBox multilinha | - (recebe o texto montado) [L51153] | - | - | - | ALTA |
| btnFim | CommandButton | 416 '&Encerrar' [L660-661, L690-691] | - | - | - | ALTA |
| FrameErro | Frame | - | - | - | - | BAIXA |

- **Botões e eventos**: `Form_Load` e `FrameErro_DragDrop` (evento corrompido) só definem captions. `btnFim_Click` [L667-678]: antes de `End` executa um limpeza (libera objeto) e termina o aplicativo (`End`). `FrmErro.Show 1` (modal) [L51154].
- **Texto do erro** (montado em L51118-51153): `2450 & " " & App.ProductName & "." CRLF 2451 CRLF 2452 CRLF 2458 " " 2453 CRLF CRLF 2454 " " & App.Major & "." & Format(Minor,"00") & "." & Format(Revision,"0000") CRLF 2455 " " & <nome da rotina> CRLF 2456 " " & Err.Number CRLF 2457 " " & Err.Description` onde os IDs correspondem a: 2450 'Ocorreu um erro no programa', 2451 'Se o problema persistir, por favor, entre em contato', 2452 'com a DIMEP pelo telefone + 55 11 3646-4000', 2458 'ou fax + 55 11 3646-4071', 2453 'e informe os seguintes dados:', 2454 'Versão:', 2455 'Rotina:', 2456 'Código do erro:', 2457 'Descrição do erro:'. O cursor é restaurado (`Screen.MousePointer`) antes. ALTA.
- **Mensagens**: as acima (sem MsgBox). **Regras**: nenhuma. **Lacunas**: o `If var_20 Then ... .Proc_1_3` em btnFim_Click é limpeza irrelevante.

---

### frmAbout - Sobre  (linhas 699-770)  [Confiança geral: ALTA]

- **Propósito**: caixa "Sobre" aberta por `mnuSobre_Click` (L17781-17783, `frmAbout.Show 3`). ALTA.
- **Controles** (todos com caption por res_strings):

| Nome | Tipo | Caption/valor | Conf. |
|---|---|---|---|
| (form) | Form | `1000 'Sobre' & " " & App.Title` -> 'Sobre Mini Time...' [L707-709] | ALTA |
| picIcon | Image/PictureBox | `LoadResPicture 3` [L731-732] | ALTA |
| lblTitle | Label | `App.Title` [L711] | ALTA |
| lblVersion | Label | `global_006F0154 & " " & App.Comments`: global_006F0154 = "Versão: M.mm.rrrr" montado em L18364 (`<250 'Versão:'> M.MM.RRRR`, Minor "00", Revision "0000") [L710] | ALTA |
| lblLicense | Label | `""` (global_00443B50 = string vazia; licença não preenchida) [L712] | ALTA |
| lblRodbel | Label | 1070 'Dimas de Melo Pimenta Sistemas de Ponto e Acesso Ltda' [L735-736] | ALTA |
| lblDisclaimer | Label | 1071 aviso de direitos autorais ('Atenção : Este programa de computador é protegido pela lei de direitos autorais ...') [L737-738] | ALTA |
| LblSuporte(1) | Label | 1072 ' ' [L739-741] | ALTA |
| LblEmail(1), LblEmail(2) | Label | 1073 'supmatriz@dimep.com.br'; 1075 ' ' [L742-747] | ALTA |
| LblTelefone(1), (2) | Label | 1074 '(11) 3646 4000'; 1076 ' ' [L748-753] | ALTA |
| LblBrasil | Label | 1077 'Suporte:' [L754-755] | ALTA |
| LblPortugal | Label | 1078 ' ' [L756-757] | ALTA |
| cmdOK | CommandButton | 9801 '&Ok' [L733-734] | ALTA |

- **Eventos**: `cmdOK_Click` = `Unload Me` [L718-725]. `Form_Load` [L701-716]: carrega captions (`Carrega_Msg` = Proc_2_2 L727-769), centraliza. Versão instalada do exe: FileVersion 4.04.0001, ProductName 'Mini Time', Comments vazio (VersionInfo do MiniTime.exe).
- **Mensagens/Regras**: nenhuma. **Lacunas**: posições/imagens.

---

### frmAlarmes - Cadastro de Horários de Alarme (sirene)  (linhas 771-1907)  [Confiança geral: MÉDIA-ALTA]

- **Propósito**: CRUD da tabela `Sirene` (relógio Mini Point/MD5751-53: horários de toque com duração, 20 alarmes) e `SireneBioLite` (relógio BioLite: alarmes sem duração, máx. 12 em dia útil + 12 em fim de semana = 24). Também envia os alarmes ao relógio (botão Transmitir abre `frmComunicacao`, fora do escopo). Caption base: 1025 'Manutenção do Arquivo de Horário de Alarmes' [L1856-1857]; por modo: 1150 'Inclusão de Horário de Alarme', 1151 'Alteração de Horário de Alarme', 1152 'Exclusão de Horário de Alarme'. ALTA.
- **Schema (ALTA)**: `Sirene(Codigo Integer, Descricao Text(30), Horario Date/Time, Duracao Integer, Util Yes/No, FimSem Yes/No)`, PK Codigo, índice Descricao; 7 linhas no DB de exemplo. `SireneBioLite(Codigo, Descricao Text(30), Horario Date/Time, Util, FimSem)` (sem Duracao), 0 linhas. `Parametros.DuracaoSireneBioLite` (Integer) guarda a duração GLOBAL da sirene do BioLite (configurada em outro form: res 10058-10061 'Informe um valor válido (entre 1 e 99)!').
- **Variação por modelo (global_006F0158 = Parametros.Tipo_Relogio)**: o modelo é comparado com `"MD5751 / MD5753"` e `"BioLite"` em Inicio (L1260-1262), Form_Load (L959-960), ToUpdate (L1389-1390), Validate (L1075-1076), Carrega (L1752-1753), Consistir (L1545-1546, 1547...). Valores de Tipo_Relogio vistos no exe: "Mini Point", "BioLite", "Port Point", "MD501", "RB0703", "MD5751 / MD5753" (frmSelecRelogio L21015-21169 oferece apenas 3 opções: Mini Point, BioLite, Port Point). O VBD perdeu os operadores lógicos (`Or`/`<>`) das comparações compostas - há inclusive contradições (L19405-19410 `(g = "BioLite") And (g = "MD5751 / MD5753")`). Interpretação consistente com schema/manual/mensagens (MÉDIA, não literal):
  - Tabela: modelo BioLite -> `SireneBioLite`; os demais -> `Sirene`. O nome fica em `global_60`; a única atribuição visível é a literal `"SELECT * FROM " & "Sirene"` (L1263) e os usos `global_60 = "Sirene"` (L1686, L1783).
  - Campo Duração (`TxtDuracao` + `Label4` 'Duração:' 386): visível e obrigatório só para o modelo que usa a tabela `Sirene` (Duracao existe só ali; Altera/Insere só incluem Duracao quando `global_60 = "Sirene"`, L1686-1699, L1783-1792; Consistir só exige duração se `TxtDuracao.Visible`, L1534-1539). O Form_Load [L959-971] esconde/mostra TxtDuracao e Label4 por essa comparação, mas o sentido do `If ... = 0` vs. a comparação truncada fica ambíguo (BAIXA para qual literal dispara).
  - Faixa de código: Mini Point/Sirene = 1 a 20 (manual: "até vinte toques de alarme"; texto da msg 123 'Código do Alarme deve estar entre 1 e 20.'); BioLite = 1 a 24 - a mesma msg 123 é exibida com `Replace(msg, "20", "24")` => 'Código do Alarme deve estar entre 1 e 24.' [L1097, L1570]. O limite numérico em si (a comparação `If edi`) está truncado (MÉDIA).
  - Limite de 12 alarmes por tipo de dia só no BioLite (SireneBioLite): ver Consistir.
  - Ao entrar em Alterar para BioLite abre `Select * From Terminal` para decidir se Transmitir fica habilitado (L1395-1399) (relógio cadastrado); para o outro modelo habilita Transmitir após `Consistir` (L1392-1393) (MÉDIA/BAIXA).
- **Controles**:

| Nome | Tipo provável | Rótulo/Caption | Tamanho/Máscara/Formato | Default | Validações | Conf. |
|---|---|---|---|---|---|---|
| (form) | Form | 1025 / 1150 / 1151 / 1152 (por modo) | - | - | - | ALTA |
| fraControles | Frame | 316 'Informações' [L1858-1860] | - | - | Enabled=False no modo Excluir | ALTA |
| Label1 + txtCodigo | Label + TextBox | 308 'Código' [L1862-1863] | MaxLength desconhecido (usado em `Left(CmbCodigo.Text, txtCodigo.MaxLength)`, L775; formato "00" => 2) | vazio; ao Validate vira `Format(PadNCodigo(txt,2),"00")` | só dígitos e backspace [L1051]; Enter->Tab; ver Validate | MÉDIA (MaxLength=2) |
| CmbCodigo | ComboBox (lista de códigos existentes) | itens `NN - Descricao` | visível só no modo Alterar | - | Click: localiza registro e carrega [L773-787] | MÉDIA |
| Label2 + txtDescricao | Label + TextBox | 310 'Descrição' [L1865-1866] | Descricao é Text(30) no DB => MaxLength 30 provável | vazio | obrigatório (msg 43); Enter->Tab [L1211-1224] | MÉDIA |
| Label3 + TxtHorario | Label + TextBox/MaskEdBox | 385 'Horário do Toque:' [L1868-1869] | formato hh:nn; aceita ASCII 48-57, 8 e 58 (':') [L1171]; comparação `Replace(txt, ":", "") = ""` indica máscara "__:__" | vazio | Validate: se não vazio e `Not IsDate` -> msg 111 [L1148-1157]; GotFocus seleciona tudo [L1131-1144] | MÉDIA |
| Label4 + TxtDuracao | Label + TextBox | 386 'Duração:' [L1871-1872] | KeyPress aceita só '1'..'8' (ASCII 49-56) e backspace [L1245]; manual do Mini Point: 1 a 7 segundos | vazio | obrigatório se visível (msg 125); GotFocus seleciona tudo [L1226-1238] | MÉDIA |
| ChkUtil | CheckBox | 387 'Tocar nos dias úteis' [L1874-1876] | - | desmarcado (Limpa) | Enter->Tab [L789-802] | ALTA |
| ChkFimSem | CheckBox | 388 'Tocar nos finais de semana' [L1878-1880] | - | desmarcado | Enter->Tab [L852-865] | ALTA |
| cmdNovo | CommandButton | 418 'Novo' [L1886-1888] | - | - | pergunta se há alterações pendentes | ALTA |
| cmdGravar | CommandButton | 400 'Gravar' [L1890-1892] | - | Default em Incluir/Alterar | - | ALTA |
| cmdExcluir | CommandButton | 408 'Excluir' [L1882-1884] | - | Default em Excluir | - | ALTA |
| cmdSair | CommandButton | 402 'Sair' [L1894-1896] | - | - | pergunta se há alterações | ALTA |
| cmdTransmite | CommandButton | 436 'Transmitir' [L1898-1900] | - | habilitado só em Alterar | - | ALTA |

- **Botões e eventos** (passo a passo)
  - `Inicio(pTipoAcao, pseltexto, pTipoLista)` [L1257-1288]: define modelo; `rs = OpenRecordset("SELECT * FROM Sirene")` (literal L1263; tabela real segue `global_60`); `global_52 = pTipoAcao`; `global_56 = pseltexto`. Se 0 (Incluir) -> `ToInsert`. Senão `Procura(pseltexto)` + `Carrega`, guarda `global_68 = txtCodigo.Text`, e vai a `ToDelete` (acao 2) ou `ToUpdate`. `Show 3`.
  - `Form_Load` [L952-976]: Carrega_Msg (L1854-1906), centraliza, mouse, mostra/esconde `TxtDuracao`/`Label4` conforme modelo (ver acima). `Form_Unload` [L978-992]: libera rs, `frmCadGeral.Enabled = True`.
  - `TxtCodigo_Validate` [L1063-1129] (ao sair do código): (1) `txtCodigo.Text = Format(PadNCodigo(txt,2),"00")`; (2) se `Trim = ""` -> msg 41; (3) valida faixa de código (msg 123, 20 ou 24 por modelo); (4) se `global_52 = 0` (Incluir): `Procura(txt)`; se achou: `Carrega` e pergunta msg 1172 'Registro já cadastrado. Altera?' (292, título 9): Sim -> `ToUpdate`; Não -> `Limpa` + foco de volta no código.
  - `CmbCodigo_Click` [L773-787]: `Procura(Left(CmbCodigo.Text, MaxLength))`; se achou `Carrega`.
  - `cmdNovo_Click` [L804-824]: se dirty: msg 1171 'Deseja perder as alterações feitas neste registro?' (292, título 'Aviso!'); Não (7) aborta; Sim -> `ToInsert`.
  - `cmdSair_Click` [L994-1016]: se dirty: mesma msg 1171; Sim -> `Unload Me`; Não -> fica. Se não dirty, comportamento truncado (provável Unload direto) [MÉDIA].
  - `cmdGravar_Click` [L932-950]: `Consistir`; se OK: se `global_52 = 0` -> `Insere` e depois `ToUpdate` (passa a modo Alterar do registro recém-criado); senão `Altera` (UPDATE).
  - `cmdExcluir_Click` [L826-850]: MsgBox(`47 'Confirma Exclusão?' & vbCrLf & txtCodigo & " - " & Trim(txtDescricao)`, 260, título 9 'Aviso!'). Se Sim (6): `DELETE FROM <global_60> WHERE Codigo = <Val(txtCodigo)>` (L835-836), `PreencherComboCod`, `Unload Me`.
  - `cmdTransmite_Click` [L867-930]: cursor ampulheta, desabilita o botão, prepara globais de comunicação (arrays `global_006F035C` ReDim 0..2, `global_006F035C = &H63`, `global_006F0360 = 9`; os valores/semântica estão truncados - 9 provavelmente identifica "Alarmes" na programação, cf. res 2087 'Enviando Toques de Alarme' e 2055 'Alarmes') e abre `frmComunicacao.Show 1` (modal); reabilita o botão. Erro: handler "FrmAlarmes.CmbTransmite_Click". MÉDIA (detalhes do protocolo fora do escopo).
  - `PreencherComboCod` [L1290-1338]: `CmbCodigo.Clear`; `SELECT * FROM <global_60> Order By Codigo` (L1299) e, para cada linha, adiciona `Format(Codigo,"00") & " - " & Trim(Descricao)` (texto montado em L1312, garbled); posiciona `ListIndex` no item cujo código = `Val(txtCodigo)`; `cmdNovo.Enabled = True`.
  - `Procura` (Proc_3_27 L1440-1456): `rs.FindFirst "Codigo = " & Val(codigo)` (L1444); retorna Boolean de achado (NoMatch invertido).
  - `Carrega` (Proc_3_32 L1736-1777): lê do recordset: `txtCodigo = Format(PadNCodigo(CStr(Codigo),2),"00")`, `txtDescricao = TrimNull(Descricao)`, `TxtHorario = Format(Horario, "hh:nn")` (formato `Format(0,2)` garbled), `TxtDuracao = Duracao` (condicional por modelo), `ChkUtil = Util`, `ChkFimSem = FimSem`; `PreencherComboCod`; dirty = 0.
  - `Limpa` (Proc_3_34 L1812-1838): zera todos os campos (texto vazio; `TxtHorario` com valor "vazio", checks desmarcados), dirty = 0.
  - `Consistir` (Proc_3_28 L1458-1648) - ordem das validações:
    1. `txtCodigo = Format(PadNCodigo(txt,2),"00")`; `Procura(codigo)`.
    2. Modo Incluir e já existe -> msg 1170 'Registro já cadastrado!' (48, 'Aviso!'); foco no código; aborta.
    3. Código em branco -> msg 41 'Código Branco!' (48, 'Aviso!'); foco no código.
    4. Descrição em branco (`Trim = ""`) -> msg 43 'Descrição em Branco!'; foco na descrição.
    5. Horário: `Replace(TxtHorario.Text, ":", "") = ""` -> msg 124 'Período de Toque em Branco.' (48, 'Aviso!'); foco no horário. Se `Not IsDate(TxtHorario.Text)` -> msg 111 'Hora incorreta!' (título 'Aviso!' aqui).
    6. Duração: se `TxtDuracao.Visible` e vazia -> msg 125 'Tempo de duração em branco.'; foco na duração.
    7. Faixa de código por modelo -> msg 123 (20 ou 24); foco no código. (Observação: a posição relativa da checagem 7 vs. 5/6 é incerta: o bloco aparece aninhado depois da checagem de duração, L1545-1580.)
    8. Somente BioLite (`SireneBioLite`): se `ChkUtil` marcado: `SELECT * FROM SireneBioLite WHERE Util = true` (L1587) e, se a contagem = 12, msg 10076 'É permitido apenas 12 alarmes configurados para dia útil.' (48, 'Aviso!'); se `ChkFimSem` marcado: `SELECT 1 FROM SireneBioLite WHERE FimSem = true` (L1607) e contagem = 12 -> msg 10077 'É permitido apenas 12 alarmes configurados para final de semana.'. (A comparação é exatamente `= 12` sobre a contagem do recordset; não exclui o próprio registro em modo Alterar - BAIXA se isso é bug ou intenção.)
    9. Retorno True (`var_34 = True`) quando passa.
  - `Altera` (Proc_3_31 L1678-1734): `Me.Enabled=False`, mouse 11. Se `global_60 = "Sirene"`: `UPDATE Sirene SET  Descricao = '<esc(Trim(desc))>', Horario = #hh:nn#, Duracao = <Val(duração)>, Util = <True|False>, FimSem = <True|False> WHERE Codigo = <Val(codigo)>` (L1696-1698; hora via `Format(CDate(TxtHorario), "hh\:nn")`). Senão (SireneBioLite): mesmo UPDATE sem `Duracao` (L1710-1712). Executa (`Database.Execute`), `Recordset.Requery`(?) (`global_72.UnkVCall_100h`), `PreencherComboCod` (2x), `Carrega(txtCodigo)`; restaura mouse/Enabled.
  - `Insere` (Proc_3_33 L1779-1810): Sirene: `INSERT INTO Sirene (Codigo, Descricao, Horario, Duracao, Util, FimSem) VALUES ( <cod>,'<desc>',#hh:nn#,<dur>,<True|False>,<True|False>)` (L1785-1792). SireneBioLite: `INSERT INTO SireneBioLite (Codigo, Descricao, Horario, Util, FimSem) VALUES ( ... )` (L1795-1800). Depois `Requery`, `PreencherComboCod`, carrega o registro. Booleans via `ConvCheck` -> literais True/False.
  - `ToInsert` [L1340-1372], `ToUpdate` [L1374-1414], `ToDelete` [L1416-1438], `Habilita/Desabilita` [L1650-1676] (liga/desliga `fraControles.Enabled`), `MskFim_Change` (Proc_3_35 L1840-1852: só marca dirty).
- **Mensagens** (ID -> texto EXATO -> condição -> botões/ícone):

| ID | Texto | Quando | Botões/ícone |
|---|---|---|---|
| 41 | 'Código Branco!' | código vazio (Validate L1071; Consistir L1481) | OK, exclamação (48), título 9 'Aviso!' |
| 43 | 'Descrição em Branco!' | descrição vazia (L1499) | 48, 'Aviso!' |
| 111 | 'Hora incorreta!' | horário inválido (L1151 título 3 'Atenção'; L1523 título 9 'Aviso!') | 48 |
| 123 | 'Código do Alarme deve estar entre 1 e 20.' (com "20"->"24" no BioLite) | código fora da faixa (L1084, L1554; L1097, L1570) | 48, 'Aviso!' |
| 124 | 'Período de Toque em Branco.' | horário vazio (L1514) | 48 |
| 125 | 'Tempo de duração em branco.' | duração vazia e visível (L1537) | 48 |
| 1170 | 'Registro já cadastrado!' | Incluir com código existente (L1470) | 48 |
| 1171 | 'Deseja perder as alterações feitas neste registro?' | Novo/Sair com dirty (L807, L998) | 292 (Sim/Não, default Não), 'Aviso!' |
| 1172 | 'Registro já cadastrado. Altera?' | digitar código existente em Incluir (L1107) | 292, 'Aviso!' |
| 47 | 'Confirma Exclusão?' (+ vbCrLf + 'NN - Descrição') | Excluir (L829) | 260, 'Aviso!' |
| 10076 | 'É permitido apenas 12 alarmes configurados para dia útil.' | BioLite, 12 já marcados Util (L1593) | 48 |
| 10077 | 'É permitido apenas 12 alarmes configurados para final de semana.' | BioLite, 12 já FimSem (L1613) | 48 |
| 1025, 1150-1152, 316, 308, 310, 385-388, 400, 402, 408, 418, 436 | captions | Carrega_Msg / modos | - |

- **Regras de negócio**
  1. Código: inteiro, 2 dígitos (`Format "00"`), único; faixa 1..20 (Mini Point) ou 1..24 (BioLite). Código 0 não é tratado explicitamente em texto (cai na faixa, msg 123) - MÉDIA.
  2. Descrição obrigatória, trim; apóstrofos/aspas escapados pelo helper SQL (no port, parâmetros).
  3. Horário hh:nn obrigatório e válido; gravado como Date/Time com a data base 30/12/1899 (Access) apenas com a hora.
  4. Util/FimSem: pelo menos um deles NÃO é exigido (pode gravar ambos False); BioLite limita 12 por tipo de dia.
  5. Duração só para Sirene (Mini Point): digitar apenas dígitos 1..8 (um dígito na prática; manual: 1-7 s). Para BioLite a duração é global (Parametros.DuracaoSireneBioLite).
  6. Dirty flag controla confirmação ao Novo/Sair.
  7. Excluir é DELETE direto (não há verificação de dependências); sem renumeração.
- **Hipóteses/lacunas**: MaxLength reais; sentido exato do If de modelo em Form_Load/Carrega (ver acima); condições numéricas truncadas da faixa; comportamento de `cmdSair` sem dirty; payload de `cmdTransmite`; se `TxtHorario` é MaskEdBox; mensagem ao tentar gravar sem Util nem FimSem (não há).

---

### FrmIconDimep - Form só de ícone  (linhas 1908-1909)  [Confiança geral: ALTA]

- Objeto sem nenhum procedimento (L1908-1909 contém só o cabeçalho `'Object: FrmIconDimep`). Provavelmente um form auxiliar que apenas guarda o ícone DIMEP (`Icon` usado por MDI/outros forms - ex.: `var_70.Icon = ...` em L69441 região FrmApura). BAIXA para a finalidade; ALTA para "sem código".

---

### frmJornadas - Cadastro de Jornadas (tabela de períodos semanais + DSR)  (linhas 1910-3154)  [Confiança geral: MÉDIA-ALTA]

- **Propósito**: CRUD da tabela `Jornadas`: associa a cada dia da semana (Domingo..Sábado) um código de faixa horária (`Horarios.Codigo`, 0 = sem expediente) e define parâmetros de falta/DSR (descanso semanal remunerado). `Funcionario.Horario`/jornada referenciam o Codigo da jornada (msgs 16-17, 106). Caption base 1017 'Tabela de Jornadas' [L3053-3054]; por modo: 1250 'Inclusão de Jornada', 1251 'Alteração de Jornada', 1252 'Exclusão de Jornada'. ALTA.
- **Schema (ALTA)**: `Jornadas(Codigo Integer, Descricao Text(35), Segunda, Terca, Quarta, Quinta, Sexta, Sabado, Domingo Integer, NaoMarcarFalta Integer, TrataDSR Integer, JornSemanal Integer, DiaDSR Integer)`; PK Codigo; índice Descricao. 13 linhas no DB de exemplo; no exemplo: dias contêm códigos de Horarios (ex.: Jornada 1 = Seg..Sex 1, Sáb 6, Dom 0), NaoMarcarFalta = 0, TrataDSR = 0, JornSemanal = 0, DiaDSR = 1 em todas.
- **Controles**:

| Nome | Tipo provável | Rótulo/Caption | Tamanho/Máscara/Formato | Default | Validações | Conf. |
|---|---|---|---|---|---|---|
| fraControles | Frame | 316 'Informações' [L3055-3058] | - | - | Enabled False em Excluir | ALTA |
| Label9 + TxtCodigo | Label + TextBox | 308 'Código' [L3060-3061] | 2 dígitos (`Format "00"`); MaxLength usado em `Left(CmbCodigo.Text, TxtCodigo.MaxLength)` (L2042) | vazio | só dígitos; Validate: PadNCodigo(...,2) "00", branco -> msg 41, duplicado em Incluir -> msg 1172 (L1998-2038) | MÉDIA |
| CmbCodigo | ComboBox | itens `NN - Descrição` | visível só em Alterar | - | Click carrega registro (L2040-2054) | MÉDIA |
| Label10 + TxtDescricao | Label + TextBox | 310 'Descrição' [L3062-3063] | Text(35) => MaxLength 35 provável | vazio | obrigatória (msg 43); Enter->Tab | MÉDIA |
| Label1 | Label | 371 'Tabela de Período' [L3064-3065] | - | - | - | ALTA |
| LstHorarios | ListBox (faixas horárias disponíveis) | 1º item = 53 'Sem Expediente' [L2254-2255], seguidos de `NN - Descrição` (Format(Codigo,"00") & " - " & TrimNull(Descricao), L2268) de cada linha de `Horarios` (SQL truncado; `SELECT * FROM Horarios` provável) | `ListIndex = 0` ao carregar | item 0 | Enter (KeyPress 13) aciona o 1º dia livre (L2102-2121) | MÉDIA |
| LblSemana(0..6) | Label array (uma por dia: 0 Domingo, 1 Segunda, ... 6 Sábado) | texto da faixa atribuída: `NN - Descrição`, `Sem Expediente` (53) ou `NN - * Não Cadastrado *` (L2838) | - | vazio | - | MÉDIA |
| CmdSemana(0..6) | CommandButton array | alterna ">>"/"<<": 419 'Domingo >>', 421 'Segunda >>', 423 'Terça >>', 425 'Quarta >>', 427 'Quinta >>', 429 'Sexta >>', 431 'Sábado >>'; estado atribuído: 420 '<< Domingo', 422 '<< Segunda', 424 '<< Terça', 426 '<< Quarta', 428 '<< Quinta', 430 '<< Sexta', 432 '<< Sábado' (Mens_ID L2459-2478 constrói os dois vetores de IDs: `419 + 2*i` e `420 + 2*i`) | - | ">>" | - | ALTA |
| chkFalta | CheckBox | 373 'Não Marca Falta' [L3076-3077] | valor 0/1 | 0 | - | ALTA |
| chkDSR | CheckBox | 372 'Tratamento DSR' [L3074-3075] | 0/1 | 0 | Click habilita/desabilita fraDSR (L2168-2214) | ALTA |
| fraDSR | Frame | 516 'DSR' [L3070-3073] | - | - | habilitado só com chkDSR | ALTA |
| Label2 + txtDSR | Label + TextBox/MaskEdBox | 374 'Horário Semanal' [L3066-3067] | "HH:MM" (Left 2 = horas, Right 2 = minutos); default de tela nova "00:00" (Limpa L3002), se `JornSemanal` Null -> "44:00" (L2898) | - | KeyPress/Change/GotFocus (L2216-2244); Consistir | MÉDIA |
| Label3 + cmbDia | Label + ComboBox (lista fixa) | 375 'Dia da Semana' [L3068-3069]; itens "1 - Domingo", "2 - Segunda", "3 - Terça", "4 - Quarta", "5 - Quinta", "6 - Sexta", "7 - Sábado" (res 330-336, L2279-2292) | ListIndex 0..6 | ListIndex 0 (Domingo) | - | ALTA |
| cmdNovo / cmdGravar / cmdExcluir / cmdSair | CommandButton | 418 'Novo', 400 'Gravar', 408 'Excluir', 402 'Sair' [L3099-3113] | - | - | - | ALTA |

- **Botões e eventos**
  - `Inicio` [L2379-2409]: abre `rs` (global_68), `global_52`, `global_56`. Incluir -> `ToInsert`; senão `Procura(global_56)` + `Carrega` e `ToUpdate`/`ToDelete`. Depois `Proc_5_37(chkDSR.Value)` (habilita/desabilita grupo DSR conforme o check, L3127-3153) e `Show 3`.
  - `Form_Load` [L2246-2305]: adiciona 'Sem Expediente' em `LstHorarios`, percorre `Horarios` adicionando `NN - Descrição`, `ListIndex = 0`, preenche `cmbDia` com "N - Dia", `Mens_ID`, `Carrega_Msg` (Proc_5_36 L3049-3125), centraliza.
  - `CmdSemana_Click(i)` [L1912-1951] (alterna): se o caption termina em ">>": copia `LstHorarios.Text` para `LblSemana(i).Caption` e troca o caption do botão pelo ID da lista "<<" (`global_006F02D4`); senão (caption "<<"): limpa `LblSemana(i).Caption = ""` e volta ao caption ">>" (`global_006F02B8`). Não marca dirty (nenhum `global_60 = True` nesse trecho - lacuna).
  - `LstHorarios_KeyPress` Enter [L2102-2121]: procura o primeiro `LblSemana(i)` (i = 0..6) com caption vazio e dispara o click do botão do dia (atalho para preencher dia a dia).
  - `chkDSR_Click` [L2168-2214]: Value = 0 -> desabilita `fraDSR`, `txtDSR`, `Label2`, `Label3`, `cmbDia`; senão habilita; marca dirty.
  - `TxtCodigo_Validate` [L1998-2038]: `Format(PadNCodigo(...,2),"00")`; branco -> msg 41; se Incluir: `Procura`; se já existe -> `Carrega` e msg 1172 (292): Sim -> `ToUpdate`; Não -> `Limpa` + foco.
  - `cmdNovo_Click` [L2056-2076], `cmdSair_Click` [L2078-2100]: msg 1171 se dirty (como alarmes); Novo -> `ToInsert`.
  - `cmdGravar_Click` [L2123-2141]: `Consistir`; Incluir -> `Insere` + `ToUpdate`; senão `Altera`.
  - `cmdExcluir_Click` [L2143-2166]: MsgBox(`106 & vbCrLf & 47`, 308, título 3 'Atenção'); Sim (6) -> `DELETE FROM Jornadas WHERE Codigo = <Val(TxtCodigo)>` (L2152-2153), `PreencherComboCod`, `Unload Me`.
  - `PreencherComboCod` [L2411-2457]: `SELECT * FROM Jornadas Order By Codigo` (L2422); combo `NN - Descrição`; seleciona o atual.
  - `Consistir` (Proc_5_27 L2583-2677), ordem: (1) código "00"; `Procura`; (2) Incluir e existe -> msg 1170 'Registro já cadastrado!'; (3) código em branco -> msg 41; (4) `Val(codigo) = 0` -> msg 42 'O Código não pode ser Zero!' (L2615); (5) descrição vazia -> msg 43 'Descrição em Branco!' (foco em TxtDescricao); (6) Horário semanal: `Right(txtDSR.Text, 2)` (minutos) validado -> se inválido msg 104 'Horário Semanal Inválido!' (a condição `If 0 Then` é truncada: aparentemente testa `IsDate`/faixa dos minutos/horas); senão calcula `minutos = Val(Left(txt,2))*60 + Val(Right(txt,2))` (L2649-2652) e, se maior que o limite semanal -> msg 105 'Horário Semanal Maior que 44 Horas!' (44 h = 2640 min, o literal da comparação está truncado mas é o limite declarado na mensagem). Não fica claro se (6) só roda com `chkDSR` marcado (os dados reais têm JornSemanal=0 com TrataDSR=0, o que seria inválido se (6) rodasse sempre com 00:00).
  - `Carrega` (Proc_5_31 L2769-2936): lê `Codigo` ("00"), `Descricao`; para cada dia i=0..6 lê a coluna do dia (Select Case: Domingo..Sábado; ordem das colunas ao ler segue o array de dias 0..6 = Domingo, Segunda, Terça, Quarta, Quinta, Sexta, Sábado) -> se código = 0/nulo: `LblSemana(i).Caption = "Sem Expediente"` (res 53); senão executa `SELECT * FROM Horarios WHERE Codigo = <cod>` (L2828): se não achou -> caption `NN - * Não Cadastrado *` (L2838); se achou -> `NN - Trim(Descricao)` (L2851). Ajusta o caption do botão do dia para "<<". `chkFalta = NaoMarcarFalta` (Null -> ""->0), `chkDSR = TrataDSR`, `txtDSR = JornSemanal` convertido a "HH:MM" (`Format(minutos \ 60, "00") & ":" & Format(minutos Mod 60, "00")`, L2904-2911) ou "44:00" se Null, `cmbDia.ListIndex = IIf(IsNull(DiaDSR), 0, DiaDSR - 1)` (L2923); `PreencherComboCod`; dirty = 0.
  - `Insere` (Proc_5_32 L2938-2978): monta o vetor `dias(0..6)` com `IIf(IsNumeric(Left(Trim(LblSemana(i)), 2)), Val(Left(Trim(LblSemana(i)),2)), 0)` (o "" do decompilado deve ser 0; L2950) e: `INSERT INTO Jornadas (Codigo, Descricao, Domingo, Segunda, Terca, Quarta, Quinta, Sexta, Sabado, NaoMarcarFalta, TrataDSR, JornSemanal, DiaDSR) VALUES ( <cod>,'<desc esc>',<dom>, <seg>, <ter>, <qua>, <qui>, <sex>, <sab>, <chkFalta.Value>, <chkDSR.Value>, <minutos>, <cmbDia.ListIndex+1>)` (L2962-2969). Em seguida `Requery`, `PreencherComboCod` e recarrega.
  - `Altera` (Proc_5_30 L2707-2767): `UPDATE Jornadas  SET  Descricao = '<desc>', Domingo = d0, Segunda = d1, Terca = d2, Quarta = d3, Quinta = d4, Sexta = d5, Sabado = d6, NaoMarcarFalta = <chkFalta>, TrataDSR = <chkDSR>, JornSemanal = <minutos>, DiaDSR = <cmbDia.ListIndex(+1)> WHERE Codigo = <cod>` (L2737-2746; o `ListIndex(1)` está truncado, deduzido +1 pelo simétrico em Carrega L2923 - MÉDIA).
  - `Limpa` (Proc_5_33 L2980-3015): zera Código/Descrição, `LblSemana(i)=""`, volta cada botão ao ">>", `chkFalta = 0`, `chkDSR = 0`, `cmbDia.ListIndex = 0`, `txtDSR = "00:00"`, dirty = 0.
  - Outros: `ToInsert` (L2480-2511), `ToUpdate` (L2513-2540), `ToDelete` (L2542-2563), `Procura` (L2565-2581), `Habilita/Desabilita`, `Carrega_Msg`.
- **Mensagens**:

| ID | Texto | Quando | Botões/ícone |
|---|---|---|---|
| 41 | 'Código Branco!' | código vazio | 48, 'Aviso!' |
| 42 | 'O Código não pode ser Zero!' | `Val(codigo)=0` em Consistir | 48, 'Aviso!' |
| 43 | 'Descrição em Branco!' | descrição vazia | 48, 'Aviso!' |
| 104 | 'Horário Semanal Inválido!' | formato de horas semanais inválido | 48, 'Aviso!' |
| 105 | 'Horário Semanal Maior que 44 Horas!' | total semanal > 44 h | 48, 'Aviso!' |
| 106 + 47 | 'Pode haver Funcionários utilizando esta jornada. Após a exclusão verifique e atualize o cadastro de Funcionários.' + CRLF + 'Confirma Exclusão?' | Excluir | 308 (Sim/Não, default Não, exclamação), título 3 'Atenção' |
| 1170 | 'Registro já cadastrado!' | Incluir duplicado (Consistir) | 48 |
| 1171 | 'Deseja perder as alterações feitas neste registro?' | Novo/Sair com dirty | 292, 'Aviso!' |
| 1172 | 'Registro já cadastrado. Altera?' | Código existente ao sair do campo em Incluir | 292, 'Aviso!' |
| 53 | 'Sem Expediente' | item 0 da lista / dia sem faixa | - |
| 16, 17 | 'A jornada associada a este funcionario foi excluída.' / 'Associe uma nova jornada ao funcionário e tente novamente.' | usados na apuração (fora deste form) quando Funcionario aponta para Jornada inexistente | - |

- **Regras de negócio**
  1. Código 1..99 (2 dígitos) único; 0 proibido (msg 42). Não há mensagem 'maior que 99' neste form (limitado por MaxLength 2 - MÉDIA).
  2. Cada dia aponta para `Horarios.Codigo` (0 = Sem Expediente); exibido/parseado pelos 2 primeiros caracteres do caption (`Left(Trim(caption),2)`).
  3. `NaoMarcarFalta` e `TrataDSR`: 0/1 (CheckBox.Value). `JornSemanal`: MINUTOS (HH*60+MM), limite 44 h; `DiaDSR` 1..7 (1 = Domingo ... 7 = Sábado, ordem de cmbDia).
  4. Ao carregar jornada com Horarios inexistente mostra `* Não Cadastrado *` (e na gravação vira 0 por não ser numérico? - não: o caption começa com os 2 dígitos do código, portanto o código antigo é PRESERVADO ao regravar) - ver sutileza: `Left(Trim("07 - * Não Cadastrado *"),2)` = "07" (ALTA pela lógica do parse).
  5. Excluir não verifica dependências (aviso apenas textual).
- **Hipóteses/lacunas**: SQL exato do preenchimento da lista Horarios; condição de msg 104; condicionalidade da validação semanal a chkDSR; `ListIndex(1)` do DiaDSR; MaxLength; se a gravação de dias com "Sem Expediente" realmente produz 0 (o `""` do IIf no decompilado seria erro de tipo em CInt - tratado como 0).

---

### FrmLocalizar - Procura de usuário (tabela Usuarios)  (linhas 3155-3316)  [Confiança geral: MÉDIA-ALTA]

- **Propósito**: janela de pesquisa (aberta com F10 no campo Usuário de `FrmSenhas`, L4837-4851: `KeyCode = 121` seta `global_006F0094 = "Usuarios"`, `global_006F0098 = "Usuari01"` e `global_006F00A0 = FrmSenhas.TxtUsuario`; `FrmLocalizar.Show 2`). Handlers chamam-se "FrmF10S.*" (cópia de uma tela F10 genérica). Caption 1013 'Procura - Tabela Senha'. ALTA.
- **Controles**:

| Nome | Tipo provável | Rótulo/Caption | Conf. |
|---|---|---|---|
| (form) | Form | 1013 'Procura - Tabela Senha' [L3287-3288] | ALTA |
| SSFrame1 | SSFrame/Frame | 363 'Usuário - Senha' [L3289-3292] | ALTA |
| Label1 | Label | 346 '&Usuário' [L3294-3295] | ALTA |
| Label2 | Label | 347 '&Senha' [L3296-3297] (rótulo existe no form, uso real não visível) | MÉDIA |
| TxtProcura | TextBox (pesquisa incremental) | - | MÉDIA |
| LstProcura | ListBox (nomes de usuários) | - | MÉDIA |
| CmdOk | CommandButton | 9801 '&Ok' [L3298-3301] | ALTA |
| CmdSair | CommandButton | 402 'Sair' [L3302-3303] | ALTA |

- **Botões e eventos**
  - `Form_Load` [L3157-3200]: Carrega_Msg (L3284-3315), centraliza, abre a tabela de usuários pelo nome em `global_006F0094` com índice em `global_006F0098` (DAO Table + Index "Usuari01" - índice por coluna `Usuario`, schema: `Usuarios(Usuario Text(20), Senha Text(20))`, PK Usuario). Se vazia -> msg 98 'Sem Cadastros na Tabela.' (48, título 'Aviso!'); senão percorre o recordset e adiciona o campo (Usuario) em `LstProcura`; `ListIndex = 0`.
  - `TxtProcura_Change` [L3244-3269]: busca incremental: para `i = 0..ListCount-1` compara `UCase$(Left$(LstProcura.List(i), Len(texto))) = UCase$(texto)` e seleciona o primeiro item que começa com o texto; sem acerto volta ao item 0 (`ListIndex = 0`, L3259).
  - `TxtProcura_KeyPress` Enter [L3271-3282] = `cmdOK_Click`. `cmdOK_Click` [L3213-3227]: `global_006F00A0.Text = Trim(LstProcura.Text)` (devolve o usuário ao TextBox do chamador) + `Unload Me`. `cmdSair_Click` = `Unload Me` [L3229-3242]. `Form_Unload` fecha o recordset [L3202-3211].
- **Mensagens**: 98 'Sem Cadastros na Tabela.' (48, 'Aviso!') [L3176-3178]. Nota: a tela de usuários (FrmSenhas L4846-4851) também mostra msg 121 'Sem Cadastros na Tabela.' (64, título 'Aviso!') se a tabela estiver vazia antes de abrir FrmLocalizar.
- **Regras**: lista só nomes de usuário (não exibe senha); pesquisa por prefixo case-insensitive. **Lacunas**: se `Label2 'Senha'` e a lista mostram algo além do usuário (provavelmente só o nome).

---

### FrmMarcDespr - Marcações não consideradas (desprezadas) de um dia  (linhas 3317-3631)  [Confiança geral: BAIXA-MÉDIA]

- **Propósito**: janela flutuante aberta a partir da grade de apuração (`FrmApura.GridEspelho`) ao clicar na coluna 7 (coluna de marcações desprezadas/extras) de um dia (L69676-69692: `global_006F012C = data da linha` (`CDate(CDate(Row + global_64) - 1)`), `global_006F013C/global_006F0140 = limites do dia (tabela global_006F0438 indexada pelo dia 1..31)`, `FrmMarcDespr.Show 10`). Lista as marcações (tabela `Marcacao`) do funcionário (`global_006F0134` = Cracha) naquele dia que NÃO foram consideradas na apuração, permitindo arrastá-las (drag and drop de `lblDrag`) para uma célula (Entrada/Refeição/Retorno/Saída/Extra) da grade do espelho (L69440-69470 trata o drop; L50300-50304: a apuração usa `Entrada_Saida = -1` para marcação desprezada: `DELETE FROM Marcacao WHERE Data_Hora BETWEEN #..# AND #..# AND Entrada_Saida = -1 AND (Tipo = 256 OR Tipo = ...)`). Caption 1019 'Marcações não consideradas'. ALTA para propósito/captions; MÉDIA para o filtro; BAIXA para detalhes do layout (decompilação muito degradada).
- **Controles**:

| Nome | Tipo provável | Rótulo/Caption | Conf. |
|---|---|---|---|
| (form) | Form | 1019 'Marcações não consideradas' [L3374-3375] | ALTA |
| GridDesprezadas | MSFlexGrid (coluna única com cabeçalho `"^" & Format(data, "dd/mm/yy")`, centralizado; linhas de horários) | cabeçalho = data do dia [L3417-3419] | MÉDIA |
| lblDrag | Label (fantasma de arrasto) | texto da célula arrastada [L3325-3331] | MÉDIA |
| btnFechar | CommandButton | 433 '&Fechar' [L3376-3378] | ALTA |

- **Botões e eventos**
  - `btnFechar_Click` [L3345-3358]: `Unload Me`.
  - `Form_Load` [L3360-3515]: centraliza, define caption/botão; posiciona o form junto à célula clicada em `FrmApura.GridEspelho` (Top = `GridEspelho.Top + FrmApura.Top + RowPos(Row)`, Left análogo com `ColPos(Col)`), limitando às bordas da tela (L3380-3413); cabeçalho do grid; abre o recordset de marcações do dia (SQL truncado) e preenche `GridDesprezadas` com as marcações que satisfazem: mesmo `Cracha` (`global_006F0134`) e `Data_Hora` entre o início do dia (`global_006F012C`/`global_006F0140`) e `DateAdd("d", 1, ...) + global_006F013C` quando `global_006F0474 = True` (flag de horário noturno/virada de data), ou entre o dia e o dia seguinte quando `global_006F0474 = False` (L3462); considera as marcações em que `Entrada_Saida = -1` (4294967295 sem sinal) ou `Tipo = 7` (L3471: `(var_38 = 4294967295) Or (1 = 7)`; o `1 = 7` é o `Tipo`, truncado); cada uma vira uma linha com `Format(Data_Hora, "hh:nn")` (a string do formato foi corrompida para "Data_Hora"); a célula fica em negrito conforme o Tipo; a cor da fonte: `IIf((Tipo And 255) = 7, 0, 16711680)` (preto para Tipo 7 = inserida manualmente, azul &HFF0000 BGR para as demais); itálico por flag truncada. Remove a linha em branco extra (`RemoveItem Rows-1` se `Rows > 2`).
  - `MontaGridDespr` [L3517-3630]: mesma montagem (usada após arrastar); se não sobra nenhuma linha (`Rows <= 2`): limpa a célula (Row = 1, Text = "") e `Unload Me` (fecha a janela quando acabaram as marcações desprezadas) [L3616-3621].
  - `GridDesprezadas` evento 'E' (MouseDown, nome corrompido) [L3319-3343]: se botão = 1 e `Row > 0`: copia o texto da célula para `lblDrag.Caption`, posiciona `lblDrag` sobre a célula (Move), `lblDrag.Drag 3` (inicia/encerra? `Drag 3` = vbBeginDrag...), `global_006F0138 = 1` (origem do arrasto = esta janela) e `global_006F013A = Row`; depois `MontaGridDespr`. O drop acontece em `FrmApura` (L69440-69470: ao soltar em `FrmApura.GridEspelho` atualiza a marcação para a coluna destino, limpa a grade `LimpaGridEspelho` e `Unload FrmMarcDespr`).
- **SQL reconstruído** (indireto, de L50300-50304, L68080, L69142-69236, L71781): `UPDATE Marcacao SET Entrada_Saida = <col> ... WHERE Cracha = '..' AND Data_Hora = #yyyy/mm/dd hh:nn:ss#` ao arrastar para uma coluna; `UPDATE Marcacao SET Entrada_Saida = -1 WHERE Data_Hora = #..# AND Tipo = 0 AND Entrada_Saida = <col>` ao devolver para desprezadas (L69211-69212). SQL de seleção deste form: NÃO legível.
- **Mensagens**: nenhuma MsgBox; 1019 e 433 apenas.
- **Regras**: `Marcacao.Entrada_Saida = -1` = marcação desprezada/não considerada; `Tipo = 7` = marcação inserida; `Tipo = 256/263` etc. conforme apuração (fora do escopo). Janela fecha sozinha quando não há mais linhas.
- **Hipóteses/lacunas**: SQL do preenchimento; semântica exata de `global_006F013C/0140` (limites inferior/superior do dia, hora de virada); `global_006F0474` (provável "horário noturno do funcionário"); layout do grid; o literal `Show 10`.

---

### frmFeriados - Tabela de Feriados  (linhas 3632-4503)  [Confiança geral: ALTA]

- **Propósito**: CRUD da tabela `Feriados` (feriados fixos anuais dia/mês). Caption 1002 'Tabela de Feriados' [L4459-4460]; por modo 1270 'Inclusão de Feriado', 1271 'Alteração de Feriado', 1272 'Exclusão de Feriado'. IMPORTANTE: este form NÃO trata `Ferias`: nenhum código de férias existe nas linhas 3632-4503. Férias por funcionário é mantido dentro de `frmCartoes` (abas/lista `lstFerias`, `txtFeriasDe/Ate`; ver seção "Referência cruzada: Ferias" ao final). ALTA.
- **Schema (ALTA)**: `Feriados(Codigo Integer, Descricao Text(30), Dia Integer, Mes Integer)`; PK Codigo; índices Descricao e (Dia, Mes) não únicos; 13 linhas no exemplo (ex.: 21/04 Tiradentes, 25/12 Natal, 19/03 'Feriado São José', 20/11 'Dia Consciencia Negra').
- **Controles**:

| Nome | Tipo provável | Rótulo/Caption | Tamanho/Máscara | Default | Validações | Conf. |
|---|---|---|---|---|---|---|
| fraControles | Frame | 316 'Informações' | - | - | Enabled False em Excluir | ALTA |
| Label1 + TxtCodigo | Label + TextBox | 308 'Código' [L4466-4467] | 2 dígitos ("00"); MaxLength (usado em L3888) | vazio | só dígitos; Validate abaixo | MÉDIA |
| CmbCodigo | ComboBox | `NN - Descrição` | visível só em Alterar | - | Click carrega | MÉDIA |
| Label4 + TxtDescricao | Label + TextBox | 310 'Descrição' [L4472-4473] | Text(30) | vazio | obrigatória | MÉDIA |
| Label2 + TxtDIA | Label + TextBox | 309 '&Dia' [L4468-4469] | numérico, 2 dígitos ("00") | vazio | Validate (msg 39); GotFocus seleciona tudo | MÉDIA |
| Label3 + TxtMes | Label + TextBox | 300 '&Mês' [L4470-4471] | numérico, 2 dígitos ("00") | vazio | Validate (msg 40); GotFocus seleciona tudo | MÉDIA |
| cmdNovo, cmdGravar, cmdExcluir, cmdSair | CommandButton | 418 'Novo', 400 'Gravar', 408 'Excluir', 402 'Sair' [L4474-4490] | - | - | - | ALTA |

- **Botões e eventos**
  - `Inicio` [L4022-4049]: igual aos demais cadastros (`global_68` rs; Incluir -> `ToInsert` com literal "SELECT * FROM Feriados"; senão `Procura`, `Carrega`, `ToUpdate`/`ToDelete`; `Show 3`).
  - `TxtCodigo_Validate` [L3775-3841]: `Format(PadNCodigo(txt,2),"00")`; branco -> msg 41; (condição truncada `esi`) código maior que o limite -> msg 37 'O Código não pode ser maior que 30'; (outro ramo) código zero -> msg 38 'Código não pode ser Zero!'; todas via MsgBox 48 título 'Aviso!'; em Incluir procura duplicado e, se existe, `Carrega` + msg 1172 (292): Sim -> `ToUpdate`; Não -> `Limpa`.
  - `TxtDIA_Validate` [L3661-3680]: se o valor `Val(TxtDIA.Text)` está fora do intervalo (condição truncada `If eax`; esperado >31 ou <1) -> msg 39 'Dia Inválido!' (48, 'Aviso!') e `TxtDIA.Text = ""`. `txtMes_Validate` [L3709-3728]: análogo com msg 40 'Mês Inválido!' (esperado >12 ou <1) e limpa `TxtMes`.
  - `cmdNovo_Click` [L3902-3922], `cmdSair_Click` [L3924-3946]: msg 1171 se dirty (292) como nos demais.
  - `cmdGravar_Click` [L3972-3990]: `Consistir`; Incluir -> `Insere` + `ToUpdate`; senão `Altera`.
  - `cmdExcluir_Click` [L3948-3970]: MsgBox(`47 & vbCrLf & TxtCodigo & " - " & Trim(TxtDescricao)`, 260, 'Aviso!'); Sim -> `DELETE FROM Feriados WHERE Codigo = <Val(TxtCodigo)>` (L3956-3957), `PreencherComboCod`, `Unload Me`.
  - `PreencherComboCod` [L4051-4097]: `SELECT * FROM Feriados Order By Codigo` (L4062).
  - `Consistir` (Proc_8_26 L4202-4331), ordem: (1) código "00"; Procura; (2) Incluir e existe -> msg 1170 'Registro já cadastrado!'; (3) código em branco -> msg 41; (4) `Val(codigo) = 0` -> msg 42 'O Código não pode ser Zero!'; (5) descrição vazia -> msg 43 'Descrição em Branco!'; (6) `Dia` vazio -> msg 44 'Dia em Branco!'; (7) `Mês` vazio -> msg 45 'Mês em Branco!'; (8) `IsDate(Dia & "/" & Mês & "/" & Year(Now))` falso -> msg 46 'Data Inválida!' (48, 'Aviso!'), seleciona o texto de TxtDIA e dá foco (L4301-4309). Cada falha dá foco ao campo correspondente. Retorna True se tudo ok.
  - `Altera` (Proc_8_29 L4361-4392): `UPDATE Feriados  SET  Descricao = '<desc esc>', Dia = <Val(TxtDIA)>, Mes = <Val(TxtMes)> WHERE Codigo = <Val(TxtCodigo)>` (L4368-4371), `Requery`, `PreencherComboCod` x2, recarrega.
  - `Insere` (Proc_8_31 L4421-4436): `INSERT INTO Feriados (Codigo, Descricao, Dia, Mes) VALUES ( <cod>,'<desc esc>', <dia>, <mes>)` (L4424-4427), `Requery`, `PreencherComboCod`, recarrega.
  - `Carrega` (Proc_8_30 L4394-4419): `TxtCodigo = Format(PadNCodigo(Codigo,2),"00")`, `TxtDescricao = TrimNull(Descricao)`, `TxtDIA = Format(Trim(Str(Dia)),"00")`, `TxtMes = Format(Trim(Str(Mes)),"00")`; dirty = 0. `Limpa` (Proc_8_32 L4438-4454): zera os 4 campos.
- **Mensagens**:

| ID | Texto | Quando | Botões/ícone |
|---|---|---|---|
| 37 | 'O Código não pode ser maior que 30' | TxtCodigo_Validate (L3792) | 48, 'Aviso!' |
| 38 | 'Código não pode ser Zero!' | TxtCodigo_Validate (L3807) | 48 |
| 39 | 'Dia Inválido!' | TxtDIA_Validate (L3667) | 48 |
| 40 | 'Mês Inválido!' | txtMes_Validate (L3715) | 48 |
| 41 | 'Código Branco!' | código vazio (L3783, L4223) | 48 |
| 42 | 'O Código não pode ser Zero!' | Consistir (L4236) | 48 |
| 43 | 'Descrição em Branco!' | Consistir (L4250) | 48 |
| 44 | 'Dia em Branco!' | Consistir (L4264) | 48 |
| 45 | 'Mês em Branco!' | Consistir (L4278) | 48 |
| 46 | 'Data Inválida!' | Consistir (L4303) | 48 |
| 47 | 'Confirma Exclusão?' | Excluir (L3950) | 260, 'Aviso!' |
| 1170 | 'Registro já cadastrado!' | Incluir duplicado (L4212) | 48 |
| 1171 | 'Deseja perder as alterações feitas neste registro?' | Novo/Sair com dirty | 292 |
| 1172 | 'Registro já cadastrado. Altera?' | código existente em Incluir | 292 |
| 1002, 1270-1272, 308, 309, 300, 310, 316, 400, 402, 408, 418 | captions | - | - |

- **Regras de negócio**
  1. Código 1..30 (limite do msg 37) - mas a gravação em si não revalida o limite (só o Validate do campo). Código 0 proibido (msgs 38/42).
  2. Dia: 1..31; Mês: 1..12; a combinação é validada com `IsDate(dia/mes/AnoAtual)`: 30/02 e 31/04 são rejeitados; 29/02 só é aceito se o ano corrente for bissexto (comportamento a decidir no port: o feriado é recorrente anual).
  3. NÃO há validação de duplicidade de data (dia/mês) nem de descrição - o índice (Dia,Mes) é não único.
  4. Os feriados são anuais fixos (sem ano). Ao apurar, a data (dia, mês) casa com o feriado (fora deste bloco).
  5. Excluir é DELETE direto.
- **Hipóteses/lacunas**: condições numéricas truncadas (>30; >31; >12), MaxLength de Dia/Mes (2), ordem entre as checagens 37/38 no Validate.

---

## Referência cruzada: Ferias (tabela `Ferias`) - FORA do bloco frmFeriados, documentada aqui por solicitação (frmCartoes, outro agente)

Schema (ALTA): `Ferias(Funcionario Text(16), Inicio Date/Time, Fim Date/Time)`; índices Ferias01 (Funcionario, Inicio, Fim) e Ferias02 (Funcionario); 52 linhas de exemplo (ex.: '0000000000007015', 2014-03-17 a 2014-04-12).
Onde é usado: `frmCartoes` (lista `lstFerias`, `txtFeriasDe`, `txtFeriasAte`, botões `cmdIncluirF`/`cmdEditarF`/`cmdExcluirF`), L22268-22660 e L23971; leitura na apuração: `SELECT Inicio, Fim FROM Ferias WHERE Funcionario = '<cracha>'` (L44306); relatório de férias: `SELECT * FROM Ferias ORDER BY Funcionario` (L54439) e `... WHERE Funcionario BETWEEN '<de>' AND '<ate>'` (L54447); carga da lista: `SELECT * FROM Ferias WHERE Funcionario = '<cod>' ORDER BY Inicio` (L22642). Itens da lista: texto `dd/mm/yyyy` a `dd/mm/yyyy` (parse via `Left(texto,10)` e `Right(texto,10)`).
- Incluir (cmdIncluirF, L22525-22660): sem funcionário selecionado (Codigo vazio ou 0) -> msg 59 'Não foi selecionado nenhum funcionário.' (48, 'Aviso!'); data inicial inválida -> msg 46 'Data Inválida!' (16, 'Aviso!'); data final inválida -> msg 46; `DateDiff("d", inicio, fim) < 0` -> msg 60 'A data final deve ser maior qua a data inicial.' (16); se o início é anterior a hoje (`CDate(inicio) < Date`) -> msg 61 'O período de férias cadastrado começa antes da data atual. Se existirem marcações apuradas deste funcionário dentro do período de férias, será necessário utilizar a função RESTAURA da tela de apuração de ponto para que os dados sejam processados corretamente.' (64, título 3 'Atenção'). Em edição, remove o par antigo (`DELETE FROM Ferias WHERE Funcionario='..' AND Inicio=#yyyy-mm-dd# AND Fim=#yyyy-mm-dd#`) e depois `INSERT INTO Ferias (Funcionario, Inicio, Fim) VALUES ( '<cod>',#yyyy-mm-dd#,#yyyy-mm-dd#)` (L22624-22650; datas ISO). Recarrega a lista ordenada por Inicio. Não há checagem visível de sobreposição de períodos.
- Editar (L22268-22353): sem seleção -> msg 56 'Não foi selecionado nenhum período de férias.' (48, 'Atenção'); caso contrário copia datas para os campos e muda o caption do botão para 400 'Gravar'.
- Excluir (L22319-22350): sem seleção -> msg 56; senão MsgBox(`57 'Remover esse período de férias pode causar problemas ' & CRLF & 58 'no espelho de ponto do(a) funcionário(a):' & CRLF & Codigo & " - " & Nome & CRLF & CRLF & 47 'Confirma Exclusão?'`, 292, 'Aviso!'); Sim -> `DELETE FROM Ferias WHERE Funcionario = '<cod>' AND Inicio = #yyyy-mm-dd# AND Fim = #yyyy-mm-dd#` e remove o item da lista.
- Campos de data usam máscara "__/__/____" (`txtFeriasDe/Ate`, L22366-22420) com rotina `Geral.ValeData` (L51078 região: valida; se inválida, restaura "__/__/____" e MsgBox 46).
Marcar como MÉDIA: leitura parcial (decompilado degradado); detalhes completos cabem ao agente de frmCartoes.

---

## Resumo de lacunas desta parte (para o port)
1. SQL de carga das listas do frmCadGeral (apenas Justificativas é literal); coluna comparada em `cmdBuscar`; significado de TipoLista 100/101/102.
2. Sentido exato das comparações de modelo `"MD5751 / MD5753"` / `"BioLite"` (operadores perdidos); implementar por tabela: BioLite -> SireneBioLite (24 alarmes: 12 útil + 12 fim de semana, sem Duração por registro), demais -> Sirene (20 alarmes, Duração 1..7).
3. Limites numéricos truncados: faixa de código dos alarmes (1..20/24), código máximo de Feriados (30), Dia (1..31), Mês (1..12), JornSemanal (44 h).
4. `FrmMarcDespr`: SQL de seleção e flags `global_006F0474/013C/0140` (virada de dia/horário noturno) não decifrados.
5. Não havia dumps de controles (S\forms) nem ajuda (S\help) para confirmar MaxLength/máscaras.
