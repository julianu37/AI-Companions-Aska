# RE — gate da party, `allowPartyManagement` e o crash nativo do `DebugUnlockParty`

**Data:** 2026-10-29 · **Branch:** `feat/recruit-follow` · **ASKA 0.4 / Unity 6000.3.12f1**
**Artefatos desta máquina:**

| Item | Valor |
|---|---|
| `GameAssembly.dll` | 106.327.040 bytes, 2026-10-28 19:14:45, base no dump `0x7FF93D080000` |
| Plugin testado | `AICompanionsAska.dll` 74.240 bytes, build/deploy 18:27:54, SHA-256 `7037D65C…571D7` |
| Dumps | `%LOCALAPPDATA%\CrashDumps\Aska.exe.{14204,17876,1516,22572}.dmp` (18:19, 18:21, 18:24, 18:28) |
| Log | `BepInEx\LogOutput.log` (16 linhas `[PARTY]`, termina 18:28:40) e `Player.log` (35 linhas, termina em `[Weather System] Weather Forcast Reset`) |

**Resumo em uma frase:** os RVAs do crash **não** estão em `PlayerHouse`, na UI da party nem em
`PlayerParty` — estão em `WeatherManager`, `UnityEngine.Object.op_Equality` e
`VillagerSelectionManager`; e a causa provável é o patch em `PlayerHouse._OnActivate`, que
**não é um método real** e sim um stub compartilhado por 29 métodos, no qual o mod escreve um
bool em objetos de outros tipos.

---

## 1. Mapeamento RVA → método (verificado)

Fonte: `BepInEx\interop\MethodAddressToToken.db` (magic `UMTM`, v1), gerado pelo Il2CppInterop.
O arquivo mapeia **RVA do ponteiro de método** (relativo à imagem — mesmo espaço do offset do
WER) → token da assembly interop. Reproduza com:

```powershell
dotnet fsi docs/re/tools/rva-to-method.fsx 0xF72955 0x4525346 0x112B70D
```

| WER (horário) | RVA | Método | Offset | Tamanho | Aliases |
|---|---|---|---|---|---|
| 18:28:41 e 18:24:12 | `0xF72955` | `SSSGame.WeatherManager::Instance_OnWeatherUpdate` | `+0xAA5` | `0xAF0` | 1 |
| 18:21:43 | `0x4525346` | `UnityEngine.Object::op_Equality` | `+0xD6` | `0x100` | 1 |
| 18:19:00 | `0x112B70D` | `SSSGame.UI.VillagerSelectionManager::GetRandomPearks` | `+0xFD` | `0x280` | 1 |

Confirmações:
- O evento WER (`Application`/id 1000) dá os três offsets como "deslocamento de falha" em
  `GameAssembly.dll`, todos `0xC0000005`; o dump `22572` traz `ExceptionAddress =
  0x7FF93DFF2955` = `GameAssembly + 0xF72955` e `tid=17828` (188 threads, 350 módulos).
- O menor RVA do DB é `0x699060`, dentro da seção `il2cpp` (`0x699000`) — e todos os RVAs do
  WER caem na mesma seção, coerente com "offset de imagem".
- **Validação cruzada:** dentro de `Instance_OnWeatherUpdate` há um `call 0x4525400`
  (`0xF72929`) e o DB diz que `0x4525400` é `UnityEngine.Object::op_Inequality`. O mapa está
  alinhado com o código real.
- Os três métodos têm **1 alias** e nenhum outro método começa dentro do span → o mapeamento
  não é ambíguo (ver §3 para o caso em que é).

Decoder usado: Iced 1.21 (`BepInEx\core\Iced.dll`) + tabela de seções do PE (`.text@0x1000`,
`il2cpp@0x699000`, `.rdata@0x49B9000`, `.pdata@0x62CC000`, …).

## 2. O que a instrução de falha mostra (verificado)

| RVA | Instrução | Leitura |
|---|---|---|
| `0xF72955` | `mov r8,[rax+0x28]` (antes: `test rax,rax` / `je`) | `rax = [this+0x30]`; **não é nulo**, mas o ponteiro é inválido → AV. Logo depois o código faz `call qword ptr [rax+0x18]` (chamada virtual) |
| `0x4525346` | `cmp qword ptr [rbx+0x10],0` | `op_Equality` lendo o ponteiro nativo de um `UnityEngine.Object` **não nulo e inválido** |
| `0x112B70D` | `cmp r8d,[rdx+0x18]` com `rdx = [[this+0x30]+0x18]` | mesma assinatura: campo em `this+0x30` aponta para memória inválida |

