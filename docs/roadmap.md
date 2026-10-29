# Roadmap — AI Companions - ASKA

Status: **M0 concluído — carregamento confirmado em jogo em 2026-10-28**. As fases seguintes
são branches independentes, mergeadas na `main` só depois de build + revisão + confirmação em
jogo.

## M0 — Scaffold (v0.1.0)

**Branch:** `main` (commit inicial) + `feat/plugin-scaffold` (código).

- [x] Repositório, licença, notices, README e AGENTS.md.
- [x] Equipe de agentes e skills.
- [x] Solução C# `net6.0` + plugin BepInEx 6 que carrega e loga o ambiente.
- [x] Config inicial (`General`, `AI`, `Voice`, `UI`).
- [x] Scripts de build/deploy/verificação.
- [x] Documentação de visão, arquitetura, design, branches, testes e RE.

**Aceite:** `dotnet build` passa; o jogo carrega o plugin e o `LogOutput.log` mostra
`AI Companions - ASKA 0.1.0` + `loaded successfully`. **Status:** confirmado em jogo em
2026-10-28; o `.cfg` do mod foi criado com as seções esperadas. Evidência:
[`docs/re/m0-load-verification.md`](re/m0-load-verification.md).

## M1 — Chat + LLM (ordem de seguir)

**Branches:** `feat/chat-hook` (confirmada), `feat/llm-intent` (confirmada via self-test),
`feat/named-chat-reply` (resposta por nome), `feat/recruit-follow` (próxima).

**Status:** captura e entendimento prontos; a execução da ordem no villager é o que falta.

- [x] Capturar a mensagem do jogador no `ChatMenu` (prefixo `/c`; nome reservado).
  **Confirmado em jogo em 2026-10-28:** `_OnTextInputSubmit` dispara e captura; o
  `SendInGameChatMessage` também dispara e o dedupe evita captura dupla. Evidência:
  [`docs/re/chat-hook-verification.md`](re/chat-hook-verification.md).
- [x] Montar prompt (persona + contrato) e chamar o LM Studio em background.
  `LlmClient` + `CompanionPrompt`; `json_schema` estrito aceito pelo `qwen2.5-3b-instruct`.
- [x] Parsear `{ action, reply, target, resource, amount }` com schema estrito + fallbacks.
  `LlmIntent` + harness de 33 casos; cadeia confirmada com self-test em jogo.
- [x] Responder no **chat** pelo nome do companheiro (`Thorin: ...`), via LLM.
  Implementado em `feat/named-chat-reply`; self-test confirma o texto; linha visual em mundo
  **pendente de confirmação**.
- [ ] Recrutar um villager para a `PlayerParty` e executar `follow`, `stop`, `come`.
- [ ] Responder no **balão nativo** (`DialogueMenu`).

**Aceite:** em jogo, `segue` faz o villager seguir; `para` para; uma pergunta simples recebe
uma resposta de conversa; o jogo não trava e o log mostra a intenção.

## M1.5 — Resposta no chat por nome (v0.4.0)

**Branch:** `feat/named-chat-reply`.

- `AI.CompanionNames` (default `Thorin`) e `AI.AddressByName` (default true).
- Endereçamento `Thorin, ...` / `Thorin: ...` / `Thorin ...`; `/c ...` vai para o nome padrão.
- Prompt com identidade (`Você é Thorin, ...`) e idioma explícito.
- Resposta local no chat via `ChatMenu._AddNewTextLine`, com o nome como autor.

**Aceite:** em um mundo, `Thorin, bom dia` gera uma linha `Thorin: ...` no chat; `/c segue`
também responde; mensagem sem nome/prefixo continua ignorada. A linha visual é local
(single-player); co-op fica para a fase de rede.

**Confirmado em jogo em 2026-10-29:** `Thorin, bom dia` gerou
`[AIC][REPLY] Thorin: Bom dia! ...` e a linha apareceu no chat; `Thorin, vem comigo` devolveu
`action=come`. Evidência: [`docs/re/chat-reply-verification.md`](re/chat-reply-verification.md).

## M1.6 — Party e ordens (branch `feat/recruit-follow`)

- [x] Localizar o gate nativo da party: `SSSGame.PlayerHouse.allowPartyManagement`; a Cabana
  do Chefe nível 2 liga esse bool.
