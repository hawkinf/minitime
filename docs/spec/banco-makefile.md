# 08 - Makefile (L61699-66670) e FrmMakeFile (L10712-10899)

> Todas as referencias `Lnnnn` sao numeros de linha de `S\MiniTime_utf8.txt`. Confianca: ALTA / MEDIA / BAIXA.

## 0. AVISO IMPORTANTE: a premissa do BRIEF nao se confirma

O BRIEF descreve o modulo `Makefile` como "geracao dos arquivos de exportacao para folha de pagamento". **Isso esta errado.**

- O modulo `Makefile` (L61699-66670) tem **3 procedimentos**; o nome verdadeiro do principal, dado pelo handler de erro, e `Makefile.CriaAtualizaBD` (L66592). **Ele cria o banco Access (DIMEP.Mdb) e atualiza o esquema** (tabelas, colunas, indices, migracao de versao). [ALTA]
- Os outros dois sao `MakeFile.CriaTabelasTmp` (L66601-66640, handler L66620/L66621 -> nome literal na L66622, ver 4) e uma rotina auxiliar sem nome (L66642) que opera sobre o arquivo `.Mdb`.
- `FrmMakeFile` (L10712-10899) e a **tela "Diretorio p/ Execucao"** que pergunta onde criar o DIMEP.Mdb quando ele nao existe; ela chama `CriaAtualizaBD`. Nao gera arquivo de exportacao.
- **Nenhum layout de arquivo de exportacao (TipoExp / FiltroExp / NumDigAno / Digitos...) existe neste bloco.** O unico resultado deste bloco relevante para exportacao e: (a) a **definicao exata das colunas de `Parametros`** que guardam as opcoes de exportacao (secao 3.8) e (b) a migracao que forca `Funcionario.Codigo` e `Marcacao.Cracha` a 16 digitos com zeros a esquerda (secao 5).
- Onde o formato de exportacao realmente esta (para o orquestrador reatribuir): ver **Apendice A** (pistas com linhas, e uma analise PRELIMINAR de `Geral.GeraExportacao`, L50387-50527, que e quem escreve a linha de cada marcacao).

