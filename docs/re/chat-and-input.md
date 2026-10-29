# RE — chat e entrada de texto

**Fonte:** `SSSGame.UI.ChatMenu`, `SSSGame.UI.ChatMessage`,
`SSSGame.Network.NetworkChatSession` (interop `Assembly-CSharp.dll`), extraído com
`ilspycmd` em **2026-10-28**. Superfície apenas; o comportamento é hipótese até o probe.

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

## 2. Captura proposta

```
Harmony Prefix/Postfix em ChatMenu.SendPlayerMessage(string __0)
  → se string.IsNullOrWhiteSpace → ignora
  → se não começa com prefixo (/c) e não começa com nome de companheiro → ignora
  → remove prefixo/nome, normaliza
  → enfileira para o CompanionChat (main thread)
  → (opcional) deixa a mensagem original seguir para o chat do jogo
```

**Decisão pendente:** manter a mensagem original no chat (transparência) ou suprimi-la
(`__runOriginal = false`) para não poluir o chat em co-op. Começar mantendo.

## 3. Alternativas

| Caminho | Prós | Contras |
|---|---|---|
| `ChatMenu.SendPlayerMessage` | direto, público, assinatura simples | async void, possível inlining, depende do menu existir |
| `NetworkChatSession` | cobre o envio/recebimento real | classe grande, Photon, mais acoplamento |
| UI própria (input do mod) | controle total, não mexe no chat do jogo | mais UI, mais teclas, menos natural |
| Comando de console/dev | simples para teste | não é a UX final |

**Plano:** começar pelo `SendPlayerMessage` em `feat/chat-hook`; se o patch não disparar,
cair para a UI própria ou para o `NetworkChatSession`.

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

## 6. Probe mínimo (branch `feat/chat-hook`)

1. Aplicar um postfix que só loga `[AIC][CHAT] SendPlayerMessage: "<texto>"`.
2. Abrir o chat no single-player, enviar `teste`.
3. Confirmar a linha no `LogOutput.log`.
4. Repetir com o chat fechado/`/c`.
5. Se não disparar: testar prefix em `ChatMenu.SendPlayerMessage`, depois
   `NetworkChatSession`, depois UI própria.

**Critério de aceite do probe:** uma linha de log por mensagem do jogador, sem duplicar, sem
travar, e com o texto correto (incluindo acentos).
