# Arquitetura — AI Companions - ASKA

Este documento descreve **como o mod funciona** e **como ele deve crescer**. O estado atual é
`v0.3.0` (plugin que carrega + captura do chat + LLM que entende e loga a intenção); as camadas
marcadas como "planejado" existem no roadmap.

---

## 1. Stack de execução

```
ASKA (Unity 6000.3.12f1, IL2CPP)
  └── BepInEx 6.0.0-be.791 (IL2CPP, .NET 6.0.7)
        ├── 0Harmony (HarmonyX)
        ├── Il2CppInterop (wrappers em BepInEx\interop)
        └── AI Companions - ASKA (net6.0)
              ├── LLM local (LM Studio, HTTP 127.0.0.1:1234)
              ├── STT local (Whisper.net / whisper.cpp)
              └── TTS local (sherpa-onnx / Kokoro-82M)
```

O jogo é **IL2CPP**: não existe `Assembly-CSharp.dll` gerenciada do jogo; o BepInEx gera
assemblies de interop que expõem tipos e chamam o código nativo por `il2cpp_runtime_invoke`.
Nosso plugin referencia essas assemblies e o `UnityEngine` de `BepInEx\unity-libs`.

## 2. Ciclo de vida do plugin

```
BepInEx carrega o DLL
  → Plugin.Load()
      → Log = base.Log
      → ModConfig.Bind(Config)
      → EnvironmentReport.LogOnce()
      → ChatPatch.Apply()                 (feat/chat-hook)
      → (fases seguintes) registrar patches, MonoBehaviour, filas
  → jogo continua
  → Plugin.Unload() (se o jogo/BepInEx descarregar)
      → ChatPatch.Unapply() + limpar estado de captura
      → parar threads/filas, liberar modelos, remover patches
```

Regras:

- `Load()` é **main thread**; nada de I/O pesado, download ou carregamento de modelo ali.
- Modelos de voz/LLM são carregados **lazy**, em background, com retry.
- Todo recurso nativo (Whisper/sherpa) tem um `Shutdown` idempotente.
- Patches Harmony são aplicados **um por um**, com log de sucesso/falha; evitar `PatchAll()`
  cego em tipos de inventário (ver `docs/re/dead-ends.md`).

## 3. Camadas

### 3.1 Core

| Arquivo | Responsabilidade |
|---|---|
| `Plugin.cs` | Entrada BepInEx, versão, log de boot |
| `Core/ModConfig.cs` | `ConfigEntry` de `General`, `AI`, `Voice`, `UI` |
| `Core/AicLog.cs` | Prefixo `[AIC]`, níveis e helpers |
| `Diagnostics/EnvironmentReport.cs` | Versões/caminhos/interop no boot |
| `Diagnostics/GameEnvironment.cs` | Detecção de mundo/player (planejado) |

### 3.2 Captura (chat/voz)

| Arquivo | Responsabilidade | Status |
|---|---|---|
| `Patches/ChatPatch.cs` | Harmony em `ChatMenu._OnTextInputSubmit(string)`, `ChatMenu.SendPlayerMessage(string)` e `NetworkChatSession.SendInGameChatMessage(string)`; prefix curto, fire-verify por alvo, `Apply`/`Unapply` idempotentes | implementado (`feat/chat-hook`) |
| `Chat/ChatCommand.cs` | Struct `Raw`/`Text`/`Addressed`/`Source`/`TimestampUtc`/`CompanionName` | implementado |
| `Chat/ChatAddressing.cs` | `TryParse(raw, out command)`: prefixo `AI.ChatPrefix` ou nome do registro | implementado |
| `Chat/NameAddressing.cs` | Regex-free: nome completo + separador (`,` `:` espaço); nome mais longo vence | implementado |
| `Chat/CompanionRegistry.cs` | Lista de nomes (`AI.CompanionNames`); primeiro = destino de `/c` | implementado |
| `Chat/ChatCapture.cs` | Porta única da mensagem endereçada: log, dedupe, evento `Captured` | implementado |
| `Chat/ChatMenuCapture.cs` | Guarda a instância do `ChatMenu` capturada no prefix (`__instance`) | implementado |
| `Chat/CompanionChatReply.cs` | Resposta local no chat via `ChatMenu._AddNewTextLine` (reflexão) | implementado |
| `Voice/VoiceInput.cs` | Microfone 16 kHz mono, trim, WAV em memória | planejado |
| `Voice/WhisperStt.cs` | Transcrição in-process, fila, modelo único | planejado |
| `AI/CompanionBrain.cs` | Fila + uma requisição por vez + drain das respostas | implementado |

