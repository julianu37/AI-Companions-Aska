# Arquitetura — AI Companions - ASKA

Este documento descreve **como o mod funciona** e **como ele deve crescer**. O estado atual é
`v0.1.0` (plugin que carrega); as camadas marcadas como “planejado” existem no roadmap.

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
      → (fases seguintes) registrar patches, MonoBehaviour, filas
  → jogo continua
  → Plugin.Unload() (se o jogo/BepInEx descarregar)
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

| Arquivo | Responsabilidade |
|---|---|
| `Patches/ChatPatch.cs` | Harmony em `SSSGame.UI.ChatMenu.SendPlayerMessage(string)` |
| `Voice/VoiceInput.cs` | Microfone 16 kHz mono, trim, WAV em memória |
| `Voice/WhisperStt.cs` | Transcrição in-process, fila, modelo único |
| `AI/CompanionChat.cs` | Normaliza prefixo/nome, monta prompt, submete |

**Decisão de endereçamento:** o chat do ASKA é usado para conversa entre jogadores. O mod só
reage a mensagens **endereçadas**:

- prefixo configurável (`/c ...`), ou
- nome do companheiro no início (`Thorin, segue`).

Sem prefixo/nome, a mensagem é ignorada (ou só logada em modo debug). Isso evita sequestrar o
chat em co-op.

### 3.3 LLM e intenção

| Arquivo | Responsabilidade |
|---|---|
| `AI/LlmClient.cs` | `POST {BaseUrl}/chat/completions`, `Task` + fila |
| `AI/LlmIntent.cs` | Parser tolerante do JSON (`action`, `reply`, `target`, `resource`, `amount`) |
| `AI/CompanionPersona.cs` | Presets + texto livre |
| `AI/CompanionMemory.cs` | Últimas N trocas por companheiro |
| `AI/CompanionOrders.cs` | Intenção -> ordem nativa |

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
  formato (HTTP 400/erro citando `response_format`).
- O parser aceita aliases e campos ausentes; nunca confia cegamente no modelo.
- O prompt é montado por mensagem (persona + memória + contrato), no idioma do jogo.

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
| `AI` | `BaseUrl`, `Model`, `ApiKey`, `Temperature`, `TimeoutSeconds`, `ChatPrefix`, `AddressByName`, `Memory`, `Personality` |
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

## 10. Estado atual (v0.1.0)

- `Plugin.Load()` loga ambiente e versão.
- `ModConfig` cria as seções/chaves iniciais.
- Sem patches, sem captura, sem LLM, sem voz.
- O objetivo desta versão é provar que o toolchain (BepInEx 6 IL2CPP + net6.0 + interop +
  deploy) funciona nesta máquina.
- **Status:** build + deploy OK; carregamento em jogo **pendente de confirmação** (o
  `.cfg` do mod ainda não foi criado).
