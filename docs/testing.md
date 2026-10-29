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
| Ordem simples | digitar `/c segue` | `[AIC][LLM] intent action=follow/come` (execução na fase `recruit-follow`) |
| Parar | `/c para` | `intent action=stop` no log (execução na fase `recruit-follow`) |
| Conversa | `/c bom dia` | resposta curta no chat (`Thorin: ...`) |
| Prefixo ausente | `bom dia` | mod ignora (modo normal) |
| LLM offline | desligar o LM Studio e mandar ordem | aviso claro, sem travar |
| JSON inválido | forçar resposta fora do schema | parser cai no fallback, sem crash |
| Latência | medir 5 ordens | log com ms/tokens; sem stutter |

### LLM (M1)

| Caso | Passos | Esperado |
|---|---|---|
| Self-test | `AI.SelfTest=true`, abrir o jogo | `[AIC][CHAT] addressed to Thorin from selftest-name ... "bom dia"` + `[AIC][LLM] ... action=none ... reply="Bom dia!..."` + `[AIC][REPLY] (sem ChatMenu) Thorin: ...` |
| Chat + LLM | `/c segue` em um mundo | `addressed to Thorin ...` + intent + linha `Thorin: ...` |
| Nome | `Thorin, bom dia` em um mundo | `addressed to Thorin ...` + `intent` + linha `Thorin: ...` |
| Nome inválido | `Thorinzinho, oi` | ignorado (nome completo + separador) |
| Sem nome/prefixo | `bom dia` | ignorado |
| LM Studio offline | fechar o LM Studio e mandar ordem | `[AIC][LLM] falhou: rede...`; jogo continua |
| `AI.Enabled=false` | desligar no `.cfg` | chat captura/loga, mas nenhuma chamada LLM |
| `ReplyInChat=false` | desligar no `.cfg` | intent logado, sem linha no chat |
| `StructuredOutput=false` | desligar no `.cfg` | `mode=None` no log; parser ainda funciona se o modelo devolver JSON |
| Harness | `dotnet run --project tests/LlmIntentHarness -c Release` | `41/41 checks passed` |

> `AI.SelfTest` é diagnóstico: deixe `false` fora de um teste. O self-test injeta a mensagem no
> `ChatCapture` (não no chat do jogo), então ele prova a cadeia sem precisar entrar em um mundo.

### Fase party + ordens (M1.6, branch `feat/recruit-follow`)

Pré-requisito: o log precisa identificar o build — confirme `[AIC] AI Companions - ASKA 0.5.0`.
Preserve o `LogOutput.log` de **cada** run (o BepInEx sobrescreve a cada sessão).

| Caso | Preparação | Esperado |
|---|---|---|
| **A0 — controle sem o mod** | DLL fora de `plugins\AI-Companions-Aska\` (`Enabled=false` **não** basta) | 3 loads do save sem crash; `Player.log` passa de `[Weather System] Weather Forcast Reset` e chega ao mundo. **Se crashar aqui, o problema é do jogo/save, não da branch.** |
| **A1 — mod, unlock off** | `DebugUnlockParty=false` | 3 loads, sem crash; `debugUnlockParty=False`; nenhuma linha `allowPartyManagement: False -> true` |
| **A2 — mod, unlock novo** | `DebugUnlockParty=true`, build 0.5.0 | `DebugUnlockParty armed (no Harmony patch)`; após 5 s + settle, `allowPartyManagement: False -> true (..., trigger=post-load)`; **nenhum** dump novo |
| Poll não encontrou casa | no menu | `[AIC][DEBUG] unlock poll: id=0 state=NoHouse` (esperado; não é falha) |
| Unlock degradou | qualquer erro de interop | `DebugUnlockParty disabled: <motivo>`; jogo continua |
| Probe corrigido | entrar no mundo | `pc=<jogador>`, `authority=...`, `count=0` (antes logava `pc=? members=0` fixo) |
| Party criada (UI nativa) | juntar Thorin | `count 0->1`, `names=Thorin`, `followQuest=True` |
| Segundo membro | juntar Maria | `count 1->2`, `Maria` no `CompanionRegistry`; `Maria, oi` passa a ser endereçado |
| Ordem `follow` | `Thorin, segue` | `action=follow` + `[AIC][ORDER]` com alvo Thorin + villager segue 10–20 m |
| Ordem `stop` | `Thorin, para` | para de seguir; estado nativo consistente |
| Ordem `come` | `Thorin, vem` | aproxima — ou recusa honestamente se não entrar nesta fase |
| Nome inválido | `Thorinzinho, oi` | ignorado, sem exceção |
| Save/reload | salvar, sair, voltar | vínculo restaurado, sem crash |

**Se crashar de novo:**

1. Anotar hora e PID (`Get-Process Aska`) e copiar `LogOutput.log`, `ErrorLog.log` e
   `%LOCALAPPDATA%\CrashDumps\Aska.exe.<PID>.dmp`.
2. `dotnet fsi docs/re/tools/rva-to-method.fsx 0x<rva>` e **conferir `aliases`**.
3. Se o método estiver num thunk com `aliases > 1` (ou o fault terminar em `…1`), é corrupção
   de referência por patch: abandonar o patch e registrar em `docs/re/dead-ends.md`.
4. Se crashar também em **A0**, o problema é do jogo/save — a branch não é a causa.
5. Não repetir o run sem backup de `...\LocalLow\Sand Sailor Studio\Aska\data`.

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
- **Crash nativo sem exceção gerenciada**: mapeie o RVA do WER com
  `dotnet fsi docs/re/tools/rva-to-method.fsx 0x<rva>` **antes** de qualquer hipótese; confira
  `aliases`. Se o método tiver `aliases > 1`, é thunk compartilhado e o patch entra em todos.
  Registre em `docs/re/dead-ends.md`.
- **Patch que não dispara**: provável AOT inlining; trocar para método virtual/público e
  fire-verificar.
- **Patch que dispara em objeto errado**: `is T`/`as T` não valem como guarda — o detour
  materializa `this` como o tipo patcheado. Ver skill `aska-il2cpp` #14/#15.

## 5. Performance

- Baseline: FPS com o mod desabilitado vs habilitado.
- O mod em idle deve custar **< 1 ms/frame**.
- Picos permitidos: carregar modelo, transcrever, sintetizar, chamar o LLM.
- Se houver stutter, instrumentar `Stopwatch` e logar só o que passar de 2 ms (`[AIC][PERF]`).
- Nunca varrer a cena inteira por frame; throttle e cache.

## 6. Crash / bug report (modelo)

```md
## Versão
- Mod: 0.3.0
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
- [ ] **versão bumpada** (`PluginInfo` + `.csproj`) e a linha `[AIC] ... <versão>` batendo com
      o pacote (sem isso o log não distingue builds);
- [ ] carregamento confirmado no log;
- [ ] **controle A0** (sem o mod) e **A1** (unlock off) sem crash, antes de qualquer conclusão
      sobre a branch;
- [ ] todos os casos da fase marcados como **confirmados em jogo**;
- [ ] nenhuma feature “não testada” escondida atrás de um default `false` (se está no pacote,
      o jogador pode ligar; então precisa estar testada ou não pode existir);
- [ ] nenhum `TODO`/código sem chamador exposto (ex.: `TryUnlockNow` sem comando);
- [ ] licenças e créditos atualizados;
- [ ] `CHANGELOG.md`/release notes escritos;
- [ ] tag na `main` e pacote publicado.