Nos três casos o padrão é **ponteiro não-nulo e inválido** — não é null-deref "limpo", é campo
de objeto corrompido. Dois dos três leem o **primeiro campo próprio** do componente
(`this+0x30`), exatamente o tipo de campo que um `bool` escrito no offset errado transforma em
`0x…01`.

> Atribuição: **nenhum** dos três RVAs está em `PlayerHouse`, na UI da party, em `PlayerParty`
> ou em `PlayerPartyWidget`. O crash é *depois*, em consumo de objeto corrompido.

## 3. Por que o patch em `PlayerHouse._OnActivate` corrompe memória (verificado + inferido)

O build que crashou às 18:28 (deploy 18:27:54) já tinha o guard de prefab
(`gameObject.scene.IsValid()`) e patchava **apenas** `_OnActivate` — o guard não resolve o
problema, porque o problema não é prefab: é tipo.

### 3.1 `_OnActivate` é um stub compartilhado por 29 métodos (verificado)

`SSSGame.PlayerHouse::_OnActivate` → `0x8009B0`, e **29 métodos** apontam para esse endereço:

```
SSSGame.PlayerHouse::_OnActivate          SSSGame.WeatherManager::Awake
SSSGame.GameModeManager::Awake            SSSGame.AI.TaskManager::Awake
SSSGame.SettingsManager::Awake            SSSGame.SettlementManager::Awake
SSSGame.CavesManager::Awake               SSSGame.Combat.CombatManager::Awake
SSSGame.UI.VillagerSelectionManager::Awake  SSSGame.Weather.WeatherConditionDatabase::Awake
... (29 no total; ver docs/re/tools/rva-to-method.fsx 0x8009B0)
```

O endereço é um thunk de 7 bytes (`xor edx,edx` + `jmp 0x35626A0`) seguido de `int3` — ou seja,
**não existe corpo próprio de `PlayerHouse._OnActivate`**; o ponteiro aponta para um corpo
compartilhado. Outros "métodos" de `PlayerHouse` têm o mesmo problema: `OnDestroy` → 26
aliases, `get_OwnerStructure` → 403 aliases.

Consequência: aplicar Harmony nesse endereço instala o postfix em **todos os 29 métodos**. Esta
é a origem mais provável das 16 linhas `[PARTY]` do log — não necessariamente 16 `PlayerHouse`.

### 3.2 O `__instance` do patch não tem tipo real (verificado no fonte do Il2CppInterop)

Em `Il2CppInterop.HarmonySupport/Il2CppDetourMethodPatcher.cs`, o `this` nativo é convertido
para o tipo **declarado** do parâmetro via `Il2CppObjectPool.Get<T>(ptr)`. E
`Il2CppObjectPool.Get<T>` **não compara a classe do ponteiro** com `T` (só trata tipos
injetados). Ou seja:

- `__instance is PlayerHouse` é **sempre verdadeiro**, qualquer que seja o objeto nativo;
- `house.gameObject`/`scene.IsValid()` lê offsets de `Component` (válidos em qualquer
  componente) → o guard de prefab **não** distingue tipo;
- `house.allowPartyManagement = true` executa `*(bool*)(ptr + il2cpp_field_get_offset(PlayerHouse,
  allowPartyManagement)) = true` → **escreve 1 byte no objeto estranho**. É parente do dead end
  #11 (wrappers mentem sobre o tipo), na direção inversa.

### 3.3 O offset do campo (inferido — confirmar em runtime)

`PlayerHouse` tem **só 3 campos próprios**: `_OwnerStructure_k__BackingField` (`Structure`),
`spawnPos` (`Transform`) e `allowPartyManagement` (`bool`). O corpo de
`PlayerHouse::Initialize(Structure)` (`0xA76460`, unique) grava uma referência em `this+0x20`
seguida de `call 0x5CF280` — o mesmo helper de barreira de GC usado nas outras escritas de
referência do método —, o que situa `_OwnerStructure` em `0x20`, `spawnPos` em `0x28` e
`allowPartyManagement` em **`0x30`** — o mesmo `+0x30` lido nos crashs de `WeatherManager` e
`VillagerSelectionManager`.

**Não confirmado:** o offset nativo exato (é resolvido em runtime por
`il2cpp_field_get_offset`). O probe abaixo imprime esse número; enquanto isso, trate `0x30`
como hipótese forte, não como fato.

## 4. O que lê `allowPartyManagement` e o que a Cabana nível 2 fornece

**Verificado (superfície):**

