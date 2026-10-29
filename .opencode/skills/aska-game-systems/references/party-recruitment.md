# Recrutamento de villager — hipótese e probe

## Objetivo

Transformar um `Villager` da vila em companheiro que segue o jogador, sem criar um NPC
paralelo.

## Hipótese

1. A `PlayerParty` local é um `SimulationBehaviour` com `_partyMembers` e `_followQuest`.
2. `SetTaskAgent(ITaskAgent)` pode adicionar o villager (ou apenas atribuir trabalho).
3. `PartyFollowQuest` faz o “seguir” quando a party tem membros.
4. O vínculo é serializado por `villagerPersistentID`/`c_PartyMembersKey`.

**Nada disso está provado.** A assinatura não garante o comportamento.

## Probe mínimo (single-player, save descartável)

```csharp
// em um Update throttled, com a party local capturada:
Log.LogInfo($"[AIC][PROBE] party={party != null} members={party?._partyMembers?.Count} " +
            $"followQuest={party?._followQuest != null} auth={party?._hasAuthority}");

bool ok = party.SetTaskAgent(villager);
Log.LogInfo($"[AIC][PROBE] SetTaskAgent({villager.name}) -> {ok}; " +
            $"members={party._partyMembers.Count}");
```

Passos:

1. Capturar a `PlayerParty` (patch de `Spawned` ou busca única).
2. Logar estado inicial.
3. Chamar `SetTaskAgent(villager)`.
4. Logar estado final e observar `OnVillagerAdded` por polling.
5. Ver se o villager segue; se não, tentar `PartyFollowQuest`/`FollowPlayerTask`.
6. Testar `stop`/`come`.
7. Salvar, sair e voltar; ver se o vínculo persiste.

## Se `SetTaskAgent` não for o caminho

- Procurar RPC/método de “add party member” em `PlayerParty`/`NetworkPlayerParty`.
- Ver `CommandInteraction`/`PlayerToVillagerInteractionConfig` (ordem vanilla).
- Ver se a party é criada pelo “Eye of Odin”/summon; seguir o mesmo caminho.
- Não criar uma segunda quest de seguir concorrente com a nativa.

## Critérios de aceite

- [ ] O villager entra na party (contagem/evento).
- [ ] Segue o jogador a uma distância razoável.
- [ ] Para quando ordenado.
- [ ] Não quebra o trabalho/sobrevivência dele.
- [ ] Sobrevive a save/reload (ou recrutamento é refeito limpo).
- [ ] Sem crash/desync em single-player.

## Riscos

- `SetTaskAgent` pode ser só para workstations.
- A `PartyFollowQuest` pode ser instanciada pela própria party; não duplicar.
- `QuestRunner.Update` roda por villager/frame; não patchar sem gate.
- Em co-op, autoridade e persistência precisam de teste separado.