Fontes `S\forms\` e `S\help\` : conferidas no inicio e no fim; **nao existem** (pastas vazias/inexistentes). Nada incorporado delas.

---

## 1. Tabela de procedimentos

| Nome real (via handler de erro) | Nome do decompilador | Linhas | O que faz | Conf. |
|---|---|---|---|---|
| `Makefile.CriaAtualizaBD` (handler L66592) | `Proc_61_0_691EB0` (L61701) | L61701-66599 (~4900 linhas, 99% e declaracao de esquema + checagens de limite de array) | Cria o MDB (se nao existe) e sincroniza o esquema declarado no codigo com o do arquivo: cria tabelas/colunas/indices que faltam; reconstrói `Parametros` se `Tipo_Relogio` mudou de tamanho; migra codigos para 16 digitos e grava `Parametros.Versao`. Parametro unico (MEDIA): caminho completo do `.Mdb`. | ALTA (funcao) / MEDIA (parametro) |
| `MakeFile.CriaTabelasTmp` (handler L66621-66622) | `Proc_61_1_6A3BD0` | L66601-66640 | Apaga (se existirem) e recria as tabelas temporarias `Tmp_Inf` e `Tmp_Marcacao` com `CREATE TABLE` (usadas pelos relatorios/espelho). | ALTA (SQL literal) |
| (sem handler; `On Error Resume Next`) | `Proc_61_2_6A4320(arg_C)` | L66642-66670 | Funcao auxiliar sobre o arquivo `.Mdb`: se `Dir(arg_C)` acusa que existe, executa uma chamada de metodo nao identificada (`UnkVCall_58h`, L66656) e le `Err.Number` (L66665); parece garantir atributo/acesso do arquivo (hipotese: `SetAttr`/remover somente-leitura). | BAIXA |
| `FrmMakeFile.CmdMake_Click` | `CmdMake_UnknownEvent_9` | L10872-10899 -> na verdade L10779-10851 (ver secao 7) | Botao "Gerar": testa gravacao no diretorio, avisa se DIMEP.mdb nao existe, chama `CriaAtualizaBD`, descarrega o form. | ALTA |
| `FrmMakeFile.CmdSair_Click` | `cmdsair_UnknownEvent_9` | L10715-10724 | Executa `End` (encerra o programa). | ALTA |
| `FrmMakeFile.Drive_Change` (handler L10859) | `Drive_Change` | L10854-10880 | Troca de unidade; erro -> `LoadResString 108` "Drive nao disponivel no momento". Obs.: a etiqueta do handler de `Diretorio_Change` (L10737) tambem diz "Drive_Change". | ALTA |
| (rotulo `Diretorio_Change` ; handler diz `FrmMakeFile.Drive_Change`, L10736) | `Diretorio_Change` | L10726-10741 | Copia `Diretorio.Path` para `TxtDiretorio.Text` e ajusta `Drive.Drive`. | ALTA |
| `FrmMakeFile.Form_Load` | `Form_Load` | L10743-10756 | Chama `Carrega_Msg` e preenche `TxtDiretorio` com `Diretorio.Path`. | ALTA |
| (sem handler proprio) | `TxtDiretorio_Change` | L10758-10772 | `On Error Resume Next`: `Diretorio.Path = TxtDiretorio.Text`. | ALTA |
| `FrmMakeFile.TxtDiretorio_GotFocus` | `TxtDiretorio_GotFocus` | L10774-10788 (aprox.) | Seleciona todo o texto. | ALTA |
| `FrmMakeFile.Carrega_Msg` | `Proc_20_7_5091F0` | L10882-10899 | Carrega captions (ver 7.3). | ALTA |

(Numeros de linha de FrmMakeFile conferidos no arquivo: objeto inicia em L10712; `cmdsair` L10714; `Diretorio_Change` L10726; `Form_Load` L10743; `TxtDiretorio_Change` L10757; `TxtDiretorio_GotFocus` L10774; `CmdMake` L10790; `Drive_Change` L10854; `Carrega_Msg` L10881. Use as linhas por evento dadas na secao 7.)

---

## 2. `Makefile.CriaAtualizaBD` - fluxo [ALTA no desenho geral, MEDIA nos detalhes de DAO]

O codigo usa **DAO/Jet** (objetos Workspace/Database/TableDef/Field/Index; `GUID(...)` nos `For Each` e o decompilador mostrando `UnkVCall_5Ch` = `Database.Execute`). A senha do MDB e a **constante `<senha-do-mdb>`** (L65883, L65891; tambem L18488 e muitas outras telas: L6720, L7837, L8253, L12648, L17649...). Para o port: o arquivo `DIMEP.Mdb` e protegido por senha de banco `<senha-do-mdb>` (MEDIA/ALTA: formato `;pwd=<senha-do-mdb>`).

### 2.1 Declaracao do esquema em memoria (L61728-65873)
- Quatro arrays locais: `var_4C(14,24,24)` (indices x campos do indice), `var_84(14,30)` (colunas por tabela), `var_A8(14,24)` (indices por tabela), `var_C8(14)` (nomes das tabelas) (L61728-61731). Cada coluna e um registro de 12 bytes: nome (BSTR em +0), tipo DAO (Integer em +4, `CInt(n)`), tamanho (Integer em +6, **o literal foi perdido pelo decompilador**; ver 3), flag em +8 (usada para marcar "ja existe") [MEDIA].
- Lista de tabelas, indice 0..13, ordem exata (L61738-61816): `0 Sirene, 1 Funcionario, 2 Horarios, 3 Jornadas, 4 Feriados, 5 Ferias, 6 Marcacao, 7 Parametros, 8 Backup, 9 Terminal, 10 Usuarios, 11 Justificativas, 12 Templates, 13 SireneBioLite` [ALTA]. As tabelas `Tmp_Inf` e `Tmp_Marcacao` NAO estao nessa lista (sao criadas por `CriaTabelasTmp`).
- Constantes de tipo (DAO): `CInt(1)` = Boolean (dbBoolean), `CInt(3)` = Integer (dbInteger -> OLE 2), `CInt(8)` = Date/Time (dbDate -> OLE 7), `CInt(10)` = Text (dbText -> OLE 130), `CInt(12)` = Memo (dbMemo) (usado so em `Templates.Template`, L65559). Corrobora com `S\schema_out.txt`: bool = 11, int curto = 2, data = 7, texto = 130 [ALTA para a equivalencia, pois bate coluna a coluna com o MDB real].

### 2.2 Abrir/criar o banco (L65874-65896)
1. `Proc_45_19_634930(path)` = `Arquivos.Existe` (handler L51290: `Arquivos.Existe`; corpo L51267-51310: `Dir(arg)`; devolve True se o arquivo existe) [ALTA].
2. Se **nao existe**: cria o MDB com o localizador `";LANGID=0x0409;CP=1252;COUNTRY=0"` concatenado a `";pwd="` + `"<senha-do-mdb>"` (L65882-65884) -> `Workspace.CreateDatabase` (L65884-65885, chamada `UnkVCall_5Ch`) [MEDIA: a chamada e anonima, mas as strings sao literais]. Ou seja: banco novo = Jet, **LANGID 0x0409 (ingles EUA), code page 1252, com senha**.
3. Se **existe**: abre com `";pwd=" & "<senha-do-mdb>"` (L65891-65892) [ALTA para a string].

### 2.3 Passada 1 - quais tabelas ja existem (L65900-65934)
`For Each` sobre `TableDefs`; para cada tabela do arquivo compara `UCase(nome)` com cada nome declarado (L65925) e marca a declarada como existente (o marcador, indice `+4`, e lido em L65958 `If edx+ecx*8+4 = 0`). [MEDIA]

### 2.4 Passada 2 - por tabela declarada (L65938-66439) [MEDIA]
Para cada tabela `i` com nome nao vazio (L65950-65951):
1. **Se nao existe** (L65958): `CreateTableDef(nome)` (L65964); **se existe**: pega o `TableDef` existente (L65975).
2. **Colunas existentes** (L66109-66166): `For Each` nos campos do `TableDef`, compara `UCase(campo existente)` com `UCase(coluna declarada)` (L66147) e marca a declarada como existente.
   - **Caso especial `Tipo_Relogio`** (L66056): se a coluna encontrada tem tipo texto (`= 10`), o nome e **"Tipo_Relogio"** e o **tamanho declarado difere do tamanho do campo existente** (`<> var_254`), faz **reconstrucao da tabela inteira**:
     - `SELECT * INTO BakTmp_<tabela> FROM <tabela>` (L66068)
     - `DROP TABLE <tabela>` (L66075)
     - chama **recursivamente** `CriaAtualizaBD` (L66084) e sai (L66085).
     - Na reentrada a tabela nao existe, e criada ja com o tamanho novo; ao final da passada, se o flag modulo-global `global_006F0248(i)` e True (L66413) faz `INSERT INTO <tabela> SELECT * FROM BakTmp_<tabela>` (L66424) e `DROP TABLE BakTmp_<tabela>` (L66431). [MEDIA: onde o flag e setado nao esta visivel]
     - Consequencia pratica: o tamanho de `Parametros.Tipo_Relogio` foi ampliado numa versao anterior (hoje o MDB real tem **255**, `schema_out.txt`: `Tipo_Relogio type=130 len=255`). O tamanho declarado no codigo e um literal perdido; supoe-se 255 (MEDIA).
3. **Colunas que faltam** (L66167-66260): para cada coluna declarada nao marcada como existente (teste `+8 = 0`, L66200): `CreateField(nome, tipo, tamanho)` (L66211-66237; tamanho em +6, tipo em +4) e, **se tipo texto (10)**, liga a propriedade "permitir comprimento zero" (`AllowZeroLength = True`, L66250-66251, valor `-1`), depois `TableDef.Fields.Append`. Portanto **toda coluna Text e criada com AllowZeroLength = True** [MEDIA].
   - Efeito: o banco antigo ganha colunas novas **no fim da tabela** (nao reordena nem altera tipo de coluna existente, salvo o caso `Tipo_Relogio`).
4. **Indices** (L66263-66394): idem para `var_A8`/`var_4C`: se o indice declarado nao existe (flag +8 = 0, L66296), `CreateIndex(nome)` (L66355), acrescenta cada campo do indice na ordem declarada (loop L66311-66360, `Fields.Append` L66359), ajusta a propriedade (+6; `Primary`/`Unique`, L66386, valor do flag nao visivel) e faz `Indexes.Append`. Esquema real confirma: so indices de nome `PrimaryKey` sao `pk=True uniq=True` (S\schema_out.txt).
5. Tabela nova: `TableDefs.Append(novo)` (L66401-66406) [MEDIA].
6. Restauracao do backup (L66413-66432) quando houve reconstrucao, conforme acima.

### 2.5 Migracao por versao (L66442-66590) [MEDIA - o aninhamento foi perdido pelo decompilador]
Executada depois de garantir o esquema:
1. Abre `SELECT Versao FROM Parametros` (L66442). Se ha linha (L66448): le `Versao` (L66452-66455).
2. Compara com a versao do programa: `Val(CStr(App.Major) & CStr(App.Minor) & CStr(App.Revision))` (L66460-66480). Condicao exata: `Versao < Val(Major & Minor & Revision)` **ou** `IsNull(Versao)` (L66478-66480). O exe instalado tem FileVersion 4.04.0001 -> Major=4, Minor=4, Revision=1 -> `Val("441")` = **441** = `Parametros.Versao` do MDB real (S\q_out.txt) [ALTA].
3. Se precisa migrar:
   - `SELECT Codigo FROM Funcionario` (L66481): para cada linha, **se `Len(Trim(Codigo)) < 16`** (L66498) executa `UPDATE Funcionario SET Codigo = '<Format(Trim(Codigo), String(16,"0"))>' WHERE Codigo = '<codigo antigo>'` (L66512-66514). 
   - `SELECT Cracha FROM Marcacao` (L66521): para cada linha, **se `Len(Trim(Cracha)) < 16`** (L66538), `UPDATE Marcacao SET Cracha = '<Format(Trim(Cracha), String(16,"0"))>' WHERE Cracha = '<cracha antigo>'` (L66552-66554).
   - O caractere de preenchimento `global_0044400C` e a string `"0"` (inferido de L11447: `Checagem = '` & `IIf(..., global_0044AD80, global_0044400C)` => '1'/'0'; e L5604 `Format(0, global_0044400C)`; e L11617/L11680 `Format(txtCartao..., String(16, global_0044400C))`) [MEDIA/ALTA].
   - Isto prova a **regra de negocio**: o cartao e armazenado como texto de 16 posicoes, **zeros a esquerda**, em `Funcionario.Codigo` (L66512) e `Marcacao.Cracha` (L66552); coincide com o schema real (`len=16`) e com os dados (`0000000000007984`).
