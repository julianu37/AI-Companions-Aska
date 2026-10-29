# Design — decisões e porquês

Este documento registra as **decisões de design** e as alternativas descartadas. Quando uma
decisão mudar, reescreva o fato aqui (não acumule “antes era X”).

---

## D1 — O companheiro é um villager existente, recrutado via `PlayerParty`

**Decisão:** o jogador escolhe um villager da vila e o recruta como companheiro. O mod usa a
`PlayerParty` nativa e as quests `PartyFollowQuest` / `PartyInteractQuest` / `PartyCombatQuest`.

**Por quê:**

- O ASKA já tem movimento, navegação, inventário, sobrevivência, animação, combate e rede
  funcionando para villagers.
- Recrutar um villager preserva a economia e a vida da vila; o jogador não ganha um “NPC
  premium” desbalanceado.
- A `PlayerParty` já tem follow/combat/interact quests e RPCs de ordem; é o caminho de menor
  risco e maior integração.
- Um NPC customizado exigiria recriar prefab, animator, inventário, navegação, rede e save —
  meses de trabalho e uma fonte infinita de bugs.

**Alternativas descartadas:**

- **Spawnar um villager novo dedicado:** possível, mas o vínculo com a vila e o save fica mais
  frágil; fica para depois, se o recrutamento não der conta.
- **NPC customizado do zero:** rejeitado para o escopo atual.

**Em aberto:** como o jogador recruta (interação “Recrutar” no villager? painel? comando?),
limite de companheiros, e o que acontece se o villager morrer.

---

## D2 — Chat endereçado, não sequestro do chat

**Decisão:** o mod só processa mensagens com **prefixo** (`/c ...`) ou **nome do companheiro**
no início (`Thorin, ...` / `Thorin: ...` / `Thorin ...`). Sem isso, ignora.

**Implementado na v0.4.0:**

- `AI.ChatPrefix` default `/c`; vazio desliga a captura por prefixo.
- `AI.CompanionNames` default `Thorin` (lista separada por vírgula); o primeiro nome é o
  destino de `/c ...`.
- `AI.AddressByName` default **true**: o nome tem que ser **completo** e seguido de um
  separador (vírgula, dois-pontos, hífen, espaço). `Thorinzinho` não casa com `Thorin`.
- O **registro** ainda é configurado; quando o recrutamento via `PlayerParty` entrar, os
  villagers recrutados serão adicionados a ele.
- A resposta é uma linha **local** no chat (`ChatMenu._AddNewTextLine`), com o nome como autor;
  em co-op ela ainda não é replicada.

**Por quê:**

- O chat do ASKA é o chat da sessão (Photon), usado por jogadores em co-op.
- Processar todas as mensagens causaria respostas indesejadas, custo de LLM e confusão.
- O prefixo dá controle e o nome permite conversa natural.
- A lista de nomes evita sequestrar uma mensagem que só começa com uma palavra qualquer.

**Alternativas:** capturar tudo (rejeitado); janela de chat própria (mais trabalho de UI, mas
pode virar opção no futuro).

**Em aberto:** se a mensagem endereçada deve ser removida do chat global em co-op; como o
cliente vê a resposta do companheiro (RPC) na fase de rede.

---

## D3 — LLM local por padrão (LM Studio + Qwen2.5-3B-Instruct)

**Decisão:** endpoint `http://127.0.0.1:1234/v1`, modelo `qwen2.5-3b-instruct`, `ApiKey`
vazia. A API é OpenAI-compatible; outros provedores entram como opção.

**Por quê:**

- Privacidade: o texto da ordem não sai da máquina.
- Custo zero e funciona offline.
- O `qwen2.5-3b-instruct` é pequeno (~2 GB) e, com **saída estruturada** (`json_schema`),
  responde JSON de forma confiável o suficiente para o contrato.
- `127.0.0.1` evita a resolução IPv6 de `localhost`.

