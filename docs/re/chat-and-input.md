# RE — chat e entrada de texto

**Fonte:** `SSSGame.UI.ChatMenu`, `SSSGame.UI.ChatMessage`,
`SSSGame.Network.NetworkChatSession` (interop `Assembly-CSharp.dll`), extraído com
`ilspycmd` em **2026-10-28**. Superfície apenas; o comportamento é hipótese até o probe.
**Atualização 2026-10-28:** o hook de captura foi implementado na branch `feat/chat-hook`
(§2 e §6); o disparo segue **pendente de confirmação em jogo**.

---

## 1. `SSSGame.UI.ChatMenu`

- `public class ChatMenu : ContextMenu`
- Campos/estado relevantes:
  - `CanvasGroup textsPanelCvsGroup`, `inputButtonCvsGroup`
  - `Canvas textsPanelCvs`, `inputButtonCvs`
  - `InputFieldHelper inputFieldHelper`
  - `ChatMessage messagePrefabLeft`, `messagePrefabRight`
  - `bool lockChatWindowVisibilityAsTrue`, `lockInputFieldVisibilityAsTrue`
  - `string _currentChannel`
  - `NetworkChatSession _chatSession`
  - `PlayerManager _playeManager`
  - `List<...> _activeMessages`, `_pendingMessages`
- Propriedades públicas:
  - `bool IsTextInputActive { get; set; }`
  - `bool ChatWindowVisibility { get; set; }`
  - `bool InputFieldVisiblity { get; set; }`
  - `string CurrentChannel { get; set; }`
- Eventos (Action):
  - `OnChatWindowVisibilityChange`
  - `OnInputWindowVisibilityChange`
  - `OnCurrentChanelCanged`
- Métodos:
  - **`public void SendPlayerMessage(string message)`** — método público, com um `string`.
    Internamente inicia a state machine `<SendPlayerMessage>d__83`.
  - `OnSetup(MenuParameters)` (override de `ContextMenu`)
  - `_WorkRoutine` (coroutine de manutenção)
- Nested types:
  - `ChatMessageRequest` (`PlayerRef playerId`, `string message`, `bool isNewMessage`)
  - `ChatMenuConfig : MenuParameters` (`visibleChat`, `visibleInput`, `s_cfg`, `Create(...)`)
  - `<SendPlayerMessage>d__83` (async void; `string message`, `PlayerManager.Player _localPlayer`,
    `string _filteredMessage`)

### Implicações

- `SendPlayerMessage(string)` é o ponto mais direto para capturar a mensagem **do jogador
  local**. É público e tem assinatura simples (sem by-ref, sem tipo de inventário), o que é
  favorável para Harmony.
- É um método **async void** (state machine). O patch deve ser um **prefix/postfix curto**,
  sem reimplementar a lógica nem bloquear.
- O método pode ser **inlinado pelo AOT**; fire-verificar com log na primeira execução.
- A mensagem passa por filtro de plataforma (`PlatformUsers.StringVerifyResult`) e por
  `_filteredMessage`; o texto que chega ao `SendPlayerMessage` pode ser o **bruto**.
- O chat tem **canais** (`CurrentChannel`); em co-op, a mensagem pode ir para o canal global.
  O mod deve respeitar o escopo e não responder a mensagens de outros jogadores.

## 2. Captura implementada (`feat/chat-hook`)

O alvo único original (`ChatMenu.SendPlayerMessage`) **aplicou e verificou, mas não disparou**
no teste de 2026-10-28. A branch passou a instrumentar três candidatos, com deduplicação:

```
Harmony Prefix em [ChatPatch]:
  ChatMenu._OnTextInputSubmit(string)          ← submit do campo (provável Enter)
  ChatMenu.SendPlayerMessage(string)           ← wrapper público (mantido)
  NetworkChatSession.SendInGameChatMessage(string) ← envio final (fallback)
    → se o alvo for ChatMenu: ChatMenuCapture.Set(__instance)
    → fire-verify por alvo (antes do gate Enabled)
    → ModConfig.Enabled?            false → return (jogo segue normal)
    → ChatAddressing.TryParse(__0)  false → [AIC][CHAT] ignored (N chars) se VerboseLog
    → ChatCapture.Submit(cmd, src)  true  → [AIC][CHAT] addressed to <nome> from <alvo> ...
    → CompanionBrain -> LlmClient -> Drain -> CompanionChatReply.Say(nome, reply)
    → (a mensagem original segue para o chat do jogo: prefix retorna void)
```

`ChatCapture` descarta a segunda captura da mesma mensagem dentro de 750 ms — os três alvos
podem disparar para um único submit. O log diz qual alvo capturou.

**Decisões tomadas nesta branch:**

- O patch é um **prefix `void`** (sem `__runOriginal`): não cancela nem altera o envio. A
  mensagem crua continua no chat do jogo — transparência em co-op. Suprimir a mensagem
  (`__runOriginal = false`) fica para uma fase posterior, se o ruído incomodar.