4. `UPDATE Parametros SET Versao = <Val(Major & Minor & Revision)>` via `Database.Execute` (L66585-66586, texto base L66587). Posicao exata (dentro/fora do `If` de versao) nao confirmavel (indentacao perdida) [BAIXA para aninhamento].
5. Tratamento de erro geral: `On Error GoTo` L61732 -> L66592 `Proc_45_14_6328E0("Makefile.CriaAtualizaBD", ...)` (logger/MsgBox de erro generico) e `Exit Sub` (L66595-66598).

**Observacao de seguranca para o port:** o mecanismo e de **string de conexao DAO com senha em claro no binario** (`<senha-do-mdb>`). Para o port C#/SQLite, nao precisa replicar a senha; precisa apenas ler o MDB legado (importador) se for migrar dados.

---

## 3. Esquema declarado (colunas, tipos, indices)

Convencao: `Linha` = onde o NOME aparece (`ecx = "..."`); o tipo (`CInt(n)`) aparece ~9 linhas depois. Tamanho de texto: **literal perdido no decompilado**, entao uso o MDB real (`S\schema_out.txt`, ALTA para o tamanho real; MEDIA para "o codigo declara o mesmo valor", pois todas as colunas e tipos coincidem 1:1).

### 3.1 Sirene (L61837-61961; indices L61973-62062)
| Coluna | Linha | Tipo DAO | Tipo MDB real |
|---|---|---|---|
| Codigo | 61837 | 3 (Integer) | Integer |
| Descricao | 61858 | 10 (Text) | Text(30) |
| Horario | 61889 | 8 (Date) | Date/Time |
| Duracao | 61910 | 3 | Integer |
| Util | 61931 | 1 (Boolean) | Yes/No |
| FimSem | 61952 | 1 | Yes/No |

Indices: `PrimaryKey(Codigo)` L61973/62008, `Sirene01(Codigo)` L62020/62035, `Sirene02(Descricao)` L62047/62062.

### 3.2 Funcionario (L62083-62375; indices L62406-62494)
Codigo L62083 (Text, **16**), Nome L62114 (Text 50), Cargo L62145 (Text 30), Horario L62176 (Integer), Horario_almoco L62197 (Integer), AutHoraExtra L62218 (Integer), HrMudancaData L62239 (Date), RG L62260 (Text 15), HorarioNoturno L62291 (Boolean), HorarioDiurno L62312 (Boolean), HrMudancaDataDiaLivre L62333 (Date), SentidoMudancaDataDiaLivre L62354 (Integer), Senha L62375 (Text 20).
Indices: `PrimaryKey(Codigo)` L62406/62440, `Funcionario01(Codigo)` L62452/62467, `Funcionario03(Nome)` L62479/62494.

### 3.3 Horarios (L62515-62861; indices L62882-62970)
Codigo L62515 (Integer), Descricao L62536 (Text 30), DeSS1..DeSS4 L62567/62588/62609/62630 (Date), TolManha L62651, TolTarde L62672, TolSaida L62693, RefObrig L62714, RefMinimo L62735, Intervalo L62756, Noturno L62777, NaoTrabalha L62798, TolExtEnt L62819, TolExtInt L62840, TolExtSai L62861 (todas Integer).
Indices: `PrimaryKey(Codigo)` L62882/62916, `XCod(Codigo)` L62928/62943, `XDes(Descricao)` L62955/62970.

