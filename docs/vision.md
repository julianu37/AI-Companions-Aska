# Visão — AI Companions - ASKA

## O que queremos

Dar vida a **um companheiro com IA** dentro do ASKA: um villager da sua vila que **conversa**,
**obedece** e **fala**, usando **chat ou voz**, sem sair do jogo e sem depender de serviços
externos por padrão.

O jogador deve sentir que aquele villager é um **membro da equipe** — não um menu de ordens.
Ele tem nome, personalidade, memória curta e voz; entende pedidos em linguagem natural e usa o
que o ASKA já sabe fazer (seguir, coletar, lutar, equipar, carregar) para cumpri-los.

## Princípios

1. **Nativo primeiro.** O companheiro é um villager do jogo, recrutado para a `PlayerParty`.
   Movimento, combate, inventário, sobrevivência, animação e rede continuam sendo do ASKA.
2. **Local por padrão.** LLM, STT e TTS rodam na máquina do jogador. Nada de chave obrigatória,
   nada de áudio saindo para a nuvem.
3. **O jogador manda.** O mod não joga sozinho: ele interpreta o pedido e executa a ordem; o
   jogador mantém o controle e pode parar/corrigir a qualquer momento.
4. **Uma coisa de cada vez.** Primeiro o chat + LLM + uma ordem (seguir). Depois voz, fala,
   ordens com alvo, UI e co-op. Cada fase tem critério de aceite e teste em jogo.
5. **Evidência > achismo.** O ASKA é IL2CPP e muda com updates. Toda descoberta vai para
   `docs/re/` com data, versão e o comando/teste que a comprovou.
6. **PT-BR + EN desde o início.** Strings centralizadas; o jogador escolhe o idioma; a voz
   acompanha o idioma quando possível.
7. **Sem trapaça escondida.** O companheiro não duplica itens, não atravessa o mapa e não
   ignora regras do jogo sem uma opção explícita.

## O que NÃO é

- Não é um NPC customizado do zero (a base do Offline Companions não é portada).
- Não é um chatbot de janela separada: a conversa acontece no mundo, no chat e no balão do
  companheiro.
- Não é um mod de trapaças/cheats.
- Não é um serviço de nuvem: o LLM remoto é opcional, nunca o padrão.

## Sucesso (definição)

O jogador abre o ASKA, recruta um villager, digita **“segue”** ou segura **V** e fala
**“colete 10 de madeira”**, e o villager **faz** — com uma resposta curta e falada — sem
travar, sem dessincronizar o save e sem sair do mundo. Em PT-BR e em inglês.