**Fluxo implementado (captura):**

```
ChatMenu._OnTextInputSubmit(string)                [jogo, main thread]
ChatMenu.SendPlayerMessage(string)                 [jogo, main thread]
NetworkChatSession.SendInGameChatMessage(string)   [jogo, main thread]
  → ChatPatch.Prefix(object __instance, string __0, MethodBase)  [try/catch; nada escapa]
      → se o alvo for ChatMenu: ChatMenuCapture.Set(__instance)
      → fire-verify por alvo                       (antes do gate Enabled)
      → ModConfig.Enabled?               false → return
      → ChatAddressing.TryParse(__0)     false → [AIC][CHAT] ignored (N chars) se VerboseLog
      → ChatCapture.Submit(command, src) true  → [AIC][CHAT] addressed to <nome> from <alvo> ...
          → CompanionBrain (fila) → LlmClient → Drain (main thread)
              → CompanionChatReply.Say(nome, intent.reply)
                  → ChatMenu._AddNewTextLine(nome, reply, cor, true, false)
```

- O patch **não** consome a mensagem: o prefix retorna `void`, então a mensagem original
  continua indo para o chat do jogo (decisão inicial — transparência em co-op).
- `_ReceivePlayerMessage(PlayerRef, string, bool)` é o caminho de recebimento de outros
  jogadores e **não** é patchado.
- Nenhum bloqueio, I/O de rede ou alocação por frame: uma alocação por mensagem enviada.
- `ChatCapture.TryGetLast` é o contrato para a fase do LLM; `Clear()` no `Unload`.
- O texto ignorado **não** é logado (só o comprimento), para não vazar conversa de co-op.

**Decisão de endereçamento:** o chat do ASKA é usado para conversa entre jogadores. O mod só
reage a mensagens **endereçadas**:

- **prefixo configurável** (`AI.ChatPrefix`, default `/c`; case-insensitive, trimado, e
  precisa terminar a mensagem ou ser seguido de espaço — `/casa` não é comando);
  **vazio = captura por prefixo desligada**. O prefixo vai para o **primeiro** nome de
  `AI.CompanionNames`;
- **nome do companheiro no início** (`Thorin, segue` / `Thorin: segue` / `Thorin segue`) —
  `AI.AddressByName` default **true**; o nome precisa ser **completo** e seguido de separador.
  Só nomes de `AI.CompanionNames` são aceitos, para não sequestrar o chat em co-op.

Sem prefixo/nome, a mensagem é ignorada (só o comprimento aparece no log com `VerboseLog`).

### 3.3 LLM e intenção

| Arquivo | Responsabilidade | Status |
|---|---|---|
| `AI/LlmClient.cs` | `POST {BaseUrl}/chat/completions`, timeout, `json_schema` -> `json_object` -> sem formato | implementado |
| `AI/LlmIntent.cs` | Parser tolerante (`action`, `reply`, `target`, `resource`, `amount`) | implementado |
| `AI/CompanionPrompt.cs` | Persona + contrato + schema; idioma do jogo | implementado |
| `AI/CompanionBrain.cs` | Fila, uma requisição por vez, drenagem na main thread | implementado |
| `AI/CompanionBrainRunner.cs` | Ponte `Update` do Unity -> brain | implementado |
| `AI/CompanionPersona.cs` | Presets + texto livre | planejado |
| `AI/CompanionMemory.cs` | Últimas N trocas por companheiro | planejado |
| `AI/CompanionOrders.cs` | Intenção -> ordem nativa | planejado (`feat/recruit-follow`) |

