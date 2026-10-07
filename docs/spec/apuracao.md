# Apuração do ponto — regras implementadas

Reconstruídas por análise do banco real (82 mil marcações, 2010–2026) e do programa original. O código original
não existe (só uma saída degradada de decompilador), então **estas são as regras do motor novo**
(`src/MiniTime.Core/Apuracao/Apurador.cs`), validadas contra os dados legados — não uma cópia do algoritmo VB.

## Códigos persistidos em `Marcacao` (herdados do legado)

| Campo | Valor | Significado | Evidência |
|---|---|---|---|
| `Tipo` | 7 | batida coletada do relógio | 61,9 mil linhas; inserida pela coleta |
| | 0 | batida inserida pelo usuário; com `Justificativa = -1` é inserção automática do legado (ignorada) | 13 mil linhas |
| | 256 | linha gerada pelo legado: status do dia (`EntradaSaida = 8`) ou abono de uma coluna | 7,3 mil |
| | 263 | batida coletada que o usuário desprezou (7 + 256) | 57 |
| `EntradaSaida` | 1 / 2 / 3 / 4 | entrada, saída p/ intervalo, retorno, saída | |
| | 5 / 6 | entrada / saída extra | raros |
| | -1 | desconsiderada | 14 mil |
| | 8 | registro de status do dia (falta/abono), horário fictício 23:00/01:30/00:00 | 5,4 mil |
| `Situacao` | 1 normal · 3 justificada · 4 divergente · 0 não tratada | | |
| `Divergencia` | 1 atraso · 2 saída antecipada | (3–9 em linhas de status: falta, meia falta…) | |

## Regras do motor

1. **Batidas válidas:** Tipo 7 e Tipo 0 manual (justificativa ≠ -1). Ignora Tipo 256, 263 e inserções automáticas.
2. **Janela do dia:** dia civil; turno noturno usa `HrMudancaData` (ex.: 12:00 → 12:00 do dia seguinte); dia livre usa
   `HrMudancaDataDiaLivre` e o sentido configurado.
3. **Horário do dia:** `Jornada.<dia da semana>` → `Horarios`. Sem horário/`NaoTrabalha` = dia livre (DSR se a jornada trata DSR
   e é o dia do DSR). Feriado (dia/mês) e férias zeram a obrigação.
4. **Colapso nas pontas:** batidas até a entrada prevista ficam como uma só (a primeira); a partir da saída prevista, só a última.
5. **Atribuição de posições:** com até 4 batidas, atribui a subconjunto ordenado de {entrada, saída int., retorno, saída} que minimiza
   a distância aos horários previstos (cobre dias com batidas faltando). Acima de 4, mantém primeira, última e o par do intervalo.
6. **Tolerâncias:** entrada/saída a até `TolManha`/`TolSaida` minutos do previsto valem como o horário previsto; além disso, atraso
   conta cheio (entrada tardia) ou saída antecipada conta cheio.
7. **Intervalo:** retorno antes de `RefMinimo` minutos vale o mínimo; acima do previsto + `TolTarde` conta o excesso como atraso;
   sem batidas de intervalo, desconta o previsto.
8. **Hora extra:** só se `AutHoraExtra` e o excesso supera a tolerância de extra; senão é desprezada.
9. **Status:** Falta (dia útil sem batidas, exceto jornada `NaoMarcarFalta` → "sem marcação"), 1/2 falta (duas batidas cobrindo só um
   período), pendência (incompleto), abonado (linha de status com justificativa), futuro (> hoje).
10. **Noturno:** adicional = minutos entre 22:00–05:00 × (60/52,5 − 1).

## Paridade com o legado

Teste `ParidadeTests` (categoria Live, só roda com `MINITIME_PARITY_DB`): nas batidas coletadas desde 06/2022,
**posição igual em ~80%** e **divergência (atraso/saída antecipada) igual em ~98,6%**. A diferença se concentra em batidas que o legado
trocava por uma linha automática no horário previsto; regras do legado variaram entre versões (anos anteriores a 2022 têm
comportamento diferente, ex.: mínimo do intervalo não aplicado).

## Lacunas conhecidas

- "Almoço móvel" e "saldo antecipado" (`Total_SdAntec` do espelho legado) não têm regra própria ainda.
- DSR é tratado de forma simples (marca o dia); não calcula perda de DSR por falta na semana.
- Fórmulas exatas dos totais do espelho legado (`Total_Calc`, etc.) não foram recuperadas.
