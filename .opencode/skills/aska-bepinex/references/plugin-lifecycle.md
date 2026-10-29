# Ciclo de vida do plugin (BepInEx 6 IL2CPP)

## Esqueleto

```csharp
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace AICompanionsAska;

[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Log = null!;
    internal static Plugin Instance = null!;

    public override void Load()
    {
        Instance = this;
        Log = base.Log;

        ModConfig.Bind(Config);
        Log.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} carregando...");
        Log.LogInfo($"Unity {Application.unityVersion} | BepInEx {BepInEx.Paths.BepInExRootPath}");
        Log.LogInfo($"{PluginInfo.Name} loaded successfully.");
    }

    public override bool Unload()
    {
        // parar threads/filas, liberar modelos, remover patches
        return true;
    }
}
```

`PluginInfo`:

```csharp
internal static class PluginInfo
{
    public const string Guid = "julianu37.AICompanionsAska";
    public const string Name = "AI Companions - ASKA";
    public const string Version = "0.1.0";
}
```

## Ordem de boot

1. BepInEx carrega o DLL e instancia o plugin.
2. `Load()` roda na **main thread**, antes do mundo.
3. O jogo entra no menu/mundo.
4. `Update()` dos MonoBehaviours do mod começa quando o objeto existe.

## Regras

- **`BasePlugin`**, não `BaseUnityPlugin` (BepInEx 6 IL2CPP).
- `Load()`: só config/log/registro de patches. Nada de HTTP, modelo, thread pesada ou
  `FindObjectsByType`.
- Modelos/LLM: lazy, em background, com retry.
- `Unload()`: idempotente; `Join` de workers; `Dispose` de nativos; remover Harmony.
- Um `GameObject` do mod deve ser `DontDestroyOnLoad` se precisar sobreviver a cenas.
- Para um `MonoBehaviour` próprio em IL2CPP:

```csharp
ClassInjector.RegisterTypeInIl2Cpp<MeuBehaviour>();
var go = new GameObject("AIC_MeuBehaviour");
UnityEngine.Object.DontDestroyOnLoad(go);
go.AddComponent<MeuBehaviour>();
```

- `ClassInjector` loga warnings de métodos com tipos genéricos/arrays/classes do mod; são
  **inofensivos** se você chama o método direto do C# gerenciado. Marcar helpers como `static`
  remove o warning (o injector só percorre métodos de instância).

## Harmony

- Prefira `harmony.Patch(...)` por alvo, com `try/catch`, em vez de `PatchAll()` cego.
- Resolva o tipo do alvo por uma lambda (`Func<Type>`) para que o `typeof` seja JITado dentro
  do try/catch.
- Logue uma linha por patch anexado.
- Fire-verifique o patch com log na primeira execução.

## Threads

- `Task`/`Thread` para HTTP/STT/TTS.
- `ConcurrentQueue<T>` + `SemaphoreSlim`.
- Drenar no `Update` com orçamento (ex.: 1 item/frame).
- Nunca tocar em `UnityEngine` fora da main thread.

## Falhas

- Se uma interop não resolver, logue o tipo/método e desative a feature; não deixe o plugin
  derrubar o jogo.
- Se o modelo de voz não existir, siga sem voz.
- Se o LLM estiver offline, avise uma vez e continue com ordens locais.
