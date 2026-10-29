# RE — dead ends e armadilhas do IL2CPP

Este arquivo registra **caminhos que já queimaram tempo** e as armadilhas do stack
BepInEx 6 + Il2CppInterop + ASKA. A maioria foi documentada publicamente pelo projeto
[askamods](https://github.com/simpsonbm1/askamods); **confirmar na nossa build** antes de
tratar como fato local. O status de cada item diz o que já foi verificado.

> Regra: se um agente for tentar algo listado aqui, primeiro leia o item, depois proponha um
> probe que **não** repita o caminho que falhou.

---

## 1. A superfície do interop não é o jogo

**Status:** confirmado pela natureza do Il2CppInterop + inspeção local.

As DLLs em `BepInEx\interop` são wrappers: expõem tipos/assinaturas e chamam o nativo por
`il2cpp_runtime_invoke`. Não há corpo C# nem grafo de chamadas confiável.

- Use `ilspycmd -l/-t` para **superfície**.
- Use **Cpp2IL** (metadata v39) ou **Ghidra/IDA** para lógica nativa.
- Use **probe em runtime** (log/patch) para comportamento.
- Não gaste um passe tentando “quem chama X” no Cecil; a resposta não está lá.

## 2. AOT inlining mata patches silenciosamente

**Status:** reportado por terceiros; **verificar em toda patch nova**.

O compilador AOT do IL2CPP inlina métodos pequenos/privados/single-caller. Um patch Harmony
nesses métodos **nunca dispara**, sem erro.

- Prefira métodos **virtuais/vtable** (`Init`, `Show`, `Use`) ou públicos multi-caller.
- **Sempre** fire-verifique com uma linha de log na primeira execução.
- Se não disparar, procure um método irmão com assinatura segura.

## 3. Parâmetros by-ref primitivos quebram o trampoline

**Status:** reportado por terceiros; **não repetir sem probe**.

Não patchar métodos com `ref/out` de primitivo (`float&`, `int&`, `bool&`). O marshalling do
trampoline lança `NullReferenceException` **fora** do seu `try/catch`, e o corpo original não
roda — o jogo pode ficar inerte.

- Patch um método irmão com assinatura simples (ex.: `Update()`), e chame a lógica nativa
  depois.
- `DialogueMenu.Display(Character, ref string, ...)` é **chamar**, não patchar.

## 4. Família de inventário: parâmetro e tipo declarante

**Status:** reportado por terceiros (reproduções múltiplas); **tratar como proibido**.

- **Parâmetros** `Item`, `ItemCollection`, `ItemEventContext`: o Harmony resolve os tipos no
  setup do patch, força `il2cpp` class-init cedo e pode matar o processo
  (`coreclr.dll+0x1d1fdd`, sem exceção gerenciada).
- **Tipo declarante** na família de inventário (ex.: `SandSailorStudio.Inventory.Blueprint`)
  também é perigoso mesmo com parâmetros simples: pode lançar `NullReferenceException` no
  trampoline logo após o chainloader.
- Use captura por lifecycle sem parâmetros (`Awake`/`Spawned`) + polling; nunca detour de
  método com esses tipos.

## 5. Não patchar lifecycle de `Interaction`/prefab cedo

**Status:** reportado por terceiros.

`Initialize`/lifecycle de `MonoBehaviour`-based `Interaction` pode rodar durante o init do
prefab, antes do GC handle do objeto estar pronto; o trampoline lança “Handle is not
initialized” e o jogo não termina de abrir.

- Prefira `Spawned()` de `NetworkBehaviour` ou um `Update()` posterior.
- Se precisar capturar, use um método de lifecycle tardio e guarde a instância.

## 6. Não patchar `CopyBackingFieldsToState`/`CopyStateToBackingFields`

**Status:** reportado por terceiros (hang no load, sem exceção).

São os métodos de sincronização do Fusion, rodando no tick de rede. Patch neles = jogo
congelado no carregamento. Capture instâncias em `Awake`/`Spawned` e registre numa lista.

## 7. `Character.Spawned` pode conflitar com o Aska Plus

**Status:** reportado por terceiros (2026-10-17); **verificar a versão do Aska Plus**.

O Aska Plus já postfixa `Character.Spawned`. Um segundo patch no mesmo método pode matar o
processo no load de `PlayerCharacter` (`Fatal error. Internal CLR error. 0x80131506`).

- Alternativa: rastrear por `Character.Start`/`Despawned`, ou achar o player por nome de
  classe nativa + `HasAuthority` sob demanda.
- Se for inevitável, documente a combinação e teste com/sem o Aska Plus.

## 8. `Harmony.PatchAll()` pode transformar um erro de tipo em morte do processo

**Status:** reportado por terceiros.

`PatchAll()` materializa atributos de todos os tipos; um `[HarmonyPatch(typeof(GameType), ...)]`
força a resolução do tipo dentro do parser de atributos. Se falhar, o processo morre com
`0x80131506` sem exceção gerenciada.

- Padrão mais legível: **sem** `PatchAll()`, **sem** atributos com `typeof` de tipo do jogo;
  chamar `harmony.Patch()` por alvo dentro de `try/catch`, resolvendo o tipo por uma lambda.
- Logar uma linha por patch anexado.
- Não é cura universal, mas produz um log que diz onde morreu.

## 9. Eventos `Action` do jogo não são assináveis

**Status:** reportado por terceiros; consistente com o interop.

`Il2CppSystem.Action(methodRef)` só aceita `IntPtr` nesta versão; não dá para assinar um
lambda gerenciado em `_onNewDay`, `OnFullyHarvested`, `OnVillagerAdded`, etc.

- Use um `MonoBehaviour` registrado + `Update()` com polling, ou capture por patch.
- Para a party, observar `_partyMembers.Count`/estado em vez de `OnVillagerAdded`.

## 10. `FindObjectsByType<T>()` e plurais genéricos

**Status:** reportado por terceiros.

- `FindObjectsByType<T>()` (genérico) lança `MissingMethodException` no trampoline; chamado no
  `Update`, flooda o log.
- `GetComponentsInChildren<T>(bool)` (plural) também falta.
- `FindAnyObjectByType<T>()` (singular) funciona.
- Alternativa: capturar por patch/`__instance`; para coletar filhos, caminhar a hierarquia com
  `GetComponent<T>()` por objeto.

## 11. Casts gerenciados mentem para objetos materizados como tipo base

**Status:** reportado por terceiros.

`obj as Derived` retorna `null` quando o wrapper foi materializado como `Base` (listas,
parâmetros base), mesmo que o objeto nativo seja `Derived`.

- Identifique pelo `name`/`id` do asset ou pelo nome da classe nativa
  (`IL2CPP.il2cpp_object_get_class` + `il2cpp_class_get_name`).
- Reconstrua o wrapper derivado com `new Derived(IntPtr)`.

## 12. Logging de structs do Unity

**Status:** reportado por terceiros.

Interpolar `Vector3`/`Vector4` direto em `Logger.LogInfo($"...")` pode lançar
`VerificationException` no site do log (o handler de string interpolada rejeita o tipo).

- `.ToString()` antes, ou use um wrapper `LogInfo(string)`.

## 13. Teclas do jogo enquanto digita

**Status:** reportado por terceiros; relevante para o painel do mod.

Atalhos globais (`Input.GetKeyDown`) disparam enquanto o jogador digita em um campo de texto
do jogo (rename, chat, busca). Se o mod tiver teclas, gate por
`EventSystem.current.currentSelectedGameObject` com `TMP_InputField`/`InputField`.

- O chat do ASKA tem `IsTextInputActive`; usar isso também.
- Push-to-talk deve ser ignorado com chat/console/UI de texto ativo.

## 14. Config do BepInEx não recarrega sozinha

**Status:** reportado por terceiros; comportamento padrão do BepInEx.

Editar o `.cfg` com o jogo aberto não muda o `ConfigEntry.Value` em memória. Para hot-reload,
chamar `Config.Reload()` periodicamente (throttle, ex. 5–30 s).

- Leia a config **depois** do reload e antes do gate de `Enabled`.
- Não faça isso por frame.

## 15. Custo por frame

**Status:** reportado por terceiros (medições em ASKA modado).

- Vários mods com trabalho por frame somam dezenas de ms e derrubam o FPS.
- `FindAnyObjectByType<T>()` por frame é uma varredura de cena; throttle 1 Hz.
- Loops em todos os villagers por frame: gate 4–15 Hz.
- Patch em método por-frame-por-entidade: coloque `if ((Time.frameCount & N) != 0) return;`
  como primeira linha.
- Se throttle lógico, acumule `Time.deltaTime` dos frames pulados (não descarte o tempo).

## 16. Cache de wrappers por mundo

**Status:** reportado por terceiros.

Nunca guarde wrappers de objetos nativos por sessão/mundo em caches estáticos sem limpar na
saída do mundo. Quit-to-menu e reload do mesmo mundo podem manter o mesmo `ActiveSessionID`;
um wrapper velho pode causar AV nativo sem exceção gerenciada.

- Detecte saída de mundo e limpe o estado.
- Não use `UniqueId` de chunk como chave global; use posição do mundo.

## 17. Metadata v39 e ferramentas

**Status:** local (metadata v39 confirmada).

- `global-metadata.dat` tem magic `AF 1B B1 FA` e versão `0x27` (39).
- O BepInEx be.791 suporta Unity 6/metadata recente; o Il2CppDumper antigo pode não suportar.
- Prefira **Cpp2IL** atualizado ou o dump do próprio BepInEx/Il2CppInterop.
- Se o dump falhar, use análise nativa + `MethodAddressToToken.db`.

## 18. Crash nativo sem exceção gerenciada

**Status:** procedimento.

Sintomas: processo morre, `LogOutput.log` termina sem stack gerenciada, WER aponta
`coreclr.dll+0x1d1fdd` ou similar.

1. Pegue o offset do WER/`.dmp`.
2. Mapeie o endereço para um método com Cpp2IL (ou `dumpbin`/Ghidra).
3. Verifique se o método é de inventário, lifecycle de Interaction, state-sync do Fusion ou
   um wrapper velho.
4. Remova o patch/caminho e registre aqui.

## 19. `FindObjectsByType(Type, ...)` aborta o load do plugin

**Status:** local (2026-10-28, ASKA 0.4/Unity 6000.3.12f1).

**Sintoma:**

```text
[Error : BepInEx] Error loading [AI Companions - ASKA 0.3.0]:
System.MissingMethodException: Method not found:
'UnityEngine.Object[] UnityEngine.Object.FindObjectsByType(System.Type,
 UnityEngine.FindObjectsInactive, UnityEngine.FindObjectsSortMode)'.
```

O plugin **inteiro** não carrega (nem o chat hook, nem o brain).

**Causa:** além do genérico `FindObjectsByType<T>()` (item 10), o overload
**não-genérico** também falta no trampoline do IL2CPP. O `MissingMethodException`
dispara no **JIT do método que contém a chamada**, então um `try/catch` no call
site não intercepta.

**Alternativa:** capture a instância no prefix Harmony (`__instance`). Para o
`ChatMenu`, `_OnTextInputSubmit`/`SendPlayerMessage` já entregam a instância;
não faça varredura de cena no boot. Se precisar de um objeto, obtenha-o de um
patch/lifecycle, não de `FindObjects*`.

## 20. Como adicionar um dead end

- Título curto e específico.
- **Status:** `local (verificado em jogo em AAAA-MM-DD)` ou `reportado por terceiros`.
- Sintoma exato (log/mensagem).
- Causa (se conhecida) e o que **não** fazer.
- Alternativa que funciona.
- Link para o log/issue/handoff.

## 21. Um método do interop pode ser um stub compartilhado por dezenas de métodos

**Status:** local (verificado em 2026-10-29, ASKA 0.4 / metadata v39).

**Sintoma:** patch Harmony "em `X.Y`" dispara para objetos que não são `X`, e o jogo morre com
AV nativo em locais sem relação (`WeatherManager`, `Object.op_Equality`,
`VillagerSelectionManager`) sem stack gerenciada.

**Causa:** o ponteiro nativo de vários métodos aponta para o **mesmo** endereço. Medido nesta
build com `BepInEx\interop\MethodAddressToToken.db`:

```text
SSSGame.PlayerHouse::_OnActivate          -> 0x8009B0  (29 aliases, thunk de 7 bytes)
SSSGame.PlayerHouse::OnDestroy            -> 0xA765A0  (26 aliases)
SSSGame.PlayerHouse::get_OwnerStructure   -> 0x6B1D70  (403 aliases)
SSSGame.PlayerParty::Spawned / Start      -> únicos (1 alias) — seguros
SSSGame.UI.ChatMenu::_OnTextInputSubmit   -> único — seguro
```

Harmony/Il2CppInterop patcha **por endereço**: o detour entra em todos os aliases.

**Não fazer:** patch de mutação em método com `aliases > 1`; nem assumir que o nome do método
implica corpo próprio.

**Alternativa:** antes de qualquer patch, rode
`dotnet fsi docs/re/tools/rva-to-method.fsx 0x<rva>` e confira `aliases`. Se `> 1`, procure um
método irmão único ou troque a abordagem (observar estado em vez de hookar).

## 22. `__instance is T` não é guarda de tipo em patch IL2CPP

**Status:** local (verificado no fonte do Il2CppInterop + crash de 2026-10-29).

**Sintoma:** o postfix loga `... PlayerHouse ...` para objetos que não são `PlayerHouse` e/ou
escrever um campo no `__instance` corrompe campos de outros componentes (crash posterior em
offset alheio).

**Causa:** `Il2CppDetourMethodPatcher` converte o `this` nativo com
`Il2CppObjectPool.Get<T>(ptr)`, onde `T` é o tipo **declarado** do parâmetro do patch, e
`Get<T>` não valida a classe real do ponteiro. Logo, `__instance` é um wrapper do tipo do alvo
do patch, mesmo que o objeto nativo seja de outra classe — e `house.campo = valor` escreve em
`ptr + offset(campo)` **naquele** objeto.

**Não fazer:** usar `is`/cast para decidir se pode mutar; nem mutar campos a partir de
`__instance` de um método compartilhado.

**Alternativa:** comparar a classe nativa de verdade
(`IL2CPP.il2cpp_object_get_class(ptr)` + `il2cpp_class_get_name`) e, de preferência, obter a
instância por API tipada (`FindObjectsOfType<T>()`, `__instance` de método *unique*) em vez de
herdar o wrapper do patch. Ver `docs/re/party-gate-crash.md` §3.
