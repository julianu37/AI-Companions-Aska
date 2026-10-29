# Triagem de `LogOutput.log`

## Onde

`C:\Program Files (x86)\Steam\steamapps\common\ASKA\BepInEx\LogOutput.log`

- Sempre gravado; o console é opcional (`BepInEx.cfg` -> `[Logging.Console] Enabled = true`).
- `ErrorLog.log` para erros do BepInEx/preloader.
- `%LOCALAPPDATA%\CrashDumps\Aska.exe.*.dmp` para crash nativo.

## Linhas do mod

```text
[AIC] AI Companions - ASKA 0.1.0 | Unity 6000.3.12f1
[AIC] loaded successfully
[AIC][CHAT] ...
[AIC][LLM] ...
[AIC][ORDER] ...
[AIC][STT] ...
[AIC][TTS] ...
[AIC][PERF] ...
```

## Padrões de erro

| Padrão | Significado | Ação |
|---|---|---|
| `FileLoadException` / `0x800711C7` | SAC/antivírus bloqueou DLL | bump versão, excluir pasta |
| `MissingMethodException` | membro removido/renomeado | regenerar interop; isolar referência |
| `Handle is not initialized` | lifecycle cedo | patchar `Spawned`/`Update` |
| `coreclr.dll+0x1d1fdd` | crash nativo | WER + Cpp2IL; remover caminho |
| `Fatal error. Internal CLR error. (0x80131506)` | `PatchAll`/atributo | patch por alvo com try/catch |
| `VerificationException` | log de struct | `.ToString()` |
| `During invoking native->managed trampoline` | marshal de patch | by-ref/inventário |
| patch sem `[AIC][PATCH]` | AOT inlining | trocar método |

## Comandos

```powershell
# últimas 80 linhas
Get-Content "C:\...\BepInEx\LogOutput.log" -Tail 80

# erros do mod
Select-String -Path "C:\...\BepInEx\LogOutput.log" -Pattern "\[AIC\]|Exception|Error" |
  Select-Object -Last 60

# versão carregada
Select-String -Path "C:\...\BepInEx\LogOutput.log" -Pattern "AI Companions - ASKA"
```

## Boas práticas

- Sempre peça o log inteiro (ou o trecho em volta do erro), não só a mensagem.
- Confirme a versão carregada antes de culpar o código.
- Se o jogo crashou, procure o último `[AIC]` e o `.dmp`.
- Não cole logs gigantes no chat; recorte.
- Nunca inclua a API key no log/issue.
