# Changelog

Todas as mudanças relevantes do projeto. Formato baseado em Keep a Changelog;
versionamento semântico (`MAJOR.MINOR.PATCH`).

## [0.3.0] — 2026-10-28

### Adicionado

- **Cliente LLM local** (`AI/LlmClient.cs`): `POST {BaseUrl}/chat/completions`
  OpenAI-compatible, timeout por requisição, `Authorization` opcional, cadeia de formato
  `json_schema` -> `json_object` -> sem formato.
- **Parser de intenção** (`AI/LlmIntent.cs`): `action`, `reply`, `target`, `resource`,
  `amount`; aliases PT-BR/EN; code fence; amount string; `resource` manda quando o modelo
  devolve o recurso no campo `action`.
- **Prompt e schema** (`AI/CompanionPrompt.cs`): persona + contrato, idioma do jogo,
  `enum` fechado das ações.
- **Brain** (`AI/CompanionBrain.cs` + `CompanionBrainRunner`): fila, uma requisição por vez,
  drenagem na main thread, log do intent; self-test por `AIC_LLM_SELFTEST=1`.
- **Harness** `tests/LlmIntentHarness` (19 casos, todos passando).
- Config `[AI] Enabled`, `StructuredOutput`, `MaxTokens`.

### Notas

- O LLM **entende** e loga a intenção, mas **ainda não executa** ordens.
- Chat hook (`/c ...`) continua igual; o brain assina o evento `ChatCapture.Captured`.
- **Cadeia confirmada com self-test em jogo (2026-10-28):** `ChatCapture` -> `CompanionBrain`
  -> LM Studio -> `action=follow` em `mode=JsonSchema` (226 ms). Harness do parser: 19/19.
  Evidência: `docs/re/llm-intent-verification.md`.
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