- **`_ReceivePlayerMessage` não é patchado** — é o recebimento (outros jogadores).
- Prefixo `AI.ChatPrefix`: trimado, `OrdinalIgnoreCase`, e precisa ser seguido de espaço ou
  terminar a mensagem (`/casa` não é comando). **Prefixo vazio = captura por prefixo
  desligada.** O destino é o primeiro nome de `AI.CompanionNames`.
- `AI.AddressByName` (default **true**) aceita o nome do companheiro no início
  (`Thorin, ...`, `Thorin: ...`, `Thorin ...`). O nome tem que ser **completo** e existir em
  `AI.CompanionNames`; `NameAddressing` prefere o nome mais longo. Sem lista, nada é capturado.
- Texto ignorado **não** é logado (apenas o comprimento), para não vazar conversa alheia.
- `ChatCapture` guarda `Last` (`TryGetLast`), dispara `Captured` e deduplica; `Plugin.Unload()`
  chama `ChatPatch.Unapply()` + `ChatCapture.Clear()`.
- **Resposta:** `ChatMenuCapture` guarda a instância do `ChatMenu` no prefix; o
  `CompanionChatReply` chama `_AddNewTextLine(nome, reply, cor, true, false)` por reflexão.
  É uma linha local (single-player); co-op fica para a fase de rede.