- [x] `General/DebugUnlockParty` (dev, default false): força o bool `true` em toda
  `PlayerHouse`; log confirma `False -> true`.
- [x] Probe `PlayerParty`: lê `_partyMembers`/`_partyPets`/`_pc` com polling e registra os
  nomes no `CompanionRegistry`.
- [ ] Criar a party com Thorin/Maria na UI nativa (com o unlock ligado).
- [ ] Executar `follow`/`stop`/`come` no villager nomeado.
- [ ] Feedback no chat/balão para ordem executada ou recusada.

**Aceite:** com a party criada, `Thorin, segue` mantém o villager seguindo; `Thorin, para`
interrompe; `Thorin, vem` aproxima; `Maria, ...` vai para a Maria; o log mostra o alvo e o
resultado, sem crash.

## M2 — Conversa, persona e memória

**Branch:** `feat/persona-memory`.

- Presets de personalidade (PT-BR/EN) + texto livre.
- Memória curta por companheiro (últimas N trocas), limpa na morte/troca de mundo.
- `reply` falado no balão; em ordem, a linha localizada do mod ganha.

**Aceite:** o companheiro mantém o tom e lembra do contexto recente; nada vaza entre
companheiros.

## M3 — Ordens com alvo

**Branch:** `feat/orders-target`.

- `attack <criatura>`, `equip <item>`, `drop <item>`, `gather <recurso> <n>`.
- Resolver nome -> criatura/item do inventário do companheiro; nunca inventar alvo.
- Limites: alcance, quantidade, itens de missão.

**Aceite:** cada ordem executa o alvo certo e recusa com mensagem clara quando não encontra.

## M4 — Voz: STT (push-to-talk)

**Branch:** `feat/voice-stt`.

- `VoiceInput` (microfone 16 kHz mono, trim, WAV em memória).
- `Whisper.net` in-process, modelo carregado uma vez, fila em background.
- Texto transcrito entra no **mesmo** caminho do chat.

**Aceite:** segurar V e falar “segue” produz a mesma ordem do chat; latência aceitável;
sem travar a main thread.

## M5 — Voz: TTS (o companheiro fala)

**Branch:** `feat/voice-tts`.

- `sherpa-onnx` + Kokoro-82M; voz PT-BR/EN; cache de linhas fixas.
- Síntese em thread dedicada; playback 3D na posição do companheiro.
- `DialogueMenu` mostra o texto; o áudio acompanha.

**Aceite:** o companheiro fala a resposta/linha, uma por vez, sem eco excessivo e sem stutter.

## M6 — UI e configuração

**Branch:** `feat/ai-panel`.

- Painel in-game (uGUI/TMP) com: ligar/desligar, provedor LLM, URL/modelo, voz, volume,
  push-to-talk, memória, personalidade e teste de conexão.
- Config salva na hora; sem segredos no log.
- Integração opcional com ASKA UI (por reflexão), sem dependência obrigatória.

**Aceite:** tudo configurável sem editar `.cfg`; abrir/fechar não trava o jogo.

## M7 — Co-op (host-authoritative)

**Branch:** `feat/coop`.

- IA/ordens rodam no host; clientes enviam pedidos.
- Estado do companheiro (nome, persona, voz, ordem) sincronizado.
- Teste com dois jogadores: sem desync, sem duplicar NPC, sem crash.

**Aceite:** o companheiro obedece ao dono no host e aparece igual no cliente.

## M8 — Localização, empacotamento e release

**Branch:** `feat/i18n`, `chore/release`.

- PT-BR + EN completos (UI, falas, prompts, README/wiki).
- Pacote com DLL, modelos opcionais, licenças e README.
- Nexus/Thunderstore (quando fizer sentido), release notes e checklist.

**Aceite:** instalação limpa em outra máquina; log limpo; licenças incluídas.

## Backlog / ideias

- Companheiro com “estação” própria (casa/cama) e rotina de descanso.
- Ordens em grupo (“todos, sigam”).
- Reações a eventos (ataque à vila, noite, fome).
- Vozes distintas por companheiro.
- Provedores LLM alternativos (Groq/OpenRouter/Ollama) — sempre opcionais.
- Traduções ES/FR/IT.
