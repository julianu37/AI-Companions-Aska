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

## 3. Confirmação em jogo (2026-10-29)

O jogador carregou o save, enviou `Thorin, bom dia` e o chat respondeu. O log mostra a linha
visual sendo adicionada **sem** `(sem ChatMenu)`:

```text
[AIC][CHAT] ChatMenu instance captured (reply target ready).
[AIC][PATCH] ChatMenu._OnTextInputSubmit(string) fired (first call; string arg received).
[AIC][CHAT] addressed to Thorin from ChatMenu._OnTextInputSubmit (7 chars): "bom dia"
[AIC][PATCH] NetworkChatSession.SendInGameChatMessage(string) fired ...
[AIC][CHAT] duplicate from NetworkChatSession.SendInGameChatMessage ignored (7 chars).
[AIC][LLM] intent em 3890 ms (mode=JsonSchema): action=none ... reply="Bom dia! Como posso ajudar?"
[AIC][REPLY] Thorin: Bom dia! Como posso ajudar?
```

O jogador confirmou: **“está respondendo no chat do jogo”**. Uma segunda mensagem
(`Thorin, vem comigo`) devolveu `action=come` e `[AIC][REPLY] Thorin: Acompanho-te.`; o
`ChatCapture` também ignorou mensagens sem nome/prefixo.

## 4. Estado

- `ChatMenu` capturado no primeiro envio (`_OnTextInputSubmit`/`OnSetup`).
- `_AddNewTextLine` por reflexão funcionou (a linha `Thorin: ...` apareceu).
- A resposta é **local** (single-player); co-op ainda não replica.
- O save de teste tem 5 companions; Thorin e Maria foram renomeados para o desenvolvimento.
  O registro automático de nomes via `PlayerParty` é o próximo passo.

## 5. O que falta

- **Ordens:** o LLM entende `follow`/`stop`/`come`, mas nada é executado; próximo branch
  `feat/recruit-follow`.
- **Registro automático:** hoje `AI.CompanionNames` é configurado à mão; a `PlayerParty` deve
  registrar os nomes dos companions reais.
- **Co-op:** a linha de resposta é local.
- **Voz/UI:** não existem.

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