### 3.4 Jornadas (L62991-63253; indices L63274-63362)
Codigo L62991 (Integer), Descricao L63012 (Text 35), Segunda L63043, Terca L63064, Quarta L63085, Quinta L63106, Sexta L63127, Sabado L63148, Domingo L63169, NaoMarcarFalta L63190, TrataDSR L63211, JornSemanal L63232, DiaDSR L63253 (todas Integer).
Indices: `PrimaryKey(Codigo)` L63274/63308, `Jornadas01(Codigo)` L63320/63335, `Jornadas02(Descricao)` L63347/63362.

### 3.5 Feriados (L63383-63465; indices L63477-63607)
Codigo L63383 (Integer), Descricao L63404 (Text 30), Dia L63435 (Integer), Mes L63456 (Integer).
Indices: `PrimaryKey(Codigo)` L63477/63511, `Feridos01(Codigo)` (sic - typo no codigo e no MDB) L63523/63538, `Feriados02(Descricao)` L63550/63565, `Feriados03(Dia, Mes)` L63577/63592/63607.

### 3.6 Ferias (L63628-63689; indices L63702-63774)
Funcionario L63628 (Text 16), Inicio L63659 (Date), Fim L63680 (Date).
Indices: `Ferias01(Funcionario, Inicio, Fim)` L63702/63717/63732/63747, `Ferias02(Funcionario)` L63759/63774. Sem PK.

### 3.7 Marcacao (L63795-63973; indices L63995-64079) - a tabela das batidas
| Coluna | Linha | Tipo | MDB real |
|---|---|---|---|
| Cracha | 63795 | Text | Text(16) |
| Data_Hora | 63826 | Date | Date/Time |
| Terminal | 63847 | Integer | Integer |
| Entrada_Saida | 63868 | Integer | Integer |
| Situacao | 63889 | Integer | Integer |
| Tipo | 63910 | Integer | Integer |
| Divergencia | 63931 | Integer | Integer |
| SaiuMarcacao | 63952 | Boolean | Yes/No |
| Justificativa | 63973 | Integer | Integer |

Indices (sem PK): `Marcacao01(Cracha, Data_hora)` L63995/64010/64025, `Marcacao02(Data_hora)` L64037/64052, `Marcacao04(Cracha)` L64064/64079.
**Nota:** `Marcacao02` (so Data_hora) e o indice usado pela exportacao (ver Apendice A, L6E6221 `"Marcacao02"` = L73???, abaixo).

### 3.8 Parametros (L64100-64830; sem indices) - 31 colunas, 1 linha
Aqui estao as colunas que governam a exportacao. Ordem de criacao = ordem abaixo.
| # | Coluna | Linha (nome) | Tipo DAO | MDB real | Significado (nesta fonte so ha o nome; semantica por uso) |
|---|---|---|---|---|---|
| 1 | Nome_Cliente | 64100 | 10 | Text(50) | nome da empresa |
| 2-5 | Comunic_Height / Left / Top / Width | 64131/64152/64173/64194 | 3 | Integer | geometria da janela de comunicacao |
| 6 | **Digitos** | 64215 | 10 | **Text(2)** | (ver 3.9) quantidade de digitos do cartao; no MDB real vazio |
| 7 | UltUsuario | 64246 | 10 | Text(20) | ultimo usuario logado |
| 8 | ApuraHeight | 64277 | 3 | Integer | altura da janela de apuracao |
| 9 | CNPJ | 64298 | 10 | Text(19) | |
| 10 | DiaFechamento | 64329 | 3 | Integer | dia de fechamento do periodo |
| 11-13 | TolManha / TolTarde / TolSaida | 64350/64371/64392 | 3 | Integer | tolerancias padrao |
| 14 | **TipoExp** | 64413 | 3 | Integer | tipo (layout) de exportacao |
| 15 | **NumCartao** | 64434 | 3 | Integer | numero de digitos do cartao exportado |
| 16 | **NumDigAno** | 64455 | 3 | Integer | digitos do ano (2 ou 4) na data exportada |
| 17 | **DigVerificador** | 64476 | 3 | Integer | usa digito verificador (0/1) |
| 18 | **FiltroExp** | 64497 | 3 | Integer (pode ser Null) | filtro de quais marcacoes exportar |
| 19 | Velocidade | 64518 | 3 | Integer | velocidade serial (19200 no MDB real) |
| 20 | **DisqVirtual** | 64539 | 3 | Integer | "disquete virtual" (chkDisqVirtual em FrmParametros, L5307/L5666) |
| 21 | Versao | 64560 | 3 | Integer | versao do esquema/app (441) |
| 22 | HorarioVeraoAtivo | 64581 | 1 | Yes/No | |
| 23-24 | InicioHorarioVerao / FimHorarioVerao | 64602/64633 | 10 | Text(4) / Text(10) | |
| 25 | Tipo_Relogio | 64664 | 10 | **Text(255)** | modelo do relogio (ex.: "Mini Point"); coluna sujeita a reconstrucao (2.4) |
| 26 | **QtdeDig** | 64695 | 10 | **Text(2)** | quantidade de digitos (texto; padrao `'04'`) |
| 27 | Checagem | 64726 | 1 | Yes/No | |
| 28 | **ExpMonitoracao** | 64747 | 1 | Yes/No | exportar monitoracao |
| 29 | **NomeArq** | 64768 | 10 | **Text(90)** | caminho/nome do arquivo de exportacao |
| 30 | TipoEmpresa | 64799 | 10 | Text(1) | |
| 31 | DuracaoSireneBioLite | 64830 | 3 | Integer | |

