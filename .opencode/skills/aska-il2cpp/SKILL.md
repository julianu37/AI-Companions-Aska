---
name: ASKA IL2CPP e HarmonyX
description: Padrões e armadilhas do Il2CppInterop + HarmonyX no ASKA — patches seguros, AOT inlining, by-ref, família de inventário, lifecycle, eventos, casts e crash nativo. Use ao escrever qualquer patch ou wrapper de interop.
---

# ASKA — IL2CPP e HarmonyX

O código do jogo é **nativo** (`GameAssembly.dll`). As assemblies em `BepInEx\interop` são
wrappers gerados: expõem tipos/assinaturas e chamam o nativo por `il2cpp_runtime_invoke`.
Patches HarmonyX funcionam, mas têm armadilhas específicas do IL2CPP.

## Quando usar

Sempre que o trabalho envolver: Harmony patch, `Il2CppObjectBase`, `TryCast`, captura de
instância, `ClassInjector`, eventos do jogo, crash nativo, `MissingMethodException`, ou
qualquer interação com `SSSGame.*`/`SandSailorStudio.*`.

## Regras de ouro

1. **AOT inlining**: método pequeno/privado/single-caller pode ser inlinado; o patch nunca
   dispara. Prefira virtual/público multi-caller e **fire-verifique** com log.
2. **Nunca patchar** métodos com `ref/out` de primitivo (`float&`, `int&`, `bool&`).
3. **Nunca patchar** métodos com tipos de inventário (`Item`, `ItemCollection`,
   `ItemEventContext`) nem métodos cujo **tipo declarante** seja da família de inventário.
4. **Nunca patchar** `Initialize`/lifecycle de `Interaction` durante o init do prefab.
5. **Nunca patchar** `CopyBackingFieldsToState`/`CopyStateToBackingFields` de `NetworkBehaviour`.
6. **Não assinar** eventos `Action` do jogo; use polling/`Update`.
7. **Não usar** `FindObjectsByType<T>()` genérico; capture por patch ou use
   `FindAnyObjectByType<T>()` com throttle.
8. **Casts** `as/is` podem mentir para wrappers materializados como tipo base; confira a classe
   nativa (`IL2CPP.il2cpp_object_get_class` + `il2cpp_class_get_name`).
9. **Logs** de structs do Unity via `.ToString()`.
10. **Autoridade**: `HasAuthority`/`Runner.IsServer` antes de escrever estado de rede.
11. **Confira `aliases` antes de qualquer patch**: vários métodos compartilham o mesmo endereço
    nativo (thunk). `dotnet fsi docs/re/tools/rva-to-method.fsx 0x<rva>` — se `aliases > 1`, o
    patch entra em todos e você vai escrever no objeto errado. Ex.: `PlayerHouse::_OnActivate`
    tem **29** aliases (causou `0xC0000005` em `WeatherManager`).
12. **`is T`/`as T` não são guarda de tipo** em prefix/postfix: o detour materializa `this` como
    o tipo patcheado sem validar a classe nativa.

## Referências

- `references/harmony-pitfalls.md` — lista detalhada com sintoma/causa/alternativa (#14 thunk
  compartilhado, #15 `is T` que mente).
- `references/interop-patterns.md` — captura de instância, polling, casts, listas, strings.
- `references/native-crashes.md` — WER, Cpp2IL, `coreclr.dll+0x1d1fdd`, o que fazer.
- Ferramenta: `docs/re/tools/rva-to-method.fsx` (RVA → método + aliases).

## Fluxo

1. Identifique o método/campo na **superfície** (`ilspycmd`).
2. Escolha o ponto de patch menos arriscado (virtual/público, sem by-ref/inventário).
3. Escreva um prefix/postfix mínimo; nada de reimplementar o método.
4. Fire-verifique com `[AIC][PATCH] <método> fired` na primeira execução.
5. Se falhar, **não** insista no mesmo método: procure um irmão seguro.
6. Registre o resultado em `docs/re/dead-ends.md` se for uma armadilha nova.
