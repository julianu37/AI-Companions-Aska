# Prompt — persona, memória e contrato

## Estrutura

```text
[system]
Você é {nome}, um companheiro de {jogador} em ASKA.
Personalidade: {persona}.
Idioma: {idioma do jogo}.
Regras: responda em uma frase curta; nunca invente itens/criaturas; ...
Memória recente:
Jogador: ...
{nome}: ...

Contrato (responda SOMENTE JSON válido):
{schema + lista de ações + exemplos}
```

- O **contrato fica no fim** (o modelo lê por último).
- A memória vai como **texto** no system, não como mensagens `user`/`assistant` extras (isso
  empurra o contrato para longe e confunde modelos pequenos).
- Persona em texto livre ganha do preset.
- `reply`: 1 frase, até ~15 palavras, porque é falada.

## Regras do prompt

- Ações e chaves em **inglês** (contrato estável); o texto do jogador/resposta no idioma do
  jogo.
- Proíba inventar alvo; mande usar `null` quando não houver.
- Proíba ação fora do `enum`.
- Diga que o número em `gather` é um **alvo total**.
- Exemplos curtos de `follow`, `gather`, `none`.
- Não inclua segredos, caminhos, logs ou dados pessoais.

## Memória

- Últimas N trocas por companheiro (default 4; 0 desliga; teto 12).
- Limpar na morte, dismiss e troca de mundo.
- Formato: `Jogador: ...` / `{nome}: ...`.
- Não guardar áudio; só texto.

## Latência

- Prompt ~1.500–2.000 tokens é o suficiente; não despeje o manual.
- `max_tokens` ~200–300.
- Temperatura 0,2–0,4 para ordens; 0,6–0,8 para conversa (se separar).
- Meça ms/tokens no log.
- `127.0.0.1`, não `localhost`.

## Privacidade

- LM Studio local: nada sai.
- Provedor remoto: avisar na UI; a voz continua local.
- Não logar prompt inteiro em produção (pode conter conversa); `VerboseLog` para depurar.

## Exemplo de contrato (resumo)

```text
Responda SOMENTE com um objeto JSON:
{"action": "...", "reply": "...", "target": null, "resource": null, "amount": null}

Ações permitidas: follow, stop, come, attack, gather, equip, drop, inventory, none.
- Em gather, "amount" é o total desejado (ex.: 10 madeiras).
- Em attack/equip/drop, preencha "target" com o nome citado; se não houver, use null.
- Em conversa, action = "none" e reply com uma frase curta.
```