**Valores de exportacao e defaults que o codigo grava** (fora deste bloco, em `MDIPrincipal.MDIForm_Load`, L18343-18600; contexto, outro agente):
- Nome de arquivo padrao: `var_38 = "MOVIMENT.txt"` (L18343); `global_108 = "DIMEP"` (L18344) -> `DIMEP.Cfg`/`DIMEP.Mdb`.
- Quando `NumCartao` (e ainda `QtdeDig`) estiverem Null: `Update Parametros set NumCartao = 6,  NumDigAno = 2, ExpMonitoracao = '0',  TipoExp = 0, DigVerificador = 0,  NomeArq = '<App.Path>\MOVIMENT.txt'` (L18591-18593; sub-strings L18591, L18592, L18593 em var_54/var_58/var_3C) [ALTA].
- Quando `QtdeDig` Null ou igual ao valor sentinela: `Update Parametros set QtdeDig = '04',  Checagem = '1'` (L18568) [ALTA].
- Linha unica de Parametros no MDB real (S\q_out.txt): `Tipo_Relogio='Mini Point', Velocidade=19200, Versao=441, TipoExp=0, NumCartao=5, DigVerificador=0, QtdeDig='04', Digitos='', Checagem=True`. Note: `NumCartao=5` no MDB real != default 6 do codigo: o usuario alterou (frmParamExp).

### 3.9 Backup (L64861-65018)
Mesmas 8 primeiras colunas de Marcacao, **sem `Justificativa`**: Cracha (Text 16) L64861, Data_Hora L64892, Terminal L64913, Entrada_Saida L64934, Situacao L64955, Tipo L64976, Divergencia L64997, SaiuMarcacao L65018. Sem indices. (Copia das marcacoes coletadas antes do processamento? uso nao confirmado aqui.)

### 3.10 Terminal (L65049-65185)
Endereco L65049 (Integer), Porta L65070 (Integer), FaixaInicio L65091 (Date), FaixaFim L65112 (Date), VersaoFW L65133 (Text 4), Impressora L65164 (Boolean), PenDrive L65185 (Boolean). Sem indices. 1 linha no MDB real (Endereco=1, Porta=1, VersaoFW='2.03').

### 3.11 Usuarios (L65216-65256; indices L65278-65339)
Usuario L65216 (Text 20), Senha L65247 (Text 20). Indices: `PrimaryKey(Usuario)` L65278/65312, `Usuari01(Usuario)` (sic) L65324/65339.

### 3.12 Justificativas (L65360-65412; indice L65433-65467)
Codigo L65360 (Integer), Descricao L65381 (Text 60), Tipo L65412 (Integer). Indice: `PrimaryKey(Codigo)` L65433/65467.

### 3.13 Templates (L65488-65550; indice L65571-65649)
CodigoCartao L65488 (Text 16), Dedo L65519 (Text 1), Template L65550 (**Memo**, `CInt(12)`, L65559). PK composta: `PrimaryKey(CodigoCartao, Dedo)` L65571/65605/65649. (Biometria MiniBio/BioLite.)

### 3.14 SireneBioLite (L65670-65764; indices L65785-65873)
Codigo L65670, Descricao L65691 (Text 30), Horario L65722 (Date), Util L65743 (Boolean), FimSem L65764 (Boolean). Indices: `PrimaryKey(Codigo)` L65785/65819, `SireneBioLite01(Codigo)` L65831/65846, `SireneBioLite02(Descricao)` L65858/65873.

---

## 4. `MakeFile.CriaTabelasTmp` (L66601-66640) [ALTA para o SQL; MEDIA para o DROP]

- Erro: `On Error GoTo` L66607 -> `Proc_45_14_6328E0("MakeFile.CriaTabelasTmp", ...)` (L66623; **atencao: grafia "MakeFile"** com F maiusculo, diferente de "Makefile.CriaAtualizaBD").
- Passo 1 (L66609-66618): `For Each` em `TableDefs`; para as tabelas de nome `Tmp_Inf` (L66611) e `Tmp_Marcacao` (L66616) executa uma instrucao via `Database.Execute` (`UnkVCall_5Ch`, L66618) - o texto nao aparece, mas e o que antecede o `CREATE TABLE` (hipotese: `DROP TABLE <nome>`) [MEDIA].
- Passo 2: SQL literal (concatenado em L66623-66626):

```
CREATE TABLE Tmp_Inf(
  Cartao TEXT(16), Nome TEXT(50), Funcao TEXT(30), Empresa TEXT(50), TipoEmpresa TEXT(1),
  Total TEXT(7), Total_Calc TEXT(7), Total_Atraso TEXT(7), Total_Ext TEXT(7), Total_Dsr TEXT(7),
  Total_Abonado TEXT(7), Total_SdAntec TEXT(7), CNPJ TEXT(25), Data_Inicial TEXT(10), Data_Final TEXT(10),
  Horario TEXT(35))
```
(L66623-66626: `var_801C`, `var_8030`, `var_8044`, `var_60`; execucao L66627)

```
CREATE TABLE Tmp_Marcacao(
  Cracha TEXT(16), Nome TEXT(50), Ent_Manha TEXT(15), Ent_Manha_Just TEXT(60), Sai_Manha TEXT(15),
  Sai_Manha_Just TEXT(60), Ent_Tarde TEXT(15), Ent_Tarde_Just TEXT(60), Sai_Tarde TEXT(15), Sai_Tarde_Just TEXT(60),
  Ent_Extra TEXT(15), Ent_Ext_Just TEXT(60), Sai_Extra TEXT(15), Sai_Extra_Just TEXT(60), Data TEXT(8),
  Dia_Semana TEXT(3), Total_Normais TEXT(7), Total_Calc TEXT(7), Total_Extra TEXT(7), Total_DSR TEXT(5),
  Total_Atraso TEXT(5), Total_Abonado TEXT(5), Divergencia TEXT(50), Status TEXT(11), Tipo TEXT(10),
  Situacao TEXT(30), Data_Apura Date)
```
(L66631-66639; execucao L66640). Corrobora exatamente com `S\schema_out.txt` (Tmp_Inf 16 colunas; Tmp_Marcacao 27 colunas) [ALTA].

- Chamadores (por `Proc_61_1_6A3BD0`): `frmEspelhoPonto` L20514, L20595 (antes de montar o espelho) e `Form_Load` L20748. Atencao: L71328 (`FrmApura`, args `(8, var_120, "QtdeDig")`) aparece como chamada a `Proc_61_1`, mas os argumentos nao combinam com uma rotina sem parametros: provavelmente colisao de indice de procedimento do decompilador (BAIXA).
- Observacao: todos os valores dessas tabelas sao TEXTO ja formatado (horas "HH:MM" etc.), i.e. sao staging de impressao, nao dados mestres.