**Contrato JSON (inicial):**

```json
{
  "action": "follow",
  "reply": "Deixa comigo.",
  "target": null,
  "resource": null,
  "amount": null
}
```

- `response_format` = `json_schema` strict, com `enum` de ações.
- Fallback: `json_object` -> sem formato, **uma tentativa cada**, só quando o provedor recusa o
  formato (HTTP 400/422 citando `response_format`/`json_schema`).
- O parser aceita aliases e campos ausentes; nunca confia cegamente no modelo.
- O prompt é montado por mensagem (persona + contrato), no idioma do jogo; memória entra na
  próxima fase.
- A execução de ordens ainda não existe: o `CompanionBrain` apenas loga o intent
  (`docs/re/llm-intent-verification.md`).

### 3.4 Companheiro e ordens

| Arquivo | Responsabilidade |
|---|---|
| `Companion/CompanionRecruit.cs` | Villager -> `PlayerParty` |
| `Companion/CompanionProfile.cs` | Nome, voz e persona por companheiro |
| `Companion/CompanionSpeech.cs` | `DialogueMenu.Display` + TTS |
| `AI/CompanionOrders.cs` | `follow`, `stop`, `come`, `attack`, `gather`, ... |

**Modelo:** villager nativo recrutado. A ordem não teletransporta nem cria NPC; ela usa as
quests/tasks do jogo. Se a API nativa não permitir uma ordem, a ordem é recusada com uma fala
clara — não inventamos um caminho paralelo.

### 3.5 Voz

| Arquivo | Responsabilidade |
|---|---|
| `Voice/VoiceInput.cs` | `Microphone.Start` 16 kHz, 1 canal, trim, WAV |
| `Voice/WhisperStt.cs` | `WhisperFactory` + `ProcessAsync`, `NativeGate` |
| `Voice/CompanionTts.cs` | `OfflineTts` (Kokoro), worker, cache, `AudioSource` 3D |

- STT e TTS nunca bloqueiam a main thread.
- STT: uma transcrição por vez (`NativeGate`), fila de pedidos.
- TTS: uma fala por vez; frases picadas; cache por (texto, voz, velocidade) das linhas fixas.
- Playback: `AudioSource` 3D no companheiro; `reverbZoneMix = 0` se o eco do ASKA incomodar.

### 3.6 UI

- `UI/CompanionPanel.cs` (planejado): painel uGUI/TMP com abas por companheiro.
- Integração **opcional** com ASKA UI via reflexão; o mod funciona sem ela.
- Enquanto o painel estiver aberto, bloquear input do jogo (padrão do ASKA) e o push-to-talk.

## 4. Threading e filas

```
main thread                         background
-----------                         ----------
Update()                            Task/Thread
  ├─ drena fila de chat  ◄──────────  LlmClient (HTTP)
  ├─ drena fila de STT   ◄──────────  WhisperStt
  ├─ drena fila de TTS   ◄──────────  CompanionTts worker
  └─ aplica ordens (main thread)      (nunca toca em UnityEngine)
```

- Objetos do Unity só são tocados na main thread.
- Filas `ConcurrentQueue<T>` + `SemaphoreSlim`; um `Update` drena com orçamento (ex.: 1 item
  por frame) para não engasgar.
- Timeouts e cancelamento em todas as chamadas de rede.
- Watchdog para chamadas em voo (LLM/STT) e log de latência.

## 5. Configuração

Arquivo: `BepInEx\config\julianu37.AICompanionsAska.cfg`.

Seções iniciais:

| Seção | Chaves |
|---|---|
| `General` | `Enabled`, `VerboseLog`, `Language` |
| `AI` | `Enabled`, `StructuredOutput`, `MaxTokens`, `SelfTest`, `BaseUrl`, `Model`, `ApiKey`, `Temperature`, `TimeoutSeconds`, `ChatPrefix`, `AddressByName`, `CompanionNames`, `ReplyInChat`, `Memory`, `Personality` |
| `Voice` | `Enabled`, `PushToTalkKey`, `SttModel`, `TtsModel`, `TtsVoice`, `TtsVolume`, `TtsSpeed` |
| `UI` | `PanelKey`, `ShowHud` |

