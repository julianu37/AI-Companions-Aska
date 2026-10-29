# RE — mapa dos sistemas do ASKA

**Fonte:** superfície das interop assemblies (`BepInEx\interop\Assembly-CSharp.dll`), extraída
com `ilspycmd` em **2026-10-28**. Isto é **superfície de API**, não lógica. Onde a lógica
importa, o status é “a confirmar em runtime”.

---

## 1. Namespaces

| Namespace | Conteúdo |
|---|---|
| `SSSGame` | Código do jogo (Sand Sailor Studio): personagens, vila, estruturas, interações, UI, rede |
| `SSSGame.AI` | Quests, tasks, FSM, dispatchers, outlets, combat AI |
| `SSSGame.AI.FSM` | Nós de FSM (ScriptableObjects) usados pelas quests |
| `SSSGame.UI` | Menus, painéis, HUD, chat, diálogo, schedules |
| `SSSGame.Network` | Sessão, spawner, chat (Photon), party, world data |
| `SSSGame.Localization` | Texto localizado (uGUI/TMP) |
| `SandSailorStudio.Inventory` | Inventário, itens, processos, containers |
| `SandSailorStudio.*` | Utilitários, atributos, culling, storage |
| `Invector.*` | Character controller/combate de terceiros usado pelo ASKA |
| `Fusion.*` | Photon Fusion (rede) |

## 2. Personagens

| Classe | Base | Papel |
|---|---|---|
| `SSSGame.Character` | `Fusion.NetworkBehaviour` | Base de saúde/combate/equipamento |
| `SSSGame.PlayerCharacter` | `Character` | Avatar do jogador |
| `SSSGame.Villager` | `Character` | Villager (trabalhador, guerreiro, etc.) |
| `SSSGame.Creature` | `Fusion.NetworkBehaviour` | Monstros/criaturas (hierarquia separada) |
| `SSSGame.CharacterInventory` | — | Inventário do personagem |
| `SSSGame.VillagerInventory` | — | Inventário do villager |
| `SSSGame.PlayerInventory` | — | Inventário do jogador |

**Observação:** `Villager` não tem subclasse de “soldier/guard”; guerreiros são villagers com
flag/equipamento. Confirmar `IsWarrior` e o conjunto de flags.

## 3. Vila e trabalho

| Classe | Papel |
|---|---|
| `SSSGame.PopulationManager` | População, spawn, dados de villagers |
| `SSSGame.SettlementManager` / `Settlement` | Assentamento, estruturas, limites |
| `SSSGame.Workstation` | Estação de trabalho (base) |
| `SSSGame.Buildstation` | Construção |
| `SSSGame.AI.TaskManager` | Gerencia tasks/agentes |
| `SSSGame.AI.QuestRunner` | Roda quests por villager |
| `SSSGame.AI.Quest` | Base de quest |
| `SSSGame.AI.Task` / `DynamicTask` | Base de task |
| `SSSGame.AI.TaskDispatcher` | Distribui tasks |
| `SSSGame.AI.WorkstationTaskData` | Dados de task de estação |

## 4. Party e seguir

| Classe | Papel |
|---|---|
| `SSSGame.PlayerParty` | `Fusion.SimulationBehaviour`; guarda `_partyMembers`, `_partyPets`, `_followQuest`, `_interactQuest`, `_combatQuest`; tem `SetTaskAgent`, `AddInteractionOrder`, `TryOrderToInteract`, `FocusOnTarget`, etc. |
| `SSSGame.Network.NetworkPlayerParty` | RPCs de foco/interação/abortar; estado de rede da party |
| `SSSGame.AI.PartyFollowQuest` | Quest de seguir (FSM `followFsm`) |
| `SSSGame.AI.PartyInteractQuest` | Quest de interagir/ordenar |
| `SSSGame.AI.PartyCombatQuest` | Quest de combate da party |
| `SSSGame.AI.FollowPlayerTask` | Task de seguir (`PlayerParty`, min/max range, `MoveToPositionStep`) |
| `SSSGame.AI.PartyFollowQuest.PartyFollowQuestData` | Dados da quest |

**Hipótese de trabalho:** dá para recrutar um villager chamando `PlayerParty.SetTaskAgent` (ou
o caminho nativo equivalente) e deixar a `PartyFollowQuest` cuidar do “seguir”. **A confirmar
em runtime**; a assinatura sozinha não prova que o método adiciona à party.

## 5. Chat e diálogo

| Classe | Papel |
|---|---|
| `SSSGame.UI.ChatMenu` | Menu de chat; `SendPlayerMessage(string)` público; `CurrentChannel`, `IsTextInputActive`, `ChatWindowVisibility`, `InputFieldVisiblity` |
| `SSSGame.UI.ChatMessage` | Item de mensagem |
| `SSSGame.Network.NetworkChatSession` | Sessão de chat (Photon Chat), canais, conexão |
| `SSSGame.Dialogue` | `message`, `duration`, `skip` |
| `SSSGame.DialogueMenu` | `Display(Character, ref string, float, bool, bool)` estático; `ClearDialogues`, `RegisterDialogue` |
| `SSSGame.CharacterDialogueWraper` | Wrapper de diálogo por personagem |
| `SSSGame.AI.FSM.FSM_ShowDialogueMessage` | Nó de FSM que mostra diálogo |

## 6. Rede

- `Fusion.NetworkBehaviour`, `SimulationBehaviour`, `NetworkRunner` (Photon Fusion).
- `SSSGame.Network.NetworkSession`, `NetworkSpawner`, `NetworkWorldDataManager`.
- Autoridade: `HasStateAuthority`, `HasInputAuthority`, `Runner.IsServer`,
  `Runner.IsSharedModeMasterClient`.
- O ASKA tem single-player e co-op; a `PlayerParty` já é `SimulationBehaviour`.

## 7. Localização

- `SSSGame.Localization.LocalizedText`, `LocalizedTextMesh`, `LocalizedCompositeText`,
  `TMProTextWrapper`, `UnityTextWrapper`.
- O jogo tem um sistema próprio; podemos usar JSON próprio no mod e integrar depois.

## 8. O que precisa de probe

- [ ] `PlayerParty.SetTaskAgent(villager)` adiciona à party? Retorna o quê?
- [ ] `PartyFollowQuest` já está ativa na party do jogador? Como iniciar?
- [ ] `ChatMenu.SendPlayerMessage` é chamado no single-player e no co-op?
- [ ] `DialogueMenu.Display` funciona para villager sem `CharacterDialogueWraper`?
- [ ] Qual é o caminho de “ordem” do jogador para villager no vanilla (`CommandInteraction`)?
- [ ] Como o save persiste a party (`c_PartyMembersKey`)?
