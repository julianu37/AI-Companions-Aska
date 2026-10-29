---
name: Co-op e Fusion no ASKA
description: Como manter o mod compatível com o co-op do ASKA — autoridade host, RPCs Fusion, sincronização de estado do companheiro, NetworkId/persistência e testes com dois jogadores. Use ao tocar em rede ou estado compartilhado.
---

# Co-op e Fusion no ASKA

O ASKA é co-op (Photon **Fusion**). O mod começa single-player, mas todo estado de IA/ordem deve
nascer com **autoridade** em mente.

## Quando usar

Sempre que o trabalho envolver: recrutar/ordenar um companheiro em sessão com outro jogador,
estado compartilhado (nome, persona, voz, ordem), RPC, `HasAuthority`, `Runner.IsServer`,
save/reload, ou teste co-op.

## Modelo

```
Host (autoridade)
  ├── executa a IA/ordem
  ├── altera o estado do villager/party
  └── replica o estado (RPC/NetworkBehaviour)
Cliente
  ├── vê o companheiro
  ├── pede uma ordem (RPC/pedido)
  └── não escreve estado de rede diretamente
```

## Regras

- **Nunca** patchar `CopyBackingFieldsToState`/`CopyStateToBackingFields`.
- **Nunca** escrever estado sem `HasAuthority`/`Runner.IsServer`.
- Use as **RPCs nativas** (`NetworkPlayerParty.Rpc_*`) quando existirem.
- Não assuma que o cliente vê o mesmo objeto/ID; use `NetworkId`/persistent ID.
- Evite RPC por frame; agrupe/limite.
- Teste com dois jogadores reais: host recruta, cliente vê; cliente ordena; host sai; reload.
- Se não houver ambiente co-op, documente o que falta e **não** declare pronto.

## Referências

- `references/fusion-authority.md` — propriedades, gates, RPCs.
- `references/companion-sync.md` — o que sincronizar e quando.
- `docs/testing.md` (M7) — matriz de teste.