## 5. Regra de cartao com 16 digitos (resumo)
- `Funcionario.Codigo`, `Marcacao.Cracha`, `Backup.Cracha`, `Ferias.Funcionario`, `Templates.CodigoCartao`, `Tmp_Inf.Cartao`, `Tmp_Marcacao.Cracha`: todos Text(16) (L62083, L63795, L64861, L63628, L65488; SQL L66623, L66631).
- Migracao: pad a esquerda com "0" ate 16 se `Len(Trim(x)) < 16` (L66498, L66538) usando `Format(Trim(x), String(16,"0"))` (L66512, L66552) - **apenas para Funcionario.Codigo e Marcacao.Cracha** (Ferias.Funcionario e Backup.Cracha nao sao migrados aqui).
- A exportacao reduz de novo o cartao a `NumCartao` digitos (Apendice A).

---

## 6. (reservado)

---

## 7. FrmMakeFile - "Diretorio p/ Execucao" (L10712-10899) [Conf. geral: ALTA]

### 7.1 Controles (inferidos do codigo; `S\forms\FrmMakeFile.txt` nao existe) [MEDIA para tipos, ALTA para nomes]
| Nome | Tipo provavel | Rotulo | Notas | Conf. |
|---|---|---|---|---|
| `Caption` do form | Form | "Diretorio p/ Execucao" (`LoadResString 1018`, L10889/10890 via `Global.Caption`) | modal (`FrmMakeFile.Show 1`, L18461) | ALTA |
| `TxtDiretorio` | TextBox | - | texto livre do diretorio; `GotFocus` seleciona tudo (L10781-10784); `Change` repassa ao `Diretorio.Path` com `On Error Resume Next` (L10757-10770) | ALTA |
| `Diretorio` | DirListBox | - | `Change` copia `Path` para `TxtDiretorio` e ajusta `Drive` (L10734-10741) | ALTA |
| `Drive` | DriveListBox | - | `Change` troca `Diretorio.Path` (L10860-10866); usa `Drive.Tag` como trava de reentrada (L10858-10863) | ALTA |
| `CmdMake` | CommandButton | "Gerar" (`LoadResString 2620`, L10893) | cria/atualiza o BD | ALTA (texto) / MEDIA (associacao ao controle: a atribuicao do caption e truncada L10893-10896) |
| `CmdSair` | CommandButton | "Sair" (`LoadResString 402`) | `End` | ALTA |

### 7.2 Eventos
- **`Form_Load`** (L10743-10756): chama `Carrega_Msg` (L10746) e `TxtDiretorio.Text = Diretorio.Path` (L10749). Erro -> "FrmMakeFile.Form_Load" (L10752).
- **`Diretorio_Change`** (L10726; handler diz "Drive_Change", L10736): `TxtDiretorio.Text = Diretorio.Path` (L10734); `Drive.Drive = Left(path, 2)` (L10738).
- **`Drive_Change`** (L10854-10880): se `Drive.Tag <> ""` (reentrada), seta `Tag = ...` (L10858), tenta `Diretorio.Path = Drive.Drive` (L10862-10863); em erro mostra `MsgBox(LoadResString 108, 48, LoadResString 9, ...)` (L10870-10879) e restaura a unidade anterior (`Drive.Drive = var_20`, L10881). Zera `Tag` (L10864).
- **`TxtDiretorio_Change`** (L10757): ver acima.
- **`CmdSair_Click`** (L10714-10724): `End` (L10718) - **encerra a aplicacao inteira**, nao so o form.
- **`CmdMake_Click`** (L10790-10851), passo a passo:
  1. Se `Trim(var_34) = ""`, `var_34 = Diretorio.Path` (L10794-10802); depois `var_34 = Trim(TxtDiretorio.Text) & IIf(termina com "\", "", "\")` (L10809) -> diretorio com `\` final.
  2. `var_28 = var_34 & "DIMEP.Mdb"` (L10810).
  3. **Teste de gravacao**: `Open var_34 & "TestFile.OCS" For Output As #1` / `Close #1` / `Kill var_34 & "TestFile.OCS"` (L10812-10816) com `On Error GoTo` proprio (L10811). Se o erro for **76** (caminho nao encontrado) -> `MsgBox(LoadResString 87 = "Diretorio nao encontrado.", 48, LoadResString 3 = "Atencao")` (L10831-10847). Outros erros -> logger "FrmMakeFile.CmdMake_Click" (L10849).
  4. Grava `MdiPrincipal.Tag = var_34` (L10805) (e depois o chamador le `FrmMakeFile.Tag`; L18462-18466 grava `DIMEP.Cfg`).
  5. Esconde `CmdSair` e `CmdMake` (`Visible = ...`, L10806-10807 -> L10808) e desabilita `TxtDiretorio`, `Drive`, `Diretorio` (L10809-10811 equivalentes).
  6. `Dir(var_28)`: se o `.Mdb` **nao existe** (decompilador mostra `(x = "") + 1`, mas o texto da mensagem confirma a semantica): monta e mostra
     `MsgBox(msg, 36, titulo)` com **"O banco de dados" + [sep] + "DIMEP.mdb" + [sep] + "nao foi encontrado no diretorio " + <dir> + vbCrLf + "Deseja cria-lo?"** (IDs 9924, 9925, 9926; L10812-10817; flags 36 = Sim/Nao + ponto de interrogacao; titulo `LoadResString 3` = "Atencao").
     - Resposta **diferente de Sim(6)** (L10818-10829): reabilita `CmdSair`, `CmdMake`, `TxtDiretorio`, `Drive`, `Diretorio` (volta para a tela).
     - Resposta **Sim**: `Proc_61_0(var_28)` = `CriaAtualizaBD(<dir>\DIMEP.Mdb)` (L10827) que, como o arquivo nao existe, usa `CreateDatabase` e cria todas as tabelas/indices; depois `Unload Me` (L10832).
  7. Se o `.Mdb` ja existe nesse diretorio, o codigo mostrado nao tem ramo explicito (nao ha `Else` visivel): o form simplesmente permanece/e fechado pelo chamador. [BAIXA]

