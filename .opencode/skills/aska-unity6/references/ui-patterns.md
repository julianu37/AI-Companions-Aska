# UI — padrões uGUI/TMP

## Estrutura do painel

```
Canvas (Screen Space - Overlay, sortingOrder alto)
  └── Panel (Image, RectTransform)
        ├── Header (TMP_Text + botão X)
        ├── Tabs (botões)
        ├── Content (ScrollRect + Mask)
        └── Footer (status)
```

- Canvas próprio com `GraphicRaycaster`; `overrideSorting`/`sortingOrder` acima do HUD do jogo.
- Painel de madeira/estilo do ASKA se possível; caso contrário, um tema escuro simples.
- Ancorar com `RectTransform`; testar 16:9, 16:10 e 21:9.
- Não cubra o centro da tela sem necessidade.

## Input

- `TMP_InputField` precisa de `textComponent`, `textViewport` e `placeholder` **antes** de
  ativar o GameObject (o `OnEnable` cria o caret).
- Use `Overflow` + `PreserveWhitespaceNoWrap` para o texto não truncar.
- `richText = false` se o jogador digitar tags.
- `onSubmit` para Enter.
- Enquanto um campo estiver em foco:
  - bloqueie atalhos do mod e push-to-talk;
  - opcionalmente bloqueie teclas do jogo que abrem menus (ver dead-ends).

```csharp
bool TextFocused()
{
    var go = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
    if (go == null) return false;
    return go.GetComponent<TMPro.TMP_InputField>() != null
        || go.GetComponent<UnityEngine.UI.InputField>() != null;
}
```

## Abrir/fechar

- Abrir: `SetActive(true)` + cursor livre + bloquear input do player.
- Fechar: Esc, X, morte, saída do mundo, abertura de menu conflitante.
- Não destruir/recriar o painel a cada abertura se ele for caro; construir uma vez e
  mostrar/esconder.
- Ao fechar, devolver o cursor/input ao jogo (o próprio jogo costuma cuidar; não force).

## Textos

- Tudo via chaves de tradução (PT-BR/EN).
- Fonte do jogo/TMP; se não achar, use a default do TMP.
- Evite emojis/ícones que a fonte não tenha.
- Números/latência formatados com cultura invariante.

## Listas/abas

- Aba por companheiro; nome como título.
- Dropdown manual (botão + lista) se o `TMP_Dropdown` exigir template.
- Scroll com `ScrollRect` + `Mask`/`RectMask2D`.
- Não recriar a lista por frame; reconstruir só quando o estado mudar.

## Acessibilidade/UX

- Botões grandes o suficiente para gamepad.
- Feedback visual de hover/seleção.
- Não usar só cor para indicar estado (texto também).
- Tooltip/descrição curta para configs.

## Performance

- Sem `LayoutRebuilder.ForceRebuildLayoutImmediate` por frame.
- Sem alocação de strings por frame; atualize status 2–5 Hz.
- `CanvasGroup` para esconder sem desativar o objeto, se precisar de estado.
