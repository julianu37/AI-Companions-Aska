---
name: LLM local para ASKA
description: Integração com LM Studio/OpenAI-compatible para ordens e conversa — endpoint, modelo qwen2.5-3b-instruct, JSON schema, parser de intenção, persona, memória, fallback, latência e privacidade.
---

# LLM local para ASKA

O companheiro entende linguagem natural e responde em uma frase curta. O padrão é **local**:
LM Studio em `http://127.0.0.1:1234/v1`, modelo `qwen2.5-3b-instruct`.

## Quando usar

Sempre que o trabalho envolver: prompt, chamada HTTP, JSON schema, parser de intenção,
personalidade, memória, teste de conexão, fallback de provedor, latência ou privacidade.

## Contrato inicial

```json
{
  "action": "follow | stop | come | attack | gather | equip | drop | inventory | none",
  "reply": "uma frase curta, falada",
  "target": "nome do alvo ou null",
  "resource": "madeira | pedra | ... ou null",
  "amount": 10
}
```

- **Saída estruturada:** `response_format` `json_schema` strict, `additionalProperties: false`,
  `enum` de ações (fonte única no código).
- **Fallback:** `json_object` -> sem formato, uma tentativa cada, só em recusa de formato.
- **Parser tolerante:** aceita aliases e campos ausentes; valida contra o jogo; nunca inventa.
- **Prompt:** persona + memória (texto no system) + contrato no fim; `reply` curto.
- **Idioma:** segue o idioma do jogo (PT-BR/EN nesta fase).

## Regras

- **Nunca** execute ação fora da lista fechada.
- **Nunca** bloqueie a main thread; HTTP em `Task` de fundo + fila drenada no `Update`.
- **Sempre** timeout, cancelamento e mensagem de “LLM indisponível”.
- **Sempre** `127.0.0.1`, não `localhost` (IPv6 custa ~2,5 s).
- **Nunca** logar a API key; mascarar.
- **Privacidade:** LM Studio local por padrão; endpoint remoto é opção explícita.
- **Modelos pequenos:** a saída estruturada é o que faz o `qwen2.5-3b` responder JSON
  confiável; não remova sem medir.
- **Teste:** harness do parser com casos reais + teste em jogo da ordem.

## Referências

- `references/lm-studio.md` — instalação, servidor, endpoint, troubleshooting.
- `references/intent-schema.md` — schema, ações, aliases, erros.
- `references/prompt-design.md` — persona, memória, contrato, latência.