| Fato | Evidência |
|---|---|
| `PlayerHouse : MonoBehaviour` tem só `_OwnerStructure`, `spawnPos`, `allowPartyManagement` | `ilspycmd -t SSSGame.PlayerHouse` (interop) — campos expostos como propriedades via `il2cpp_field_get_offset` |
| `PlayerHouse` **não** guarda nível | idem; nível/footprint vive em `Structure.footprintLevel` (`StructureFootprintLevel`) |
| `PlayerParty` **não** referencia `PlayerHouse` | lista de campos de `SSSGame.PlayerParty` (`_pc`, `_partyMembers`, `_partyPets`, `_followQuest`, … nenhum house/gate) |
| `PlayerPartyWidget : HUDWidget` **não** referencia `PlayerHouse` | campos: `Content`, `PartyMemberWidgetPrefab`, `PetContent`, `PetPartyMemberWidgetPrefab`, `PartyMembers`, `PetPartyMembers`, `_playerParty` |
| `PlayerHouse._OnActivate` é stub compartilhado (não seta nada) | §3.1 |

**Não verificado (precisa de probe/xref):** *quem* lê o bool. Não está em `PlayerParty` nem no
widget de HUD; o candidato natural é a UI/interação da estrutura (botão "gerenciar party" na
Cabana) ou o `SettlementManager`. Responder isso exige (a) o offset do campo + xref no binário
(Ghidra/Cpp2IL, custoso) **ou** (b) um probe: patch de log nos candidatos e abrir a party na
Cabana nível 2 vanilla.

**O bool sozinho basta?** Provavelmente **não é o único estado necessário**: a Cabana nível 2 é
uma `Structure` com `footprintLevel`, `OwnerStructure` e `spawnPos` ligados, e o widget de HUD
opera sobre `_playerParty` (que existe na cena, mas o probe já mostrou `members=0
authority=False`). Forçar o bool num `PlayerHouse` de nível 1 libera o *flag*, mas não cria
party nem liga esses vínculos. Isso continua **hipótese** até alguém abrir a UI com o bool
forçado por um caminho que não corrompa memória.

## 5. Caminho seguro para liberar a party cedo

Ranking por risco de crash nativo (do menor para o maior):

