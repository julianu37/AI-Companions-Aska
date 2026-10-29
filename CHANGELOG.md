# Changelog

Todas as mudanças relevantes do projeto. Formato baseado em Keep a Changelog;
versionamento semântico (`MAJOR.MINOR.PATCH`).

## [0.5.0] — em andamento (`feat/recruit-follow`, **pausado em 2026-10-29**)

> **Não mergeada na `main`.** Falta confirmação em jogo do unlock, da party e das ordens.
> Estado e próximo passo: `docs/handoff.md`.

### Adicionado

- **Gate da party descoberto:** `SSSGame.PlayerHouse.allowPartyManagement`; a Cabana do Chefe
  nível 2 liga o bool. `General/DebugUnlockParty` (dev, default false) usa **poll de main
  thread sem Harmony**, que escreve o bool só depois do load, em `PlayerHouse` de cena com
  `OwnerStructure` resolvido e 5 s de estabilidade.
- **Probe `PlayerParty`:** leitura com polling de 1 Hz e registro dos nomes no
  `CompanionRegistry`.
- **Mapeamento da membership:** fica no `Villager` (`_playerParty`, `__PlayerPartyID`,
  `Rpc_ChangePlayerParty`), não na `PlayerParty`; `PartyFollowQuest` é quem faz o follow.
- **Ferramenta de RE:** `docs/re/tools/rva-to-method.fsx` mapeia RVA → método e mostra
  aliases (rodar antes de qualquer Harmony patch).

### Corrigido

- **Crash nativo do unlock de dev** (`0xC0000005`, `GameAssembly+0xF72955`): era um patch
  Harmony em `PlayerHouse._OnActivate`, um **thunk compartilhado por 29 métodos**. O detour
  materializa `this` sem validar a classe, e a escrita do bool caía em `WeatherManager`,
  `TaskManager` etc. Regras novas na skill `aska-il2cpp` (armadilhas 14/15).
- **Versão bumpada para 0.5.0** (`PluginInfo` + `.csproj`), para o log identificar o build.

### Pendente

- Confirmar a escrita `trigger=post-load` em jogo (armou e fez poll sem crash; não escreveu).
- Reobservar o probe corrigido (`pc`/`authority`/`count`/nomes) — o anterior usava `GetField`
  em membros que são *properties* no interop e por isso sempre logava `members=0`.
- Criar a party com Thorin/Maria na UI nativa.
- Executar `follow`/`stop`/`come`; `come` só se o experimento nativo aprovar.
- Feedback no chat/balão.

## [0.4.0] — 2026-10-28

### Adicionado

- **Endereçamento por nome:** `AI.AddressByName` (default true) aceita
  `Thorin, ...` / `Thorin: ...`; o nome tem que ser completo e seguir um separador.
- **Registro de nomes:** `AI.CompanionNames` (default `Thorin`, lista separada por vírgula);
  o primeiro nome é o destino padrão de `/c ...`.
- **Resposta no chat:** `ChatMenuCapture` guarda a instância do `ChatMenu` no prefix; o
  `CompanionChatReply` adiciona a fala com o nome do companheiro via
  `_AddNewTextLine(userName, message, color, true, false)`.
- **Prompt com identidade:** o system prompt passa a ser `Você é Thorin, <persona>`; o modelo
  recebe uma instrução explícita de idioma (PT-BR/EN).
- `AI.ReplyInChat` (default true) liga/desliga a linha no chat.
- Harness de nomes: **41/41** (8 casos novos de `NameAddressing`).

### Notas

- A resposta no chat é **local** (`_AddNewTextLine`, `isMe=false`); em co-op ainda não é
  replicada.
