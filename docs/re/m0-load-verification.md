# M0 — confirmação de carregamento em jogo

- **Data:** 2026-10-28
- **Jogo:** ASKA `Application.version = 0.4`; `Player.log` mostra
  `1.48.0910261842._PC.Release`
- **Engine:** Unity 6000.3.12f1; IL2CPP; metadata v39
- **BepInEx:** 6.0.0-be.791 (`b782da4ac54e1072d2857f4a2f77c80c3ef08005`)
- **Plugin:** AI Companions - ASKA 0.1.0
- **Método:** lançamento direto do `Aska.exe` com `-WorkingDirectory` na pasta do jogo,
  janela 1280x720, sem entrar em mundo; processo encerrado após o menu.

## Resultado

`BepInEx\LogOutput.log`:

```text
[Info   :   BepInEx] 1 plugin to load
[Info   :   BepInEx] Loading [AI Companions - ASKA 0.1.0]
[Info   :AI Companions - ASKA] [AIC] mod AI Companions - ASKA 0.1.0
[Info   :AI Companions - ASKA] [AIC] Unity 6000.3.12f1 | produto Aska 0.4
[Info   :AI Companions - ASKA] [AIC] BepInEx 6.0.0-be.791+b782da4... | root ...\ASKA\BepInEx
[Info   :AI Companions - ASKA] [AIC] jogo C:\...\ASKA
[Info   :AI Companions - ASKA] [AIC] plugin ...\BepInEx\plugins\AI-Companions-Aska
[Info   :AI Companions - ASKA] [AIC] interop ok: ...\BepInEx\interop
[Info   :AI Companions - ASKA] [AIC] SO Windows 11 (10.0.26300) | CPU AMD Ryzen 7 5700X | GPU NVIDIA RTX 3070 Ti
[Info   :AI Companions - ASKA] [AIC] Enabled=True (reserved; no gameplay in 0.1.0).
[Info   :AI Companions - ASKA] [AIC] AI Companions - ASKA loaded successfully.
[Message:   BepInEx] Chainloader startup complete
```

Config criada em `BepInEx\config\julianu37.AICompanionsAska.cfg` com as seções
`[General]`, `[AI]`, `[Voice]` e `[UI]` e os defaults documentados.

## Observações

- **Lançar pelo Steam.** O teste direto do `Aska.exe` carrega o plugin, mas o jogo sai com
  código 53 depois do menu (`/Client/LoginWithSteam: Invalid input parameters`) por falta do
  ticket do Steam. Para jogar/testar gameplay, use o botão **Jogar** do Steam.
- **Working directory.** O primeiro teste, iniciado com o working directory no repositório,
  fez o BepInEx enxergar **0 plugins**. Iniciar com o working directory na pasta do jogo (ou
  pelo Steam) corrigiu. Se o plugin não aparecer, confirme o working directory e apague
  `BepInEx\cache\chainloader_typeloader.dat`, depois relance.
- **Cache.** O cache de descoberta pode ficar velho depois de mover/adicionar DLLs; apagar o
  arquivo acima força um novo scan.
- **Nada de gameplay foi testado** (sem chat, LLM, voz ou ordens). Este marco prova apenas o
  carregamento, o relatório de ambiente e a criação da config.

## Conclusão

M0 aceito: o plugin compila, é deployado, carrega no ASKA e cria a configuração. As fases
seguintes podem começar.