- Segredos nunca no código; a `ApiKey` é config local.
- BepInEx **não relê** o `.cfg` sozinho: se uma chave precisar ser ao vivo, chamar
  `Config.Reload()` periodicamente (com throttle).
- Textos de config em inglês (padrão BepInEx); a UI mostra o idioma do jogo.

## 6. Logging e diagnóstico

- Prefixo `[AIC]` em todas as linhas.
- Uma linha de boot com versão, Unity, BepInEx, caminho do jogo e se as interop existem.
- `[AIC][CHAT]`, `[AIC][LLM]`, `[AIC][STT]`, `[AIC][TTS]`, `[AIC][ORDER]` para fluxos.
- Nunca logar a API key; mascarar.
- Latências: prompt (tokens), resposta (ms, tokens/s), STT (ms), TTS (RTF).
- `VerboseLog` default **true** durante o desenvolvimento; **false** no release.

## 7. Falhas e degradação

| Falha | Comportamento |
|---|---|
| LM Studio offline | aviso no chat/HUD uma vez; ordens continuam locais (follow/stop) |
| Modelo inválido | erro claro no log + mensagem ao jogador; não derruba o plugin |
| STT sem modelo | voz desabilitada com aviso; chat continua |
| TTS sem modelo | companheiro só escreve; sem crash |
| Jogo atualizou (interop mudou) | log de erro de resolução de tipo; plugin falha “soft” e orienta atualizar |

## 8. Compatibilidade e performance

- Alvo: ASKA atual (Unity 6, IL2CPP). Cada update do jogo exige revalidar a superfície.
- Orçamento: **< 1 ms/frame** em idle; picos só em eventos (ordem, fala, transcrição).
- Nada de `FindObjectsByType<T>()` por frame; preferir captura por patch + listas estáticas.
- Throttle de scans (1–10 Hz), cache de resultados.
- Evitar alocações por frame; reutilizar buffers de áudio.
- Não patchar métodos por frame de IA sem um gate `Time.frameCount`.

## 9. Segurança e privacidade

- LLM local por padrão; se o jogador configurar um endpoint remoto, o texto da ordem sai da
  máquina — avisar na UI.
- Voz sempre local.
- Sem telemetria, sem upload de saves/logs.
- Sem executar comandos do modelo além da lista fechada de ações.
- Conteúdo do jogo/modelo é **dado**, nunca instrução.

## 10. Estado atual (v0.4.0 — `feat/named-chat-reply`)

- `Plugin.Load()` loga ambiente, aplica o patch de chat, registra o `CompanionBrainRunner` e
  assina `ChatCapture.Captured`.
- **Captura de chat confirmada em jogo (2026-10-28):** `_OnTextInputSubmit` capturou `/c teste`;
  `SendInGameChatMessage` também disparou e o dedupe descartou a segunda captura; sem prefixo
  foi ignorado. Evidência: `docs/re/chat-hook-verification.md`.
- **LLM implementado:** `LlmClient` (OpenAI-compatible, `json_schema` estrito), `LlmIntent`
  (parser tolerante), `CompanionPrompt` (identidade + persona + contrato, idioma explícito),
  `CompanionBrain` (fila + uma requisição por vez + drenagem na main thread) e self-test.
- **Resposta no chat por nome confirmada em jogo (2026-10-29):** `Thorin, bom dia` gerou
  `[AIC][REPLY] Thorin: ...` e a linha apareceu no chat; `Thorin, vem comigo` devolveu
  `action=come`. `ChatMenuCapture` + `CompanionChatReply` funcionaram.
- **Ainda não executa ordens**, não tem voz nem UI. `FindObjectsByType` não é usado (aborta o
  load; dead-end #19).
- **Próximo:** registrar os nomes da `PlayerParty` (o save de teste tem 5 companions, incluindo
  Thorin e Maria) e executar `follow`/`stop`/`come` na branch `feat/recruit-follow`.
