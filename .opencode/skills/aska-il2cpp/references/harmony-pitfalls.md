# HarmonyX no IL2CPP — armadilhas

## 1. AOT inlining

**Sintoma:** o patch nunca dispara, sem erro; o log de fire-verify não aparece.

**Causa:** o compilador AOT do IL2CPP inlina métodos pequenos/privados/single-caller.

**O que fazer:**

- Prefira métodos **virtuais/vtable** (`Init`, `Show`, `Use`) ou públicos multi-caller.
- Fire-verifique com log na primeira execução.
- Se não disparar, procure um irmão seguro com assinatura simples.

## 2. Parâmetros by-ref primitivos

**Sintoma:** `NullReferenceException` no trampoline; o corpo original não roda; o jogo pode
ficar inerte.

**Causa:** `float&`, `int&`, `bool&` etc. não marshalam no patch.

**O que fazer:** patche um método irmão sem by-ref e chame a lógica nativa depois.

## 3. Família de inventário (parâmetro)

**Sintoma:** crash nativo `coreclr.dll+0x1d1fdd`, sem exceção gerenciada.

**Causa:** `Item`, `ItemCollection`, `ItemEventContext` forçam class-init cedo no setup do patch.

**O que fazer:** captura por lifecycle sem parâmetros (`Awake`/`Spawned`) + polling.

## 4. Família de inventário (tipo declarante)

**Sintoma:** `NullReferenceException` no trampoline logo após o chainloader; o `try/catch` do
patch não pega.

**Causa:** o tipo declarante é da família (`Blueprint : Item`, etc.).

**O que fazer:** não patche; use outro ponto.

## 5. Lifecycle de `Interaction`/prefab

**Sintoma:** `Handle is not initialized`; o jogo não termina de abrir.

**Causa:** `Initialize`/lifecycle roda antes do GC handle.

**O que fazer:** `Spawned()`/`Update()` posterior; não patche o init.

## 6. State-sync do Fusion

**Sintoma:** o jogo congela no load, sem exceção.

**Causa:** patch em `CopyBackingFieldsToState`/`CopyStateToBackingFields`.

**O que fazer:** capture em `Awake`/`Spawned`; nunca patche o state-sync.

## 7. `PatchAll()` e atributos

**Sintoma:** `Fatal error. Internal CLR error. (0x80131506)`.

**Causa:** `PatchAll()` materializa atributos e resolve tipos do jogo no parser.

**O que fazer:** sem `PatchAll()`, sem `[HarmonyPatch(typeof(...))]`; `harmony.Patch()` por alvo
com `try/catch`, `typeof` em lambda.

## 8. Tipo derivado vs base

**Sintoma:** o patch no método base não vê a chamada.

**Causa:** `PlayerCharacter`/`Villager` sobrescrevem o método.

**O que fazer:** patche o tipo **mais derivado** (ou ambos, se necessário).

## 9. `Action` do jogo

**Sintoma:** não dá para assinar; `Il2CppSystem.Action` só aceita `IntPtr`.

**O que fazer:** polling/`Update`; capture por patch.

## 10. `FindObjectsByType<T>()` e plurais

**Sintoma:** `MissingMethodException` no trampoline; flood no log.

**O que fazer:** capture por `__instance`; use `FindAnyObjectByType<T>()` (singular) com
throttle; para filhos, caminhe a hierarquia com `GetComponent<T>()`.

## 11. Casts que mentem

**Sintoma:** `as Derived` retorna `null` mesmo com o objeto nativo sendo `Derived`.

**Causa:** o wrapper foi materializado como tipo base.

**O que fazer:** confira a classe nativa e reconstrua com `new Derived(IntPtr)`.

## 12. Structs no log

**Sintoma:** `VerificationException` no site do log.

**Causa:** interpolação de `Vector3`/`Vector4` no handler do BepInEx.

**O que fazer:** `.ToString()` ou wrapper `LogInfo(string)`.

## 13. Conflito com outros mods

- `Character.Spawned` pode já estar patchado pelo Aska Plus; um segundo patch pode matar o
  processo no load.
- Prefira `Character.Start`/`Despawned` ou busca sob demanda.
- Documente a combinação testada.
