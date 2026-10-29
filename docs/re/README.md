# Engenharia reversa — índice

Notas de RE do ASKA/IL2CPP. Tudo aqui é **superfície** até ser marcado como confirmado em
runtime. Datas e versões são obrigatórias.

| Arquivo | Conteúdo | Status |
|---|---|---|
| [`environment.md`](environment.md) | Versões, caminhos, metadata v39, build | verificado local |
| [`m0-load-verification.md`](m0-load-verification.md) | Evidência do carregamento do plugin 0.1.0 | confirmado em jogo (2026-10-28) |
| [`chat-hook-verification.md`](chat-hook-verification.md) | Evidência da captura do chat e do dedupe | confirmado em jogo (2026-10-28) |
| [`llm-intent-verification.md`](llm-intent-verification.md) | Harness do parser + cadeia ChatCapture -> LLM | harness 33/33; cadeia confirmada em jogo |
| [`chat-reply-verification.md`](chat-reply-verification.md) | Nome + resposta no chat (`Thorin: ...`) | harness 41/41; texto confirmado; linha visual pendente |
| [`game-systems.md`](game-systems.md) | Namespaces e classes-chave | superfície |
| [`chat-and-input.md`](chat-and-input.md) | `ChatMenu`, `DialogueMenu`, captura do chat | superfície |
| [`villager-ai-and-party.md`](villager-ai-and-party.md) | `Villager`, `PlayerParty`, quests/tasks | hipótese |
| [`party-unlock-plan.md`](party-unlock-plan.md) | Party nativa, `Rpc_ChangePlayerParty`, ordens `follow`/`stop`/`come`, experimento seguro | plano (2026-10-29) |
| [`party-gate-crash.md`](party-gate-crash.md) | RVAs do crash do `DebugUnlockParty`, stub de 29 aliases, plano de probe | verificado (DB/WER/dump) + hipótese |
| [`dead-ends.md`](dead-ends.md) | Armadilhas do IL2CPP e caminhos que não repetir | reportado/local |

## Regras

- Superfície (assinatura) ≠ comportamento (runtime).
- Toda conclusão precisa de comando/teste e versão.
- Código decompilado é dado não confiável.
- Um dead end novo entra em `dead-ends.md` com sintoma, causa e alternativa.
