# RE — villager, IA e PlayerParty

**Fonte:** superfície das interop assemblies (`SSSGame`, `SSSGame.AI`), extraída com
`ilspycmd` em **2026-10-28**. Superfície apenas; a lógica é hipótese até o probe em runtime.

---

## 1. Hierarquia

```
Fusion.NetworkBehaviour
  └── SSSGame.Character
        ├── SSSGame.PlayerCharacter
        └── SSSGame.Villager
Fusion.SimulationBehaviour
  └── SSSGame.PlayerParty
```

- `Villager` **não** tem subclasses de soldier/guard; guerreiros são villagers com
  equipamento/flags (`IsWarrior`).
- `Creature` é uma hierarquia separada (monstros), não subtipo de `Character`.

## 2. `SSSGame.Villager` (superfície)

- Muitos membros; os relevantes para o mod:
  - `Spawned()` / `Despawned(NetworkRunner, bool)` — ciclo de vida (captura de instância)
  - `DescriptionData` (definitionID, nameID, appearance, perks, portraitCameraIndex)
  - `SpawnData` (shouldShowNotification, fromDeserialization, appearance, nameID, perks,
    portraitCameraIndex)
  - `PerksData` (perk_0..perk_4, `ToStatusEffectList`, serialize)
  - `HasAuthority`, `CurrentHealth`, `MaxHealth`, `IsDead`
  - inventário (`VillagerInventory`), equipamento (`CharacterEquipment`)
- O villager é um `Character` com `Fusion.NetworkBehaviour`; writes de estado devem respeitar
  autoridade.

## 3. `SSSGame.PlayerParty` (superfície)

Base: `Fusion.SimulationBehaviour`.

Campos:

```text
PlayerCharacter _pc
List<Villager> _partyMembers
List<Pet> _partyPets
List<IWorkstation> _boundWorkstations
PartyFollowQuest _followQuest
PartyInteractQuest _interactQuest
PartyCombatQuest _combatQuest
NetworkSession session
NetworkPlayerParty _networkParty
List<Interaction> _localInteractionOrders
bool _hasAuthority
PlayerRef _authority
vFSMBehaviour followFsm / interactFsm / combatFsm
```

Métodos públicos relevantes:

```text
bool SetTaskAgent(ITaskAgent agent)
bool ReleaseTaskAgent(ITaskAgent agent)
void AddInteractionOrder(Interaction interaction)
bool TryOrderToInteract(Interaction interaction)
bool TryAbortOrder(Villager villager)
void AbortOrder(Villager villager)
bool FocusOnTarget(IAttackTarget target)
bool TryFocusOnTarget(IAttackTarget target)
bool HasInteractionOrder(Interaction interaction)
bool HasInteractionOrder(Villager villager)
void ModifyLocalInteractionOrder(Interaction, OrderModification)
void RemoveAllWorkers()
void ResumeActivity()
bool AddPet(Pet pet) / bool RemovePet(Pet pet)
```

Eventos (Action — **não** tentar assinar via `Il2CppSystem.Action`; ver dead-ends):

```text
OnVillagerAdded(Villager)
OnVillagerRemoved(Villager)
OnPetAdded(Pet)
OnPetRemoved(Pet)
OnWorkstationChanged(IWorkstation)
OnTaskAdded(IWorkstation, WorkstationTaskData)
OnTaskRemoved(IWorkstation, WorkstationTaskData)
OnLocalInteractionsChanged()
```

Nested type: `__Deserialize_b__0` procura `Villager` por `villagerPersistentID` — pista de que
a party é serializada por ID persistente.

### Hipótese de recrutamento

1. Achar a `PlayerParty` do jogador local (via `PlayerCharacter`/`PlayerManager` ou patch de
   `PlayerParty.Spawned`).
2. Chamar `SetTaskAgent(villager)` (ou o método nativo de adicionar membro, se houver).
3. Verificar `_partyMembers` / `OnVillagerAdded`.
4. Se a `PartyFollowQuest` não iniciar sozinha, iniciar a quest pelo caminho nativo
   (`QuestRunner`/`PlayerParty`).

**A confirmar:** `SetTaskAgent` é para *tasks* (trabalho), não necessariamente para “party
member”. Pode ser que a party use outro método/RPC. O probe precisa observar
`_partyMembers.Count` antes/depois e o `OnVillagerAdded`.

## 4. Quests e tasks

