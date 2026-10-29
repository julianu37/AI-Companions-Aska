# Ordens — intenção -> API nativa

## Ações iniciais

| Ação | Significado | API nativa (hipótese) |
|---|---|---|
| `follow` | seguir o jogador | `PlayerParty` + `PartyFollowQuest`/`FollowPlayerTask` |
| `stop` | parar a ordem atual | `AbortOrder`/`ReleaseTaskAgent`/estado da party |
| `come` | vir até o jogador | `FollowPlayerTask` com range 0 ou `MoveToPositionStep` |
| `attack` | atacar alvo | `PartyCombatQuest`/`FocusOnTarget`/`IAttackTarget` |
| `gather` | coletar recurso | `ResourceGatherTask`/`TaskDispatcher`/`HarvestMarker` |
| `equip` | equipar item | `CharacterEquipment`/`Inventory` (cuidado: família de inventário) |
| `drop` | largar item | `ItemDrop`/inventário (cuidado) |
| `inventory` | abrir inventário | UI do jogo (a confirmar) |
| `none` | conversa | `DialogueMenu.Display` + TTS |

## Regras

- **Validar antes de executar**: alvo existe? item está no inventário? criatura no alcance?
- **Nunca inventar** item/criatura; se não achar, recusar com fala clara.
- **Nunca** executar ação fora da lista.
- **Ordem = uma ação**; se o modelo devolver ambíguo, prefira a ação segura (conversa/recusa).
- **Números**: `amount` é um alvo total (ex.: 10 madeiras), não “10 viagens”.
- **Recurso**: se `action` e `resource` discordarem, o recurso específico manda (ex.:
  `action=wood`, `resource=stone` -> pedra).
- **Alvo**: se o modelo não nomear, tentar extrair do texto do jogador e validar; nunca chutar.
- **Feedback**: sempre uma linha no chat/balão (“Deixa comigo”, “Não achei o javali”).

## Mapeamento de alto nível

```
intent.action
  ├─ follow/stop/come  -> CompanionParty
  ├─ attack            -> CompanionCombat (alvo validado)
  ├─ gather            -> CompanionGather (recurso + amount)
  ├─ equip/drop        -> CompanionInventory (item validado)
  ├─ inventory         -> CompanionInventoryUI
  └─ none              -> CompanionSpeech (reply)
```

## Riscos

- `QuestRunner.Update` é por villager/frame; não patchar sem gate.
- A FSM é ScriptableObject; o corpo pode não ser legível.
- `SetTaskAgent` pode não ser “party member”.
- Família de inventário: capturar por lifecycle + polling, nunca patch de parâmetro.
- Em co-op, host executa; cliente pede.

## Teste

1. Cada ação com alvo válido.
2. Ação sem alvo -> recusa clara.
3. Ação com alvo inexistente -> recusa clara.
4. `amount` já atingido -> “já temos o suficiente”.
5. `stop` no meio de um trabalho -> para sem quebrar a vila.
6. Save/reload no meio de uma ordem -> estado consistente.
