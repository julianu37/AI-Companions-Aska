# Testes e triagem

O critério final é **confirmado em jogo pelo jogador**. Este documento define o que testar,
como ler o log e o que bloqueia um release.

---

## 1. Pré-voo (antes de abrir o jogo)

- [ ] `Environment.props` existe e aponta para o ASKA correto.
- [ ] `dotnet build AICompanionsAska.sln -c Release` passou.
- [ ] O DLL está em `BepInEx\plugins\AI-Companions-Aska\` e é a versão esperada.
- [ ] Não há outra cópia do mesmo GUID em `plugins\` (o BepInEx aborta o lote de plugins
      com erro ao detectar um GUID duplicado; não é “carregar duas vezes”).
- [ ] O jogo não está aberto no momento do deploy.
- [ ] Se a fase usa LLM: LM Studio está com o modelo carregado e o Local Server iniciado.
- [ ] Se a fase usa voz: microfone conectado e modelo presente na pasta do plugin.

## 2. Teste de carregamento (todas as fases)

1. Abra o jogo **pelo Steam** e espere chegar ao menu. (Iniciar o `Aska.exe` direto carrega o
   plugin, mas o login do Steam falha e o jogo pode sair com código 53; use o Steam para jogar.)
2. Abra `BepInEx\LogOutput.log`.
3. Confirme:
   - a linha do BepInEx com a versão `6.0.0-be.791`;
   - a linha `[AIC] AI Companions - ASKA <versão>`;
   - `[AIC] ... loaded successfully`;
   - nenhuma exceção/`NullReferenceException` do mod.
4. Entre em um mundo (single-player) e confirme que o jogo não trava.

**Falha de carregamento:** procure `[Error]`, `Exception`, `MissingMethodException`,
`FileLoadException`, `coreclr.dll+0x1d1fdd` (crash nativo) e a última linha antes do crash.
Anexe o log ao issue/handoff.

**Se aparecer `0 plugins to load`:** confirme que o jogo foi iniciado com o working directory
na pasta do ASKA (ou pelo Steam), apague `BepInEx\cache\chainloader_typeloader.dat` e relance.
O cache de descoberta pode ficar velho depois de mover/adicionar DLLs.

## 3. Matriz por fase

### Fase chat + LLM (M1)

| Caso | Passos | Esperado |
|---|---|---|
| Ordem simples | digitar `/c segue` | companheiro segue; log `[AIC][ORDER] follow` |
| Parar | `/c para` | companheiro para |
| Conversa | `/c bom dia` | resposta curta no chat/balão |
| Prefixo ausente | `bom dia` | mod ignora (modo normal) |
| LLM offline | desligar o LM Studio e mandar ordem | aviso claro, sem travar |
| JSON inválido | forçar resposta fora do schema | parser cai no fallback, sem crash |
| Latência | medir 5 ordens | log com ms/tokens; sem stutter |

### Fase voz (M4/M5)

| Caso | Passos | Esperado |
|---|---|---|
| Push-to-talk | segurar V, falar “segue”, soltar | transcrição correta e ordem |
| Silêncio | segurar V sem falar | nada é enviado |
| Ruído | falar em ambiente ruidoso | transcrição plausível ou aviso |
| STT sem modelo | renomear o ggml e tentar | aviso, chat continua |
| TTS | pedir uma fala | áudio no companheiro, uma por vez |
| TTS sem modelo | renomear o Kokoro e tentar | só texto, sem crash |
| Eco | falar perto de paredes | sem cauda excessiva (ajustar reverb) |

### Fase UI (M6)

| Caso | Passos | Esperado |
|---|---|---|
| Abrir/fechar | tecla do painel | abre/fecha, sem travar |
| Salvar | mudar um toggle | `.cfg` atualizado na hora |
| Input | digitar no campo | teclas do jogo não disparam |
| Sem companheiro | abrir o painel | painel abre (config global) |

### Fase co-op (M7)

| Caso | Passos | Esperado |
|---|---|---|
| Host recruta | host recruta, cliente observa | companheiro aparece igual |
| Ordem do cliente | cliente pede `segue` | host executa (ou recusa clara) |
| Saída do host | host sai | cliente não crasha; estado consistente |
| Save/reload | salvar, sair, voltar | companheiro e perfil persistem |

## 4. Leitura do log

Padrões úteis:

```text
[AIC] ...                      linha do mod
[AIC][CHAT] ...                captura/normalização
[AIC][LLM] ...                 request/response, tokens, ms
[AIC][ORDER] ...               intenção -> ordem
[AIC][STT] ...                 transcrição, latência
[AIC][TTS] ...                 síntese, RTF, playback
[Error :Il2CppInterop] ...     problema de interop (patch/trampoline)
MissingMethodException ...     membro removido/renomeado no update
coreclr.dll+0x1d1fdd ...       crash nativo (WER)
```

- **`MissingMethodException`** pode falhar no JIT do método inteiro; remova a referência ou
  isole o método.
- **Crash nativo sem exceção gerenciada**: mapear o offset do WER com Cpp2IL/`dumpbin` e
  registrar em `docs/re/dead-ends.md`.
- **Patch que não dispara**: provável AOT inlining; trocar para método virtual/público e
  fire-verificar.

## 5. Performance

- Baseline: FPS com o mod desabilitado vs habilitado.
- O mod em idle deve custar **< 1 ms/frame**.
- Picos permitidos: carregar modelo, transcrever, sintetizar, chamar o LLM.
- Se houver stutter, instrumentar `Stopwatch` e logar só o que passar de 2 ms (`[AIC][PERF]`).
- Nunca varrer a cena inteira por frame; throttle e cache.

## 6. Crash / bug report (modelo)

```md
## Versão
- Mod: 0.1.0
- ASKA build: 25834809 (Unity 6000.3.12f1)
- BepInEx: 6.0.0-be.791

## O que aconteceu
...

## Passos
1. ...
2. ...

## Log
(anexar trecho de LogOutput.log / ErrorLog.log / .dmp)

## Suspeita
...
```

## 7. Release gate

Um release só sai quando:

- [ ] build limpo;
- [ ] carregamento confirmado no log;
- [ ] todos os casos da fase marcados como **confirmados em jogo**;
- [ ] nenhuma feature “não testada” escondida atrás de um default `false` (se está no pacote,
      o jogador pode ligar; então precisa estar testada ou não pode existir);
- [ ] licenças e créditos atualizados;
- [ ] `CHANGELOG.md`/release notes escritos;
- [ ] tag na `main` e pacote publicado.
