# RELEASE_FLOW — minitime

Alvo: Windows (win-x64, autocontido).

1. Atualizar `<Version>` em `Directory.Build.props`.
2. `.\scripts\publicar.ps1` — roda os testes e gera `dist\MiniTime-<versão>-win-x64.zip` com `MdbReader\` junto.
3. Abrir `dist\win-x64\MiniTime.exe`, conferir: cadastros, espelho, importação do MDB.
4. Commit `CHORE: release vX.Y.Z` e push (o repositório é público: nunca versionar `source/`, `docs/ref/`, `*.mdb`, senhas).
