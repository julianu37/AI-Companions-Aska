# Ciclo de vida e performance (Unity 6/IL2CPP)

## GameObjects do mod

```csharp
ClassInjector.RegisterTypeInIl2Cpp<AicBehaviour>();
var go = new GameObject("AIC_Behaviour");
UnityEngine.Object.DontDestroyOnLoad(go);
go.AddComponent<AicBehaviour>();
```

- `DontDestroyOnLoad` se precisar sobreviver a cenas.
- Um comportamento raiz; não crie um por villager.
- Destrua no `Unload`/saída do mundo.

## Prefabs

- Não edite prefabs do jogo.
- Se precisar de um visual, clone um objeto existente **em runtime** e ajuste; ou crie um
  simples (cubo/ícone).
- Não instancie prefab por frame.

## NavMesh

- O ASKA usa NavMesh/A*; se criar um NPC próprio, ele precisa de `NavMeshAgent` e uma área
  válida.
- Prefira usar o villager nativo (já tem navegação).

## Performance

- **Sem alloc por frame**: cache arrays/listas, evite LINQ/lambdas/`string.Format`.
- **Throttle**: status 2–5 Hz; scans 1 Hz; varredura de villager 4–15 Hz.
- **Não** `FindObjectsByType<T>()` por frame; capture por patch.
- **Não** `GetComponentsInChildren<T>` plural (missing); caminhe a hierarquia.
- **Patches por frame-por-entidade**: gate `Time.frameCount & N` na primeira linha.
- Se throttle lógico, acumule `Time.deltaTime` dos frames pulados.
- Meça com `Stopwatch` e logue só > 2 ms (`[AIC][PERF]`).

## Ciclo de vida

- `Awake`: referências locais; nada de rede.
- `Start`/`Spawned`: registrar; seguro para capturar.
- `Update`: lógica throttled.
- `OnDisable`/`OnDestroy`: cancelar corrotinas, parar áudio, remover listeners.
- `Unload` do plugin: parar threads, `Join`, `Dispose`, remover Harmony.

## Cenas/mundo

- Limpe caches de objetos do mundo ao sair (quit-to-menu/reload).
- Não guarde wrappers nativos entre mundos.
- Reassine/reconstrua o que depende da cena.

## Erros comuns

- `MissingMethodException` em `GetComponentsInChildren<T>(bool)`.
- `Handle is not initialized` ao tocar em objeto cedo demais.
- `VerificationException` ao interpolar `Vector3` no log.
- Crash nativo por wrapper velho.
