# Protocolo serial do relógio Mini Point (reconstruído)

Fonte: disassembly nativo de `Geral.Recepcao`, `frmComunicacao.Checa_Msg`, `frmColetaRelogio` do MiniTime.exe e decompilado.
**Nada disto foi testado com o relógio real** — por isso a coleta está marcada como experimental e a tela registra o tráfego em hex.

Porta: 19200 bps (parâmetro `Velocidade`), 8 bits, sem paridade, 1 stop (`"<bps>,n,8,1"`).

## Quadros (confiança ALTA para a estrutura, lida do código nativo)

```
binário:  [AA FF FF]  FE | end | cmd | tam | dados[tam] | chk | F0      (preâmbulo AA FF FF só no envio do PC)
ASCII:    FD | end(2 dígitos '0'-'9') | cmd | tam | dados | chk | F0     (cada byte = 2 caracteres 0x30..0x3F: nibble alto, baixo)
```

- `end` = endereço do relógio em BCD (12 → 0x12).
- `chk` = (XOR de end, cmd, tam e dados) & 0x7F.
- `tam` = quantidade de bytes de dados (pode ser 0).

Implementação: `src/MiniTime.Serial/Quadro.cs` (serialização + `ReceptorQuadros`, máquina de estados equivalente à do original).

## Coleta de marcações (confiança MÉDIA/BAIXA)

Do decompilado de `frmColetaRelogio`: o programa alterna dois comandos, marcados internamente como `"J"` e `"K"`
(códigos 0x0E e 0x0F); a resposta de código 0x1F traz um contador de 4 dígitos (marcações pendentes).
Cada marcação chega como texto de 28 caracteres:

| Posição | Campo |
|---|---|
| 1–16 | cartão (16 dígitos) |
| 17–18 | dia |
| 19–20 | mês |
| 21–22 | ano (2 dígitos; < 80 → 20xx) |
| 23–24 / 25–26 | hora / minuto |
| 27–28 | tipo |

O programa grava em `Marcacao` com `Tipo = 7`, `Terminal = endereço`, e em `Backup`. Implementado em `ClienteRelogio.Coletar`.
**Hipótese:** J pergunta/inicia, K confirma o registro e pede o próximo. A sequência real precisa ser conferida com o log hex.

## Outros comandos (não implementados) e como descobri-los

Acerto de data/hora (formato `ddmmyy`), envio de cartões (50 no Mini Point), faixa horária, alarmes, horário de verão, status
(capacidade, quantidade, versão do firmware): o quadro (framing) já está resolvido; **faltam os códigos de comando e o layout dos dados**.
Eles só saem de uma destas fontes, em ordem de confiabilidade:

1. **Captura do programa original funcionando.** O VB6 antigo costuma rodar em Windows XP/7 32 bits (máquina velha ou VM VirtualBox/VMware
   com o adaptador USB-serial repassado à VM). Com ele conversando com o relógio, capture o tráfego:
   - *por software*: monitor de porta serial (log hex) no mesmo Windows; ou
   - *por hardware (recomendado)*: "tap" passivo com **dois adaptadores USB-serial só com o RX ligado** — um na linha TX do PC, outro na
     linha TX do relógio (GND comum; nunca ligue TX do adaptador de captura). Depois:
     `minitime-protocolo monitorar COMa COMb --saida captura.txt`
2. **Fontes/decompilado e manual do fabricante** (ficam só na máquina do usuário; não vão para o repositório).
3. **Teste ativo** — só como último recurso e depois de coletar e fazer backup das marcações, porque um comando desconhecido pode apagar dados do relógio.

Para cada operação do programa antigo (acertar hora, enviar cartões, ler status...), faça **uma ação por vez** e anote o que fez e o horário;
depois `minitime-protocolo decodificar captura.txt` mostra os quadros e `--resumo` agrupa por comando.

### Tabela de comandos (preencher com as capturas)

| Código | Direção | Dados | Significado | Origem/confiança |
|---|---|---|---|---|
| 0x0E | PC→relógio | vazio | pergunta marcações pendentes ("J") | decompilado, média |
| 0x0F | PC→relógio | vazio | confirma o registro e pede o próximo ("K") | decompilado, média |
| 0x1F | relógio→PC | 4 dígitos ASCII ou registro de 28 caracteres | contagem / registro de marcação | decompilado, média |
| ? | PC→relógio | `ddmmyy`+hora? | acerto de data/hora | **a capturar** |
| ? | PC→relógio | cartões | envio de cartões | **a capturar** |
| ? | PC→relógio | — | status/versão do firmware | **a capturar** |