### 7.3 `Carrega_Msg` (`Proc_20_7_5091F0`, L10882-10899)
`Caption` do form = `LoadResString 1018` = "Diretorio p/ Execucao" (L10889-10890); `CmdMake.Caption` = `LoadResString 2620` = "Gerar" (L10893-10899); `CmdSair.Caption` = `LoadResString 402` = "Sair" (L10896). Erro -> `FrmMakeFile.Carrega_Msg` (L10900).

### 7.4 Mensagens (textos exatos de `S\res_strings.txt`)
| ID | Texto | Quando | Botoes / icone |
|---|---|---|---|
| 9924 | 'O banco de dados' | `.Mdb` nao existe no diretorio escolhido, ao clicar "Gerar" | Sim/Nao, ponto de interrogacao (flags 36), L10812 |
| 9925 | 'não foi encontrado no diretório ' | idem (concatenado: 9924 + " DIMEP.mdb " + 9925 + dir) | idem, L10813 |
| 9926 | 'Deseja criá-lo?' | idem (apos `vbCrLf`) | idem, L10814 |
| 3 | 'Atenção' | titulo das mensagens | - |
| 87 | 'Diretório não encontrado.' | erro 76 ao testar gravacao no diretorio | OK, exclamacao (48), L10831-10847 |
| 108 | 'Drive não disponível no momento' | erro ao trocar de unidade | OK, exclamacao (48); titulo `LoadResString 9` = 'Aviso!', L10870-10879 |
| 1018 | 'Diretório p/ Execução' | caption | - |
| 2620 | 'Gerar' | caption de CmdMake | - |
| 402 | 'Sair' | caption de CmdSair | - |

