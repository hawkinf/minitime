# TEST_STRATEGY — minitime

- xUnit em `tests/MiniTime.Tests`: Core, Data (SQLite em memória), parser serial com quadros sintéticos.
- Paridade da apuração: comparar com `Marcacao.Situacao/Tipo` gravados no MDB real (teste opcional, `Category=Live`).
