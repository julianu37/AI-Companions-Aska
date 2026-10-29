# Sincronização do companheiro

## O que sincronizar

| Estado | Onde | Quando |
|---|---|---|
| Vínculo villager <-> party | host | recrutar/dispensar |
| Ordem atual (follow/stop/...) | host | mudança de ordem |
| Nome | host | rename |
| Persona/voz | host (perfil) | edição no painel |
| Memória de conversa | local (por jogador?) | a definir |
| Posição/animação | jogo (nativo) | sempre |

## O que NÃO sincronizar

- Texto do painel, cursor, foco de input.
- Áudio de TTS (cada cliente toca localmente, se quiser).
- Prompt/JSON (fica no host ou no cliente que pediu).

## Persistência

- O vínculo deve sobreviver a save/reload (ou ser refeito de forma limpa).
- Use o ID persistente do villager, não a referência de objeto.
- Limpe caches ao sair do mundo.

## Estratégia

1. Single-player: host = único; tudo local.
2. Co-op: host executa; cliente envia pedido; estado replica por RPC/NetworkBehaviour.
3. Se o jogo já tem RPC para a ação (party/interact), use-a.
4. Se não, adicione um RPC pequeno e idempotente (ex.: `RpcSetOrder(NetworkId, byte)`).

## Teste

- Host recruta; cliente vê o mesmo villager.
- Cliente pede “segue”; host executa.
- Host muda a ordem; cliente vê.
- Host sai; cliente não crasha.
- Reload; vínculo consistente.
