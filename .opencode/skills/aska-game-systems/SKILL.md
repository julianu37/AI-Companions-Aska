---
name: Sistemas do ASKA
description: Mapa dos sistemas nativos do ASKA — Character/Villager/PlayerCharacter, PlayerParty, quests/tasks/FSM, inventário, trabalho, chat/diálogo, população/assentamento e Fusion. Use antes de tocar na IA ou na vila.
---

# Sistemas do ASKA

O ASKA já tem uma simulação de vila rica: villagers com trabalho, schedule, necessidades,
inventário, combate, party e rede. O mod **não** recria isso; ele **dirige** o que existe.

## Quando usar

Sempre que o trabalho envolver: recrutar um villager, fazer seguir/parar/atacar/coletar,
inventário/equipamento, schedule/necessidades, workstations, população/assentamento, chat ou
diálogo. Combine com `aska-il2cpp` para o patch e `aska-coop` para autoridade.

## Mapa rápido

| Sistema | Classes |
|---|---|
| Personagens | `SSSGame.Character`, `PlayerCharacter`, `Villager`, `Creature` |
| Party | `SSSGame.PlayerParty`, `SSSGame.Network.NetworkPlayerParty` |
| Quests/tasks | `SSSGame.AI.Quest`, `QuestRunner`, `TaskManager`, `Task`, `DynamicTask`, `FollowPlayerTask`, `PartyFollowQuest` |
| IA/FSM | `SSSGame.AI.FSM.*` (ScriptableObjects) |
| Inventário | `SSSGame.CharacterInventory`, `VillagerInventory`, `SandSailorStudio.Inventory.*` |
| Trabalho | `SSSGame.Workstation`, `Buildstation`, `TaskDispatcher`, `WorkstationTaskData` |
| Vila | `SSSGame.PopulationManager`, `SettlementManager`, `Settlement` |
| Chat | `SSSGame.UI.ChatMenu`, `NetworkChatSession` |
| Diálogo | `SSSGame.Dialogue`, `DialogueMenu`, `CharacterDialogueWraper` |
| Localização | `SSSGame.Localization.*` |
| Rede | `Fusion.NetworkBehaviour`, `SimulationBehaviour`, `NetworkRunner` |

## Fatos importantes

- `PlayerCharacter` e `Villager` são as **únicas** subclasses de `Character`; guerreiro é
  villager com flag/equipamento, não uma classe.
- `Creature` é uma hierarquia **separada**; patch em `Creature.TakeDamage` não vê o player.
- `PlayerParty` é `Fusion.SimulationBehaviour` e já tem `_partyMembers`, `_followQuest`,
  `_interactQuest`, `_combatQuest`.
- `Villager` e `PlayerCharacter` sobrescrevem métodos; patche o **tipo mais derivado**.
- A FSM é ScriptableObject; o corpo pode não ser legível no dump.
- Não assine `Action`; use polling.

## Fluxo

1. Leia `docs/re/game-systems.md` e `docs/re/villager-ai-and-party.md`.
2. Confirme a assinatura na interop (`ilspycmd`) antes de codar.
3. Faça um probe que logue o estado antes/depois (party, quest, inventário).
4. Use o caminho nativo; se não existir, recuse a ordem com mensagem clara.
5. Registre o que foi confirmado em `docs/re/`.

## Referências

- `references/classes.md` — tabela de classes/campos/métodos.
- `references/party-recruitment.md` — hipótese de recrutamento e o que testar.
- `references/orders.md` — ações iniciais e o mapeamento para APIs nativas.