**Alternativas:** Groq/OpenRouter (mais rápidos, mas o texto sai e exigem chave); Ollama
(compatível, mas o LM Studio é o preferido do jogador). Ficam como opção, não padrão.

**Em aberto:** temperatura ideal, tamanho do prompt, quantas trocas de memória, e o schema
final das ações.

---

## D4 — Voz: Whisper.net (STT) + sherpa-onnx/Kokoro (TTS)

**Decisão:** reutilizar a stack validada no AI Companions do Valheim.

**Por quê:**

- Já temos código, modelos e lições aprendidas (carregamento nativo, filas, cache, playback).
- STT e TTS rodam in-process, sem serviço externo.
- Kokoro-82M tem vozes PT-BR (`pf_dora`, `pm_alex`, `pm_santa`) e RTF < 1 em CPU.
- Whisper `small-q5_1` é um bom equilíbrio para PT-BR.

**Alternativas:** sherpa-onnx para STT e TTS (uma stack só) — interessante, mas exige validar
modelo Whisper ONNX/Zipformer PT-BR e empacotamento; fica como spike se houver problema.

**Em aberto:** modelos exatos no pacote (tamanho), VAD, e como lidar com o eco/reverb do ASKA.

---

## D5 — PT-BR + EN desde o início

**Decisão:** todas as strings (UI, falas, prompts, README) em arquivos de tradução com chaves
estáveis; PT-BR é o padrão, EN é o segundo.

**Por quê:**

- O público do Nexus é internacional; o jogador é PT-BR.
- Fazer i18n depois custa caro (strings espalhadas, prompts duplicados).
- O prompt do LLM também segue o idioma do jogo.

**Em aberto:** usar o sistema de localização do ASKA ou um JSON próprio. Começamos com JSON
próprio (simples, testável); se o jogo tiver uma API estável, migramos.

---

## D6 — Single-player primeiro, co-op com autoridade em mente

**Decisão:** a primeira versão funcional é single-player. Mas todo estado de IA/ordem é
desenhado para ser **host-authoritative**: o host executa, o cliente pede.

**Por quê:**

- O ASKA é co-op e o jogador joga em dupla; um mod que quebra o save/rede é inaceitável.
- Decidir autoridade depois gera refatoração e bugs de desync.
- A `PlayerParty` já é um `SimulationBehaviour` com RPCs; vamos usar o caminho nativo.

**Em aberto:** o que o cliente vê (interpolação? cópia do estado?), como o dono do companheiro
é definido, e o comportamento quando o host sai.

---

## D7 — Sem a base do Offline Companions

**Decisão:** não portar jobs, movimento, combate, casa, stamina, etc. do mod do Valheim.
Só a **camada de IA/voz** (LLM, intenção, persona, memória, STT/TTS) é reaproveitada.

**Por quê:**

- O ASKA já tem esses sistemas, e eles são melhores integrados ao jogo.
- Reimplementar a base criaria dois cérebros disputando o mesmo villager.
- O escopo fica viável: uma camada de intenção + fala sobre o que o jogo já faz.

---

## D8 — Nada de “pronto” sem teste em jogo

**Decisão:** status `tentativa` até o jogador confirmar em jogo; o log precisa mostrar a versão
carregada.

**Por quê:**

- IL2CPP é cheio de armadilhas que só aparecem em runtime (AOT inlining, interop, crashes
  nativos).
- “Compilou” não prova nada.
- O jogador é o único que pode rodar o jogo e confirmar o comportamento.

---

## Questões abertas (para o jogador)

1. Como o recrutamento deve acontecer na UI/interação?
2. Quantos companheiros ao mesmo tempo?
3. O companheiro deve continuar com o trabalho da vila quando não estiver seguindo?
4. O chat deve aceitar o nome sem vírgula? E em co-op, a mensagem fica visível para todos?
5. Voz padrão: feminina (`pf_dora`) ou masculina (`pm_alex`)?
6. O painel deve ser próprio ou integrar com o ASKA UI?
