# Roadmap — AI Companions - ASKA

Status: **M0 concluído** (scaffold). As fases seguintes são branches independentes, mergeadas
na `main` só depois de build + revisão + confirmação em jogo.

## M0 — Scaffold (v0.1.0)

**Branch:** `main` (commit inicial) + `feat/plugin-scaffold` (código).

- [x] Repositório, licença, notices, README e AGENTS.md.
- [x] Equipe de agentes e skills.
- [x] Solução C# `net6.0` + plugin BepInEx 6 que carrega e loga o ambiente.
- [x] Config inicial (`General`, `AI`, `Voice`, `UI`).
- [x] Scripts de build/deploy/verificação.
- [x] Documentação de visão, arquitetura, design, branches, testes e RE.

**Aceite:** `dotnet build` passa; o jogo carrega o plugin e o `LogOutput.log` mostra
`AI Companions - ASKA 0.1.0` + `loaded successfully`.

## M1 — Chat + LLM (ordem de seguir)

**Branches:** `feat/chat-hook`, `feat/llm-intent`, `feat/recruit-follow`.

- Capturar a mensagem do jogador no `ChatMenu` (prefixo `/c` ou nome do companheiro).
- Montar prompt (persona + contrato) e chamar o LM Studio em background.
- Parsear `{ action, reply, target, resource, amount }` com schema estrito + fallbacks.
- Recrutar um villager para a `PlayerParty` e executar `follow`, `stop`, `come`.
- Responder no chat e no balão nativo (`DialogueMenu`).

**Aceite:** em jogo, `segue` faz o villager seguir; `para` para; uma pergunta simples recebe
uma resposta de conversa; o jogo não trava e o log mostra a intenção.

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