- O `ChatMenu` só é capturado quando o jogador envia uma mensagem (prefix); não há busca de
  cena (`FindObjectsByType(Type, ...)` aborta o load do plugin — dead-end #19).
- **Confirmado em jogo em 2026-10-29:** `Thorin, bom dia` gerou
  `[AIC][REPLY] Thorin: Bom dia! Como posso ajudar?` e a linha apareceu no chat;
  `Thorin, vem comigo` devolveu `action=come`. O `ChatMenu` foi capturado no primeiro envio.
  Evidência: `docs/re/chat-reply-verification.md`.
- Ainda **não executa ordens**, não tem voz nem UI.

## [0.3.0] — 2026-10-28

### Adicionado

- **Cliente LLM local** (`AI/LlmClient.cs`): `POST {BaseUrl}/chat/completions`
  OpenAI-compatible, timeout por requisição, `Authorization` opcional, cadeia de formato
  `json_schema` -> `json_object` -> sem formato.
- **Parser de intenção** (`AI/LlmIntent.cs`): `action`, `reply`, `target`, `resource`,
  `amount`; aliases PT-BR/EN; code fence; amount string; `resource` manda quando o modelo
  devolve o recurso no campo `action`.
- **Prompt e schema** (`AI/CompanionPrompt.cs` + `AI/LlmProtocol.cs`): persona + contrato,
  idioma configurado (pt-BR/EN; `auto` = pt-BR nesta fase), `enum` fechado das ações.
- **Brain** (`AI/CompanionBrain.cs` + `CompanionBrainRunner`): fila, uma requisição por vez,
  drenagem na main thread, log do intent; self-test por `AI.SelfTest` ou
  `AIC_LLM_SELFTEST=1`.
- **Harness** `tests/LlmIntentHarness` (33 casos: parser + URL/schema/format rejection).
- Config `[AI] Enabled`, `StructuredOutput`, `MaxTokens`, `SelfTest`.
- Log do intent só no `Drain` (main thread); o texto do chat e o `reply` só aparecem com
  `VerboseLog=true`.

### Notas

- O LLM **entende** e loga a intenção, mas **ainda não executa** ordens.
- Chat hook (`/c ...`) continua igual; o brain assina o evento `ChatCapture.Captured`.
- **Cadeia confirmada com self-test em jogo (2026-10-28):** `ChatCapture` -> `CompanionBrain`
  -> LM Studio -> ação válida (`follow`/`come`) em `mode=JsonSchema` (~230 ms). Harness:
  33/33. Evidência: `docs/re/llm-intent-verification.md`.
- **Pendente:** `/c ...` real em um mundo com o LLM ligado (captura e cadeia já provadas
  separadamente); será feito na branch `feat/recruit-follow`.
- O `ChatMenu._OnTextInputSubmit` e o `NetworkChatSession.SendInGameChatMessage` são os
  caminhos confirmados da captura (v0.2.0).

## [0.2.0] — 2026-10-28

### Adicionado

- **Captura do chat do jogador** via Harmony em
  `SSSGame.UI.ChatMenu.SendPlayerMessage(string)`, com prefixo configurável (`AI.ChatPrefix`,
  default `/c`), ignorando mensagens não endereçadas.
- `Chat/ChatCommand`, `Chat/ChatAddressing`, `Chat/ChatCapture` e `Patches/ChatPatch` (patch
  manual por alvo, sem `PatchAll`; `Apply`/`Unapply` idempotentes).
- Fire-verify no log: `[AIC][PATCH] applied chat hook ...` e `... fired (first call ...)`.

### Notas

- **Aplicação e disparo confirmados em jogo em 2026-10-28:** `_OnTextInputSubmit` capturou
  `/c teste`; `SendInGameChatMessage` também disparou e o dedupe descartou a segunda captura;
  sem prefixo foi ignorado. Evidência: `docs/re/chat-hook-verification.md`.
- `AI.AddressByName` continua reservado (sem lista de companheiros); só o prefixo captura.
- A mensagem original **não** é suprimida do chat do jogo.
- LLM, resposta, voz e UI ainda não existem.

## [0.1.0] — 2026-10-28

### Adicionado

- Estrutura inicial do repositório `AI Companions - ASKA`.
- Equipe de agentes OpenCode (lead + 10 especialistas) em `.opencode/agents/`.
- Skills técnicas em `.opencode/skills/` (BepInEx, IL2CPP, RE, sistemas do ASKA, Unity 6,
  LLM, voz, co-op, QA/release, branches, localização).
- Documentação: visão, roadmap, arquitetura, design, branches, testes, referências e notas
  de engenharia reversa (`docs/re/`).
- Solução C# `net6.0` e plugin BepInEx 6 IL2CPP mínimo (`Plugin.cs`, `ModConfig`,
  `EnvironmentReport`), que carrega e registra o ambiente.
- Config inicial (`General`, `AI`, `Voice`, `UI`) e scripts de build/verificação.
- Licença MIT, notices e créditos de terceiros.

### Notas

- **Não há gameplay** nesta versão: sem chat, LLM, voz ou ordens.
- **Carregamento confirmado em jogo em 2026-10-28** (log + `.cfg` criados); evidência em
  [`docs/re/m0-load-verification.md`](docs/re/m0-load-verification.md).
- Próximas branches previstas: `feat/chat-hook`, `feat/llm-intent`, `feat/recruit-follow`,
  `feat/voice-stt`, `feat/voice-tts`.
