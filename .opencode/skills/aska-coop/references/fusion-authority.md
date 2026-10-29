# Fusion — autoridade e RPCs

## Propriedades úteis

```csharp
bool isServer = runner.IsServer;
bool isMaster = runner.IsSharedModeMasterClient;
bool hasState = behaviour.HasStateAuthority;
bool hasInput = behaviour.HasInputAuthority;
```

- `NetworkBehaviour`/`SimulationBehaviour` expõem `Runner`, `Object`, `HasStateAuthority`,
  `HasInputAuthority`.
- `Runner` também tem `IsSinglePlayer`, `GameMode`, `Mode`, `IsClient`, `IsSceneMaster`,
  `IsConnectedToServer`.
- Em single-player, `IsServer`/`IsSharedModeMasterClient` tendem a ser verdadeiros; confirme.

## Regras

- **Host autoridade:** só o host altera estado do companheiro/party.
- **Cliente pede:** RPC/pedido; não escreve estado direto.
- **Use as RPCs nativas** (`NetworkPlayerParty.Rpc_AbortOrder`, `Rpc_FocusTarget`,
  `SendInteractOrder`, ...) quando existirem.
- **Não patche** `CopyBackingFieldsToState`/`CopyStateToBackingFields`.
- **Não** faça RPC por frame; agrupe/limite.
- **NetworkId/persistent ID** para identificar o villager; não use referência local.

## Fluxo de uma ordem

```
Cliente: input -> pedido (RPC/pedido)
Host: valida -> executa a ordem nativa -> estado replica
Cliente: vê o resultado
```

- Se o cliente não tem autoridade, a ordem pode ser recusada com mensagem clara.
- O dono do companheiro deve ser definido explicitamente (host? player que recrutou?).

## Teste

- Host recruta, cliente vê.
- Cliente ordena (se permitido).
- Host sai no meio.
- Cliente entra no meio.
- Save/reload.
- Dois clientes ao mesmo tempo.

## Riscos

- `PlayerParty` é `SimulationBehaviour`; a party pode ser do jogador local, não global.
- Estado de UI (painel aberto) é local, não sincronizado.
- Voz/TTS: cada cliente pode tocar localmente; não replicar áudio.
