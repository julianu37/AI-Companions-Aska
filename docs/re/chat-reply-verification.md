# Resposta no chat por nome — verificação

- **Data:** 2026-10-28
- **Plugin:** AI Companions - ASKA 0.4.0
- **Branch:** `feat/named-chat-reply`
- **LLM:** LM Studio local, `qwen2.5-3b-instruct`

## 1. Harness

`dotnet run --project tests/LlmIntentHarness -c Release`:

```text
... (parser + protocolo, 33 casos)
PASS  name virgula
PASS  name dois pontos
PASS  name espaco
PASS  name sozinho
PASS  name prefixo falso
PASS  name vazio
PASS  name mais longo ganha
PASS  sem nomes

41/41 checks passed.
```

O `NameAddressing` aceita `Thorin, ...` / `Thorin: ...` / `Thorin ...`, exige o nome completo
(`Thorinzinho` não casa) e prefere o nome mais longo (`Thorin` vence `Thor`).

## 2. Self-test em jogo (cadeia completa)

`AI.SelfTest=true` no `.cfg` → boot do ASKA → `ChatAddressing.TryParse("Thorin, bom dia")`:

```text
[AIC][PATCH] applied chat hook(s): ..., SSSGame.UI.ChatMenu.OnSetup(MenuParameters)
             | harmony-registered=True | prefix="/c" | enabled=True
[AIC][LLM-SELFTEST] enfileirando 'Thorin, bom dia' via ChatAddressing (language=pt-BR, ...)
[AIC][CHAT] addressed to Thorin from selftest-name (7 chars): "bom dia"
[AIC][LLM] intent em 247 ms (mode=JsonSchema): action=none target=- resource=- amount=- reply="Bom dia! Como posso ajudar?"
[AIC][REPLY] (sem ChatMenu) Thorin: Bom dia! Como posso ajudar?
```

- O nome foi removido do texto (`bom dia`, 7 chars) e o destino é `Thorin`.
- O prompt inclui `Você é Thorin, ...` e a instrução de idioma.
- A resposta veio em PT-BR (`Bom dia! Como posso ajudar?`) e o `CompanionChatReply` logou o
  texto com o nome do companheiro.
- O `OnSetup` foi patchado, mas **não disparou no menu**; não havia `ChatMenu` para a linha
  visual. Por isso o log diz `(sem ChatMenu)`.

## 3. O que falta (única coisa)

- **Linha visual no chat em um mundo:** o `ChatMenu` é capturado no primeiro
  `_OnTextInputSubmit` (quando o jogador envia a mensagem). Aí o `CompanionChatReply` chama
  `ChatMenu._AddNewTextLine(nome, reply, cor, true, false)` por reflexão. O texto/resposta e o
  nome já estão provados; falta ver a linha `Thorin: ...` aparecer no chat.
- Como o launch automatizado via `steam://` desta máquina falha no PlayFab
  (`LoginWithSteam: Invalid input parameters`), o teste tem que ser na sessão normal do Steam.

## 4. Como testar

1. LM Studio com `qwen2.5-3b-instruct` e Local Server em `127.0.0.1:1234`.
2. Abra o ASKA pelo Steam e entre em um mundo.
3. No chat:
   - `Thorin, bom dia` → o log deve mostrar `addressed to Thorin from ChatMenu._OnTextInputSubmit`
     e `[AIC][LLM] intent ...`; o chat deve mostrar `Thorin: ...`.
   - `/c segue` → o mesmo, com o nome padrão.
   - `bom dia` (sem nome/prefixo) → `ignored`.
   - `Thorinzinho, oi` → `ignored` (nome completo + separador).
4. Se o log mostrar `[AIC][REPLY] (sem ChatMenu)`, o prefix não capturou o ChatMenu: confira
   `[AIC][PATCH] ... fired` e se a mensagem foi endereçada.
5. Se o log mostrar `[AIC][REPLY] failed`, o `_AddNewTextLine` não foi encontrado/recusou a
   chamada; anexe o erro.

## 5. Dead-end descoberto

`FindObjectsByType(Type, ...)` aborta o load do plugin com `MissingMethodException` no JIT
(`docs/re/dead-ends.md` #19). Não há busca de cena; a instância vem do prefix (`__instance`).