- **Sem busca de cena:** `FindObjectsByType(Type, ...)` aborta o load do plugin (dead-end #19);
  a instância vem só do prefix.
- Sem `Harmony.PatchAll()`/atributos: cada tipo é resolvido por
  `Type.GetType("<tipo>, Assembly-CSharp")` + varredura pontual de `Assembly.GetType` (evita o
  `AllTypes()` do `AccessTools.TypeByName`), método por `AccessTools.Method(..., new[] {
  typeof(string) })` (com fallback de varredura), `new Harmony(PluginInfo.Guid + ".chat")` e
  `harmony.Patch(...)` dentro de `try/catch` por alvo.
  Fire-verify: `[AIC][PATCH] applied chat hook(s): ...` + `... fired (first call ...)`.
- O log separa **`harmony-registered=True`** (o Harmony registrou) de **`fired`** (o alvo
  realmente executou); só o segundo prova o caminho. Um alvo não encontrado não impede os
  outros.

**Risco conhecido (não confirmado em jogo):** `SendPlayerMessage` é `async void` — o patch
acerta o *stub* que inicia a state machine `<SendPlayerMessage>d__83`. Se o AOT inlinar esse
stub no call site do botão de envio, o prefix **não dispara** (ver `dead-ends.md` §2). O log
de fire-verify é o que revela isso; plano B: `NetworkChatSession` ou UI própria. Não patchar
`MoveNext` da state machine sem deduplicar (o stub também dispara).

**Aviso benigno do HarmonyX:** ao aplicar o patch, o `HarmonyX` loga
`AccessTools.GetTypesFromAssembly: assembly UnityEngine.CoreModule ... ReflectionTypeLoadException`
(os stubs do Unity têm tipos que o runtime não materializa). O patch aplica e
`Harmony.HasAnyPatches` confirma (`verified=True`); o aviso não impede o disparo e não deve ser
confundido com falha. Se virar problema, isolar o `Harmony.Patch` num passo sem varredura.

## 3. Alternativas

| Caminho | Prós | Contras |
|---|---|---|
| `ChatMenu.SendPlayerMessage` | direto, público, assinatura simples | async void, possível inlining, depende do menu existir |
| `NetworkChatSession` | cobre o envio/recebimento real | classe grande, Photon, mais acoplamento |
| UI própria (input do mod) | controle total, não mexe no chat do jogo | mais UI, mais teclas, menos natural |
| Comando de console/dev | simples para teste | não é a UX final |

**Plano:** começamos pelo `SendPlayerMessage` em `feat/chat-hook` (implementado, §2). Se o
prefix não disparar em jogo, cair para o `NetworkChatSession` ou para a UI própria; a decisão
depende do resultado do roteiro da §6.

## 4. Diálogo do companheiro (fala visual)

`SSSGame.DialogueMenu`:

- `public static Dialogue Display(Character character, ref string message, float duration = 3f,
  bool addFirst = false, bool dontWaitForCurrentMessage = false)`
- `public static void ClearDialogues(Character character)`
- `public Dialogue RegisterDialogue(Character character, ref string message, float duration = 3f,
  bool addFirst = false, bool skipFirstMessage = false)`

`Dialogue` tem `message`, `duration`, `skip`.

**Uso proposto:** mostrar a resposta do companheiro no balão dele, com `duration` proporcional
ao texto, e tocar o TTS em paralelo. O `ref string` é do método nativo; **chamar** é ok,
**patchar** é arriscado (by-ref) — não patchar.

## 5. Entrada de texto e teclas

- O ASKA usa uGUI/TMP; o chat tem um `InputFieldHelper`.
- Se o mod adicionar um painel/campo de texto, as teclas do jogo podem vazar para o mundo
  (ex.: `I` abrir inventário, `M` mapa). Solução: verificar se o `EventSystem.current
  .currentSelectedGameObject` tem `TMP_InputField`/`InputField` e bloquear só os atalhos do mod
  enquanto o campo estiver em foco (padrão já usado no AI Companions do Valheim).
- O push-to-talk deve ser ignorado enquanto o chat/console/UI de texto estiver ativo.

## 6. Probe de captura (`feat/chat-hook`) — status

**Implementado (2026-10-28):** `Patches/ChatPatch.cs`, `Chat/ChatCommand.cs`,
`Chat/ChatAddressing.cs`, `Chat/ChatCapture.cs`; `Plugin.Load/Unload` aplicam e removem o
patch. Build: `dotnet build AICompanionsAska.sln -c Release` → **0 warnings, 0 errors**,
DLL deployada em `BepInEx\plugins\AI-Companions-Aska`.

**Confirmado em jogo (2026-10-28):** o ASKA carregou o plugin 0.2.0 e o log mostrou:
`[AIC][PATCH] ChatMenu._OnTextInputSubmit(string) fired`, seguido de
`[AIC][CHAT] addressed from ChatMenu._OnTextInputSubmit (5 chars): "teste"`. O
`NetworkChatSession.SendInGameChatMessage(string)` também disparou e a segunda captura foi
descartada como duplicata. Mensagem sem prefixo gerou `ignored ... (5 chars)` nos dois
caminhos. Nenhum erro no log. Evidência completa:
[`chat-hook-verification.md`](chat-hook-verification.md).

O teste anterior, com o alvo único `SendPlayerMessage`, **não gerou linha `[AIC][CHAT]`** — daí
o probe múltiplo. O caminho real do campo de texto é `_OnTextInputSubmit`; `SendPlayerMessage`
ficou como fallback.

**Validação fora do jogo (2026-10-28):** as classes puras (`ChatCommand`, `ChatAddressing`,
`ChatCapture`) foram compiladas num harness de console temporário (fora do repositório, com
`ConfigFile` real do BepInEx) e todos os casos abaixo passaram. O lado do jogo (resolução,
aplicação e disparo) foi confirmado no log acima.

### Roteiro de teste

1. Subir o jogo e conferir no `BepInEx\LogOutput.log` a versão `0.4.0` e as linhas:
   - `[AIC] ... ChatPrefix="/c" | AddressByName=True | CompanionNames="Thorin" | ReplyInChat=True | ...`
   - `[AIC][PATCH] applied chat hook(s): ... | harmony-registered=True | prefix="/c" | enabled=True`
   - `[AIC][PATCH] harmony-registered=True is NOT proof of firing; wait for a '... fired' line after a real chat submit.`
2. Abrir o chat (`Enter`), digitar `/c teste` e enviar.
   - Esperado: `[AIC][PATCH] ChatMenu._OnTextInputSubmit(string) fired (first call; string arg received).`
   - Esperado: `[AIC][CHAT] addressed to Thorin from ChatMenu._OnTextInputSubmit (5 chars): "teste"`
   - A mensagem original continua aparecendo no chat do jogo.
3. Enviar `Thorin, bom dia`.
   - Esperado: `[AIC][CHAT] addressed to Thorin from ...` + `[AIC][LLM] intent ...`
   - Esperado no chat: uma linha `Thorin: ...` (a resposta do LLM).
4. Enviar `teste` (sem prefixo/nome).
   - Esperado (com `VerboseLog=true`): `[AIC][CHAT] ignored (5 chars)` — **sem** o texto.
   - Com `VerboseLog=false`: nenhuma linha.
5. Enviar `/C Teste` (maiúscula) e ` /c  teste ` (espaços) → ambas devem capturar `Teste`/`teste`.
6. Enviar `/c` sozinho, `/casa` e `Thorinzinho, oi` → devem ser ignorados.
7. Conferir que **não** há exceção nem stack no log e que o chat não travou.
8. Opcional (co-op): mensagem de outro jogador **não** deve gerar linha de captura.

### Pendências desta branch

- [x] Confirmar o **disparo** do patch (feito em 2026-10-28; ver evidência).
- [x] `AddressByName` e `CompanionNames` implementados (v0.4.0); o harness cobre 8 casos de nome.
- [ ] **Linha visual `Thorin: ...` em um mundo** — o texto/resposta foi confirmado via self-test;
  a chamada real ao `_AddNewTextLine` precisa do teste na sessão normal do Steam.
- LLM/resposta/voz/UI: fases seguintes.
- Decidir se a mensagem `/c ...` deve continuar aparecendo no chat do jogo (hoje: sim).
- Co-op: `SendInGameChatMessage` pode receber mensagens de outros jogadores no host; validar
  antes de habilitar ordens em co-op.

