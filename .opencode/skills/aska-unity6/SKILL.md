---
name: Unity 6 no ASKA
description: Padrões de Unity 6 para o mod — uGUI/TMP, painéis, HUD, áudio 3D, microfone, animação, NavMesh, prefabs e performance em IL2CPP. Use ao construir UI, áudio ou objetos de cena.
---

# Unity 6 no ASKA

O ASKA roda em **Unity 6000.3.12f1** com IL2CPP. O mod pode criar GameObjects, componentes
uGUI/TMP, `AudioSource`, capturar microfone e usar NavMesh — mas não pode editar assets/prefabs
do jogo.

## Quando usar

Sempre que o trabalho envolver: painel/HUD, balão de fala, botões/sliders/input, áudio 3D,
microfone, animação, NavMesh, prefab em runtime, ciclo de vida de `MonoBehaviour`.

## Regras

- **Main thread:** componentes Unity só na main thread; áudio/IO/rede em background.
- **Sem alloc por frame:** cache buffers, evite LINQ/lambdas em `Update`.
- **Sem `FindObjectsByType<T>()` por frame**: capture por patch/evento ou throttle.
- **Input:** enquanto um campo de texto do mod/jogo estiver em foco, bloqueie atalhos e
  push-to-talk; use `EventSystem.current.currentSelectedGameObject`.
- **UI do jogo:** não edite prefabs; clone/crie os seus. Respeite a fonte/TMP do ASKA.
- **Áudio:** `AudioSource` 3D no companheiro; `reverbZoneMix = 0` se o eco do jogo incomodar;
  destrua `AudioClip` ao terminar; uma fala por vez.
- **Microfone:** `Microphone.Start` 16 kHz mono; trim de silêncio; WAV em memória; não gravar
  com UI/chat ativo.
- **Ciclo de vida:** feche o painel em Esc/X/morte/saída do mundo; cancele corrotinas no
  `OnDisable`/`OnDestroy`.

## Referências

- `references/ui-patterns.md` — painel, abas, input, TMP, foco.
- `references/audio-microphone.md` — captura, reprodução 3D, filas.
- `references/lifecycle-performance.md` — prefabs, NavMesh, alloc, throttling.

## Fluxo

1. Leia `docs/architecture.md` (UI/voz) e a skill `aska-game-systems`.
2. Verifique se o jogo já tem o componente (ex.: `DialogueMenu`) antes de criar um paralelo.
3. Implemente com fallback se o TMP/fonte não existir.
4. Teste em 16:9 e numa resolução menor; confira foco de texto e áudio.