| Opção | Como | Risco | Por quê |
|---|---|---|---|
| **(d) comando manual pós-load** | `/unlockparty` no chat (já temos hook) → busca `PlayerHouse` tipado e seta o bool **uma vez** | **menor** | sem patch de código nativo, depois do load, em instância real, acionado pelo jogador; falha vira log, não crash |
| **(b) bool em instância de cena após o load** | driver `MonoBehaviour` que, depois do mundo carregado, obtém `PlayerHouse` por API tipada e seta o bool 1× | baixo | mesma escrita, sem detour; elimina a classe de bug do §3.2. **É o caminho já escolhido no `party-unlock-plan.md` §6.7** (poll), ainda não confirmado em load |
| **(a) patch no consumidor do bool** | achar quem lê o bool e patchar *esse* método | médio | depende de identificar o consumidor; antes, checar aliases do alvo (ver dead end #21) |
| **(c) upgrade/spawn nativo da Cabana** | subir a estrutura para nível 2 pelo caminho do jogo | alto/escopo grande | mexe em estrutura, custo de material e save; precisa de autoridade no co-op |
| ~~patch em `_OnActivate`~~ | — | **descartado** | stub compartilhado + escrita em objeto de tipo estranho (§3) |

Regras que saem daqui:

1. **Nunca** mutar campo em postfix de método com `aliases > 1`. Logue os aliases antes.
2. **Nunca** confiar em `__instance is T` como guarda de tipo: use
   `IL2CPP.il2cpp_object_get_class(ptr)` + nome da classe, ou `GetType()` do wrapper só para
   debug (não discrimina).
3. Preferir **propriedade de instância obtida de API tipada** a wrapper herdado do patch:
   `FindAnyObjectByType<PlayerHouse>()` **singular** (o plural `FindObjectsOfType<T>()` /
   `FindObjectsByType<T>()` já explodiu nesta build — dead ends #10 e #19) ou `__instance` de um
   método *unique* como `PlayerHouse.Initialize`.
4. Mutação idempotente, uma vez por mundo, com log de `GetInstanceID()`/`name`.
5. `FindAnyObjectByType<PlayerHouse>()` pode devolver a casa de outro dono/outpost: logar
   `name`, `OwnerStructure` e `footprintLevel` antes de escrever.

## 6. Próximo probe (ordem exata)

Rodar **um por vez**, sem mudar mais nada, e registrar no log:

1. **Baseline** — `DebugUnlockParty=false`, mesmo DLL: esperado sem crash e **sem** linhas
   `[PARTY]`. (Confirma que a correlação não é coincidência.)
2. **Patch só de log** — patch em `_OnActivate` com postfix que **apenas loga**:
   `il2cpp_object_get_class(ptr)` + `il2cpp_class_get_name` + `gameObject.name` +
   `GetInstanceID()`. Sem escrever nada. Se o crash desaparecer, a escrita é a causa; se
   aparecerem classes diferentes de `PlayerHouse` no log, o §3 está confirmado empiricamente.
3. **Offset real** — logar `il2cpp_field_get_offset(fieldInfo de allowPartyManagement)` uma vez
   (confirma ou derruba o `0x30`).
4. **Mutação tipada** — sem patch em `_OnActivate`; driver pós-load que faz
   `FindObjectsOfType<PlayerHouse>()`, loga `name`, `OwnerStructure`, `footprintLevel` e seta o
   bool 1×. Depois abrir a UI da party e ver se o botão aparece.
5. **Consumidor** — com o passo 4 estável, patch de log (prefix) nos candidatos
   (`PlayerPartyWidget.Activate/UpdateState`, UI da estrutura) para descobrir quem lê o bool.

Critérios de aceite do probe: o jogo sobrevive ao load + 2 min de jogo, a UI da party abre, e o
log mostra **um** `PlayerHouse` real (`OwnerStructure != null`, `footprintLevel` conhecido) —
não 16 objetos anônimos.

## 7. Armadilhas registradas

- WER "deslocamento de falha" = RVA de imagem, não VA → dá para cruzar direto com o DB.
- `PlayerHouse._OnActivate`/`OnDestroy`/`get_*` resolvem para stubs compartilhados; patch por
  nome nunca é "só daquele método" sem checar aliases.
- `Player.log` termina em `[Weather System] Weather Forcast Reset` no run de 18:28 e o
  `Player-prev.log` (18:21) termina na mesma linha — esse é o último log da engine antes do AV,
  não a causa: é a testemunha.
- Um dump WER pode ter o `RIP` do contexto já dentro do dispatcher de exceção do ntdll; use
  `ExceptionAddress` (do `MINIDUMP_EXCEPTION_STREAM`) e confirme pelo WER.
- Sem `cdb`/WinDbg nesta máquina: o caminho barato é WER (offsets) + `MethodAddressToToken.db`
  (nomes) + Iced (instrução). Ver dead ends #18.

## 8. Perguntas ainda abertas

- Quem lê `allowPartyManagement`? (UI da estrutura? `SettlementManager`?)
- O bool é suficiente para o botão aparecer, ou a UI exige `footprintLevel ≥ 2` / `PlayerParty`
  com `_pc` preenchido?
- O crash de 18:19 (`VillagerSelectionManager`) e o de 18:21 (`op_Equality`) usavam builds
  anteriores (com `Initialize` também patchado); não há DLL antiga para correlacionar — tratar
  como histórico, não como evidência do build atual.

## 9. Alinhamento com `docs/re/party-unlock-plan.md`

O plano da party (escrito em paralelo, 2026-10-29) já trocou o Harmony por um poll pós-load — o
que este documento **confirma** como direção certa. Três correções/refinos a levar para lá:

1. **Atribuição do `0xF72955`:** o plano diz que "ligar o bool em `Initialize` junto com
   `_OnActivate` causou crash". Os dois últimos crashes (18:24 e 18:28) usaram builds que
   patchavam **só** `_OnActivate` (o deploy de 18:27:54 tem só esse alvo) e caíram no **mesmo**
   `0xF72955` — que é `WeatherManager.Instance_OnWeatherUpdate`, sintoma, não causa. `Initialize`
   é único (1 alias) e não é o problema.
2. **O motivo do crash não é "patch em lifecycle":** é o stub de 29 aliases + `__instance` sem
   tipo real (§3). Qualquer patch de mutação em endereço com `aliases > 1` repete o bug, mesmo
   fora de `Awake`/`_OnActivate`.
3. **O gate pode ser dispensável:** se o recrutamento nativo for feito por
   `Villager.Rpc_ChangePlayerParty(...)` (caminho do plano, §1), a party não depende de
   `allowPartyManagement` — o bool parece liberar só a **UI** de gerenciar party. Ou seja: para
   `follow`/`stop`/`come`, o caminho de maior valor é o RPC nativo, não o unlock.

Onde este documento e o plano divergirem sobre o comportamento do gate, vale o **probe**, não a
inferência: nenhum dos dois viu a UI abrir com o bool forçado.
