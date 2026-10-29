# Padrões de interop

## Capturar uma instância sem `FindObjectsByType`

```csharp
[HarmonyPatch(typeof(SSSGame.Villager), nameof(SSSGame.Villager.Spawned))]
internal static class VillagerSpawnedPatch
{
    private static void Postfix(SSSGame.Villager __instance)
    {
        // guarde em uma lista estática; não toque em rede aqui
        VillagerRegistry.Add(__instance);
    }
}
```

- `Spawned`/`Despawned` são lifecycle de `NetworkBehaviour` e costumam ser seguros.
- Evite `Initialize` de `Interaction` (GC handle cedo).
- Não assine `Action`; use `Update` + polling.

## Ler uma lista do interop

```csharp
var list = party._partyMembers; // Il2CppSystem.Collections.Generic.List<Villager>
for (int i = 0; i < list.Count; i++)
{
    var v = list[i];
    // ...
}
```

- `Count` + indexador; evite LINQ (alloc e genéricos).
- Não guarde wrappers de objetos por mundo em cache estático sem limpar na saída.

## Cast seguro

```csharp
// Ruim quando o wrapper foi materializado como base:
var derived = obj as DerivedType; // pode ser null

// Melhor: confira a classe nativa
var cls = IL2CPP.il2cpp_object_get_class(obj.Pointer);
string name = IL2CPP.il2cpp_class_get_name(cls);

// Ou reconstrua o wrapper derivado:
var derived = new DerivedType(obj.Pointer);
```

## Chamar um método nativo com `ref string`

```csharp
string msg = "Olá!";
SSSGame.DialogueMenu.Display(villager, ref msg, 3f, false, false);
```

- **Chamar** é ok; **patchar** método com `ref` é arriscado.
- `DialogueMenu.Display` pode retornar `Dialogue` com `duration`/`message`.

## `ClassInjector` (MonoBehaviour próprio)

```csharp
ClassInjector.RegisterTypeInIl2Cpp<MeuBehaviour>();
var go = new GameObject("AIC_MeuBehaviour");
UnityEngine.Object.DontDestroyOnLoad(go);
go.AddComponent<MeuBehaviour>();
```

- Warnings de métodos com `List<T>`/arrays/classes do mod são inofensivos.
- Marcar helpers como `static` remove o warning (o injector só percorre instância).

## Strings

- `string` gerenciada entra/sai do interop normalmente.
- Evite `StringBuilder`/`Span<T>` em assinaturas de patch; use `string`.
- Acentuação: o jogo usa UTF-8/UTF-16; teste com PT-BR.

## Autoridade

```csharp
if (!villager.HasAuthority) return;
if (!runner.IsServer && !runner.IsSharedModeMasterClient) return;
```

- `HasAuthority` é do `Character`/`NetworkBehaviour`.
- `Runner.IsServer`/`IsSharedModeMasterClient` para decidir quem executa.

## Logging de structs

```csharp
Log.LogInfo($"[AIC] pos={pos.ToString()}"); // não interpole Vector3 direto
```

## `MonoBehaviour` e Update

- Um `Update` por comportamento, não um por objeto.
- Gate de frame para trabalho pesado:

```csharp
if ((Time.frameCount & 3) != 0) return; // ~15 Hz a 60 fps
```

- Se throttle lógico, acumule `Time.deltaTime` dos frames pulados.
