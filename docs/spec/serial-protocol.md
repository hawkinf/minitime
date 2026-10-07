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

## Outros comandos (não implementados)

Acerto de data/hora (formato `ddmmyy`), envio de cartões (50 no Mini Point), faixa horária, alarmes, horário de verão, status
(capacidade, quantidade, versão do firmware): exigem capturar o tráfego do programa original com o relógio ligado
(ex.: com um monitor de porta serial) ou testar comandos com a tela de coleta em modo de diagnóstico.