### 7.5 Fluxo no chamador (contexto, `MDIPrincipal.MDIForm_Load`, L18343-18490; outro agente) [ALTA para os literais]
- `DIMEP.Cfg` (L18449): le o arquivo `<App.Path>\DIMEP.Cfg` (conteudo = diretorio do banco com `\` final; no PC de referencia: `\\192.168.200.200\SHOficina\MiniTime\`).
- Se `<dir>DIMEP.Mdb` nao existe (L18454-18455), mostra `FrmMakeFile` modal (L18461) e depois **grava** `DIMEP.Cfg` com `FrmMakeFile.Tag` (L18462-18466).
- Se existe: `Proc_61_0(dir & "DIMEP" & ".Mdb")` = `CriaAtualizaBD` (L18470) - executado **a cada partida**, por isso o esquema se auto-atualiza.
- `global_006F0090 = FrmMakeFile.Tag` (L18480) (diretorio de dados) e `Proc_61_2(..., global_006F0090 & "DIMEP" & ".Mdb")` (L18482).
- Abre o banco com `";pwd=<senha-do-mdb>"` (L18488), le `Parametros` e preenche defaults (secao 3.8).
- Limite de funcionarios: se `> 50` (&H32) -> mensagem 9954-9957 e bloqueio (L18598-18610, outro agente).

---

## 8. Hipoteses / lacunas
- Tamanhos dos campos Text e o conteudo do flag de PK nao aparecem no decompilado (operandos imediatos perdidos); adotei os valores do MDB real. Os **tipos** (`CInt`) e **nomes** sao literais e batem 100% com o MDB (nao ha divergencia coluna a coluna).
- Nao foi possivel confirmar: onde `global_006F0248(i)` (flag de "houve backup") e setado; polaridade de algumas condicoes (`(x = y) + 1` em `FrmMakeFile` e `CriaTabelasTmp`); aninhamento exato do `UPDATE Parametros SET Versao` (L66585).
- O comportamento da funcao `Proc_61_2` e incerto (BAIXA).
- O bloco inteiro e quase todo declaracao de dados; **nao ha** formatos de arquivo de exportacao aqui.

---

## Apendice A - Onde esta a EXPORTACAO de verdade (pistas, fora do meu bloco)

Para o orquestrador: o pedido original (formatos de exportacao) deveria ser dado a quem cobre estes objetos:

| Objeto | Linhas | Papel (por evidencia) |
|---|---|---|
| `Geral.GeraExportacao` (`Proc_45_0_62D8C0`) | L50387-50527 | Escreve **uma linha de texto por marcacao** no arquivo de exportacao (`Print 1, ...`, L50466/L50497). Le `Parametros.TipoExp/NumDigAno/DigVerificador/FiltroExp/NumCartao` (L50397-50426). |
| `Geral.Zero_E` (`Proc_44_53_62D2B0`) | L50312-50330 | Preenche com zeros a esquerda ate N: `String(N - Len(s), "0") + Trim(s)` (L50319-50322); se N < Len, usa `Mid(s, ecx - diff, N)` (L50326; **truncagem** - operando de inicio truncado, provavel "mantem os N caracteres da direita") [MEDIA]. |
| `frmExportacao` | L73619-74585 | Tela "Exportar": `txtArq`, `txtDataDe/Ate`, `optFiltro`, `optTipo`, `optNumDigAno`, `optDigVerificador`, `cmdArq_Click`, `cmdExportar_Click` (L74175). Faz varredura de `Marcacao` por `Data_Hora` com o indice `"Marcacao02"` e chama `GeraExportacao` (L73973, L74027, L74105). |
| `frmParamExp` | L7880-8170 (aprox.) | Tela de parametros de exportacao (`optNumDigAno`, `chkExpMonitoracao`, `cmdGravar_Click` L8138): grava TipoExp/NumDigAno/FiltroExp/DisqVirtual/ExpMonitoracao em Parametros (L7530-7604). |
| `frmExportArq` | L26210-26591 | **Outro** gerador de arquivo texto com registros de tipos 1, 2, 3, 4 (`Print 1, 2 & Format(var_28, ...)`, `Format(CStr(3), "0")`, `4 & Format(0, String(20,...))`; L26274-26405), provavelmente um layout de folha de pagamento por apuracao. |
| `frmColetaRelogio` / `frmComunicTrata` | L21756 / L61524 | Chamam `GeraExportacao` com `Moviment.txt` (exportacao automatica apos coleta). |
| `frmImportacao` | L12968-13711 | `Open frmImportacao.txtDirArquivo.Text For Output` (L13656) / `Print` (L13677). |

### A.1 Analise PRELIMINAR de `Geral.GeraExportacao` (so o que o decompilado deixa ver; NAO e uma especificacao final)

**Parametros lidos de `Parametros`** (cada um por `Recordset.Fields("...")` / `LoadProp`, nome literal):
- `TipoExp` (L50397-50399) -> `CInt(...)`; na variavel auxiliar (nao reaparece como condicao visivel).
- `NumDigAno` (L50402-50404) -> `var_34`.
- `DigVerificador` (L50407-50410) -> `var_24/var_800C`.
- `FiltroExp` (L50413-50422) -> `var_38`, com tratamento de `Null` (`IIf(IsNull(x), "", x)`, L50422).
- `NumCartao` (L50425-50426) -> `var_2C` (o nome aparece truncado/deslocado como `Dir("NumCartao", 0)` em L50427).

**Controle de fluxo** (L50427-50505):
- Abre o arquivo `Open <arq> For Output As #1` (L50430). O chamador `frmExportacao` antes faz `Open <arq> For Append` + `Close` (L73940-73941) so para garantir existencia. **Ambiguidade:** com `For Output` o arquivo seria truncado a cada chamada; provavelmente ha um `If Dir(arq) = ""` (L50427) com `Output`/`Append` e o decompilador fundiu os ramos [BAIXA].
- Escreve apenas se `arg_10 = 0` (L50431) e a condicao `((arg_24 <> True) Or (Tipo = 7)) Or (Tipo = 263)` for **falsa** (L50432): **marcacoes com `Tipo` 7 ou 263 NAO sao exportadas** (nem as de `arg_24 <> True`); isso e coerente com os dados reais (`Tipo` 0, 7, 256, 263; S\q_out.txt). `Tipo And 255` vale 0 (tipos 0 e 256) ou 7 (tipos 7 e 263), L50465 [MEDIA].
- Ha dependencia de `FiltroExp` (valores 0/1, L50434-50443): `If var_38 = 1` e `If arg_24 >= 1 And var_38 = 0` selecionam quais situacoes entram [BAIXA: os `If` foram degradados].
- Se `arg_14 = ""` (cartao vazio) vira `"0"` (L50446-50449).

**Linha escrita (`Print`)** (L50466 e variante L50497; aparecem duas copias quase iguais, uma por ramo, provavelmente `DigVerificador = 0` / `<> 0`):
```
Print #1,  <A> & <DATA> & <HORA hhmm> & <Tipo And 255, 2 digitos> & "00" & <CARTAO>
```
- `<DATA>` = `Format(data, "ddmmyyyy")` quando `NumDigAno = 4` (L50455-50458: `If var_34 = 4 Then var_CC = "ddmmyyyy"`); senao `Format(data, <var_CC>)` com mascara de **2 digitos de ano** cujo literal foi perdido (L50461; por `NumDigAno = 2` default, L18591; **provavel "ddmmyy"**, MEDIA).
- `<HORA>` = `Format(data, "hhmm")` (L50466, L50491).
- `<Tipo And 255>` = `Format((Tipo And 255), "00")` (L50465/L50495) -> "00" ou "07".
- Constante `"00"` fixa a seguir (L50466, L50497).
- `<CARTAO>` (`var_54`) = `Format(arg_14, <mascara de zeros var_CC>)` (L50450, L50472), onde a mascara tem `NumCartao` zeros (`var_F8 = CInt(Val(var_2C))` = NumCartao, L50453) [MEDIA].
- `<A>` (`var_4C`) = `Geral.Zero_E(<?>)` (L50454, L50478), chamada com argumento perdido; `var_60 = Proc_45_10_631280(8, ..., arg_18)` (L50452) e o valor do **terminal** (`arg_18`; no chamador e `Fields("Terminal")`, L73936). Hipotese: `<A>` = numero do terminal/relogio preenchido com zeros [BAIXA].
- **Exemplo hipotetico** (apenas ilustrativo, derivado do formato acima; NumDigAno=4, NumCartao=5, Tipo=0): `<A>` + `07102026` + `0905` + `00` + `00` + `07984` -> `<A>071020260905000007984`. (Tamanho de `<A>` desconhecido.)

**Rotulo de progresso**: apos escrever, atualiza `frmExportacao.lblExport` e `LinhasExport` (L50501, L50506) [MEDIA].
**Erro**: `Err.Number = 75` ("Path/File access error") e ignorado (L50514-50519: zera o erro); qualquer outro -> `Geral.GeraExportacao` (L50521).

### A.2 `frmExportacao.cmdExportar_Click` (L74... -> `Proc` em L73640-74200; visao rapida)
- Valida datas `txtDataDe/txtDataAte` (msg 60 e 46), valida diretorio do arquivo (msg 87/88), se o arquivo ja existe pergunta (msgs 10088/10089, `Kill`), grava `FiltroExp` e `NomeArq` em `Parametros` (L73920-73940: `FiltroExp` por `optFiltro(0)`; `NomeArq` por `txtArq.Text`).
- Consulta `Marcacao` com `"Marcacao02"` (L73934) e `Data_Hora >= <de>` e `<= <ate>+1` (L73937-73960).
- Tres variantes do `GeraExportacao` (L73973, L74027, L74105) conforme `optFiltro`/`HorarioNoturno` (L73980, L74015; L74094): o filtro seleciona **todas as marcacoes** ou **so de funcionarios com `HorarioNoturno`**, lendo `Funcionario01(Codigo)` (L74067) [BAIXA].
- Mensagens finais: 9959 (nenhum registro, icone 16) e 1850 (concluido, icone 64) (L74130-74145).

### A.3 Recomendacao
Atribuir a um agente dedicado: `Geral.GeraExportacao` + `Geral.Zero_E` + `frmExportacao` + `frmParamExp` + `frmExportArq`, idealmente com apoio do disassembly (`S\xdis.py`, `S\checa.asm`), porque as mascaras de `Format` (var_CC) e os argumentos de `Zero_E` foram perdidos pelo decompilador.