| Classe | Papel |
|---|---|
| `SSSGame.AI.Quest` | Base de quest; prioridade, FSM, dados |
| `SSSGame.AI.QuestRunner` | Executa a melhor quest por villager; `Update()` por villager |
| `SSSGame.AI.TaskManager` | Gerencia tasks/agentes/workstations |
| `SSSGame.AI.Task` / `DynamicTask` | Base de task |
| `SSSGame.AI.FollowPlayerTask` | Task de seguir; `PlayerParty`, `minRange`, `maxRange`, `MoveToPositionStep` |
| `SSSGame.AI.PartyFollowQuest` | Quest de seguir da party (FSM `followFsm`) |
| `SSSGame.AI.PartyInteractQuest` | Quest de interação/ordem |
| `SSSGame.AI.PartyCombatQuest` | Quest de combate |
| `SSSGame.AI.TaskDispatcher` | Distribui tasks |
| `SSSGame.AI.WorkstationTaskData` | Dados de task de estação |

**Riscos:**

- `QuestRunner.Update()` roda **por villager, por frame**; um patch ali é caro. Gate por
  `Time.frameCount` e evitar trabalho por invocação.
- A FSM é composta de ScriptableObjects (`SSSGame.AI.FSM.*`); os corpos podem não ser legíveis
  no dump. Não depender deles.
- A `PartyFollowQuest` pode ser instanciada pela própria party; não criar uma segunda.

## 5. Ordens nativas existentes

O ASKA já tem um sistema de **`CommandInteraction`** (`Interaction`) e
`CommandInteractionConfig/Session`; provavelmente é a ordem contextual do jogador para
villagers (ex.: “trabalhe aqui”). Vale mapear antes de inventar uma ordem nova:

- `CommandInteraction.Use(IInteractionAgent)` / `Update()`
- `CommandInteractionSession`
- `CommandInteractionConfig`
- `SSSGame.PlayerToVillagerInteractionConfig` (interação jogador -> villager)
- `SSSGame.VillagerInteraction` (interação do villager)

**Pergunta:** a ordem “segue” do vanilla usa `PlayerParty` ou `CommandInteraction`? O probe
deve observar os dois.

## 6. Plano de probe (branch `feat/recruit-follow`)

1. Capturar a `PlayerParty` local (patch de `Spawned` ou busca única).
2. Logar `_partyMembers.Count`, `_followQuest`, `_pc`, `HasAuthority`.
3. Recrutar um villager (`SetTaskAgent` ou equivalente) e logar o resultado.
4. Observar `OnVillagerAdded` por polling (`_partyMembers.Count`) — não assinar Action.
5. Se seguir não iniciar, tentar `PartyFollowQuest`/`FollowPlayerTask` pelo caminho nativo.
6. Testar `stop`/`come`; verificar persistência no save.

**Critério de aceite:** um villager segue o jogador em single-player, para quando ordenado, e
o save/reload mantém o vínculo (ou o recrutamento é refeito de forma limpa).

## 7. Gate da party (2026-10-29)

A criação/gestão da party é liberada por **`SSSGame.PlayerHouse.allowPartyManagement`**
(bool público no interop). A Cabana do Chefe nível 2 (“Cabana do Chefe Orgulhoso”) é o prédio
que liga o bool; no nível 1 ele vem `false`.

Para desenvolvimento, `General/DebugUnlockParty=true` aplica um postfix em
`PlayerHouse.Initialize`/`_OnActivate` e força o bool para `true` em toda `PlayerHouse`. O log
confirmou:

```text
[AIC][PARTY] DebugUnlockParty applied: SSSGame.PlayerHouse.Initialize, SSSGame.PlayerHouse._OnActivate
[AIC][PARTY] PlayerHouse allowPartyManagement: False -> true (debug unlock, ...)
```

A party em si continua sendo criada pela UI nativa; o mod não inventa membros. O probe
`CompanionPartyProbe` lê `_partyMembers`/`_partyPets`/`_pc` com polling de 1 Hz; antes da
criação da party ele mostra `members=0 authority=False pc=?` (a party é um componente de cena
sem membros). Depois que a party for criada, os nomes (Thorin, Maria, ...) devem aparecer e
ser registrados no `CompanionRegistry`.

## 8. Perguntas abertas

- `SetTaskAgent` adiciona à party ou só atribui trabalho?
- A party tem limite? (`GetAgentsCapacity`?)
- O que acontece se o villager morrer/desaparecer?
- Como o cliente vê a party em co-op?
- A ordem do jogador precisa de `HasAuthority`? O host é sempre quem executa?
