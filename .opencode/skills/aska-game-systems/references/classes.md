# Classes-chave do ASKA

> Superfície extraída da interop em 2026-10-28. `?` = a confirmar em runtime.

## Personagens

| Tipo | Base | Membros úteis |
|---|---|---|
| `SSSGame.Character` | `Fusion.NetworkBehaviour` | `CurrentHealth`, `MaxHealth`, `IsDead`, `HasAuthority`, `GetDamageTakenHistory()` |
| `SSSGame.PlayerCharacter` | `Character` | override de `TakeDamage`/`Spawned`/`Despawned`/`IsPlayer` |
| `SSSGame.Villager` | `Character` | `Spawned`, `Despawned`, `DescriptionData`, `SpawnData`, `PerksData`; inventário/equipamento |
| `SSSGame.Creature` | `Fusion.NetworkBehaviour` | monstros; `TakeDamage` separado |

## Party

| Tipo | Base | Membros |
|---|---|---|
| `SSSGame.PlayerParty` | `Fusion.SimulationBehaviour` | `_partyMembers`, `_partyPets`, `_boundWorkstations`, `_followQuest`, `_interactQuest`, `_combatQuest`, `_pc`, `_hasAuthority`; `SetTaskAgent`, `TryOrderToInteract`, `FocusOnTarget`, `AbortOrder`, `AddPet` |
| `SSSGame.Network.NetworkPlayerParty` | `NetworkBehaviour` | `SendTargetToFocus`, `SendInteractOrder`, `ModifyLocalInteractionOrderToTarget`, `Rpc_AbortOrder`, `Rpc_FocusTarget` |

## Quests/tasks

| Tipo | Base | Papel |
|---|---|---|
| `SSSGame.AI.Quest` | — | base de quest |
| `SSSGame.AI.QuestRunner` | — | roda a melhor quest por villager (`Update` por villager) |
| `SSSGame.AI.TaskManager` | — | gerencia tasks/agentes |
| `SSSGame.AI.Task` / `DynamicTask` | — | base de task |
| `SSSGame.AI.FollowPlayerTask` | `DynamicTask` | seguir (`PlayerParty`, min/max range, `MoveToPositionStep`) |
| `SSSGame.AI.PartyFollowQuest` | `Quest` | quest de seguir (FSM `followFsm`) |
| `SSSGame.AI.PartyInteractQuest` | `Quest` | interação/ordem |
| `SSSGame.AI.PartyCombatQuest` | `Quest` | combate |

## Chat/diálogo

| Tipo | Membros |
|---|---|
| `SSSGame.UI.ChatMenu` | `SendPlayerMessage(string)` público; `CurrentChannel`, `IsTextInputActive`, `ChatWindowVisibility`, `InputFieldVisiblity`; `_chatSession` |
| `SSSGame.Network.NetworkChatSession` | sessão Photon Chat; canais/conexão |
| `SSSGame.Dialogue` | `message`, `duration`, `skip` |
| `SSSGame.DialogueMenu` | `Display(Character, ref string, float, bool, bool)` estático; `ClearDialogues`, `RegisterDialogue` |

## Inventário/vila

| Tipo | Papel |
|---|---|
| `SSSGame.CharacterInventory` / `VillagerInventory` / `PlayerInventory` | inventário |
| `SandSailorStudio.Inventory.*` | itens, containers, processos (família perigosa para patch) |
| `SSSGame.Workstation` / `Buildstation` | estações |
| `SSSGame.PopulationManager` | população/spawn |
| `SSSGame.SettlementManager` / `Settlement` | assentamento |

## Rede

| Tipo | Papel |
|---|---|
| `Fusion.NetworkBehaviour` | objeto de rede |
| `Fusion.SimulationBehaviour` | simulação/rede |
| `Fusion.NetworkRunner` | runner |
| `SSSGame.Network.NetworkSession` / `NetworkSpawner` | sessão/spawn |

## Regras

- Patche o tipo **mais derivado**.
- `Creature` não é `Character`; não misture.
- Não patche família de inventário.
- Não assine `Action`; polling.
- Confirme a assinatura antes de codar.
