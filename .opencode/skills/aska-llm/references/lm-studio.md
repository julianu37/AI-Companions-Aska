# LM Studio — setup e endpoint

## Instalação

1. Baixe o LM Studio em https://lmstudio.ai/.
2. **Model Search** -> `Qwen2.5-3B-Instruct` (Q4_K_M, ~1,9 GB) -> download.
3. **Local Server** -> carregue o modelo -> **Start Server**.
4. Confirme o endpoint: `http://127.0.0.1:1234/v1`.
5. O identificador do modelo aparece no servidor (ex.: `qwen2.5-3b-instruct`).

## Teste rápido

```powershell
$body = @{
  model = "qwen2.5-3b-instruct"
  messages = @(@{ role = "user"; content = "Responda só: ok" })
  temperature = 0.2
  max_tokens = 16
} | ConvertTo-Json -Depth 5

Invoke-RestMethod -Uri "http://127.0.0.1:1234/v1/chat/completions" `
  -Method Post -ContentType "application/json" -Body $body
```

## No mod

```csharp
var payload = new {
    model = "qwen2.5-3b-instruct",
    messages = new[] {
        new { role = "system", content = systemPrompt },
        new { role = "user", content = userText }
    },
    temperature = 0.2,
    max_tokens = 220,
    response_format = new {
        type = "json_schema",
        json_schema = new { name = "companion_intent", strict = true, schema = schema }
    }
};
```

- `HttpClient` com `Timeout` de 30 s.
- Rodar em `Task` de fundo; nunca na main thread.
- `ApiKey` vazia no LM Studio; se o provedor exigir, mandar `Authorization: Bearer`.

## Troubleshooting

| Sintoma | Causa | Solução |
|---|---|---|
| conexão recusada | servidor não iniciou | Start Server no LM Studio |
| 404 | URL sem `/v1` | `http://127.0.0.1:1234/v1` |
| modelo não encontrado | nome errado | copie o id exato do servidor |
| lento (~2,5 s) | `localhost` resolve IPv6 | use `127.0.0.1` |
| resposta sem JSON | modelo não segue formato | use `response_format` json_schema strict |
| 400 `response_format` | modelo/servidor não suporta | fallback `json_object` -> sem formato |
| timeout | modelo grande/CPU | aumente timeout, reduza prompt, use GPU |

## Privacidade

- LM Studio local: o texto não sai da máquina.
- Se o jogador configurar Groq/OpenRouter/Ollama remoto, avise na UI que o texto sai.
- A voz é sempre local.
