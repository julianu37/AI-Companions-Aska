# Chat hook — confirmação em jogo

- **Data:** 2026-10-28
- **Plugin:** AI Companions - ASKA 0.2.0
- **Jogo:** ASKA `Application.version = 0.4` (`1.48.0910261842._PC.Release`)
- **BepInEx:** 6.0.0-be.791, Unity 6000.3.12f1
- **Objetivo:** provar que a mensagem endereçada do chat é capturada e que a
  mensagem sem prefixo é ignorada.

## Evidência (`BepInEx\LogOutput.log`)

```text
[AIC][PATCH] applied chat hook(s):
  SSSGame.UI.ChatMenu._OnTextInputSubmit(String),
  SSSGame.UI.ChatMenu.SendPlayerMessage(String),
  SSSGame.Network.NetworkChatSession.SendInGameChatMessage(String)
  | id=julianu37.AICompanionsAska.chat | harmony-registered=True | prefix="/c" | enabled=True

[AIC][PATCH] harmony-registered=True is NOT proof of firing; wait for a '... fired' line after a real chat submit.

[AIC][PATCH] ChatMenu._OnTextInputSubmit(string) fired (first call; string arg received).
[AIC][CHAT] addressed from ChatMenu._OnTextInputSubmit (5 chars): "teste"

[AIC][PATCH] NetworkChatSession.SendInGameChatMessage(string) fired (first call; string arg received).
[AIC][CHAT] duplicate from NetworkChatSession.SendInGameChatMessage ignored (5 chars).

[AIC][CHAT] ignored on ChatMenu._OnTextInputSubmit (5 chars)
[AIC][CHAT] ignored on NetworkChatSession.SendInGameChatMessage (5 chars)
```

Nenhum erro/exception no log.

## Conclusões

1. **O caminho do submit é `ChatMenu._OnTextInputSubmit(string)`.** O alvo original
   `ChatMenu.SendPlayerMessage(string)` foi registrado (`harmony-registered=True`), mas
   **não disparou** no teste; não é o ponto que o campo de texto usa nesta build.
2. **`NetworkChatSession.SendInGameChatMessage(string)` também dispara**, recebendo o texto
   já filtrado. A deduplicação de 750 ms descartou a segunda captura (`duplicate ... ignored`).
3. **Mensagem sem prefixo é ignorada** nos dois caminhos (`ignored ... (5 chars)`), sem vazar o
   texto no log.
4. O prefixo `/c teste` foi capturado como `"teste"` (5 chars), sem o prefixo, como esperado.
5. `AddressByName` está `False` (default novo) e não interferiu.

## O que fica

- O patch mantém os três alvos; os dois que disparam são `_OnTextInputSubmit` e
  `SendInGameChatMessage`. O `SendPlayerMessage` fica como fallback para caminhos que ainda
  não apareceram (ex.: botão de enviar), com o mesmo dedupe.
- `ChatCapture.TryGetLast()` é o contrato para a próxima fase (`feat/llm-intent`).
- O texto do chat só vai para o log com `VerboseLog=true`; no release, só o comprimento.
- A mensagem original continua no chat do jogo (o prefix não cancela o envio).
- Co-op não foi testado; a captura é do envio local, mas `SendInGameChatMessage` pode
  receber mensagens de outros jogadores no host. Validar antes de habilitar ordens em co-op.
