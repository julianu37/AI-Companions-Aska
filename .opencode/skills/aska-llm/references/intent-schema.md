# Intenção — schema e parser

## Schema (exemplo)

```json
{
  "type": "object",
  "properties": {
    "action": {
      "type": "string",
      "enum": ["follow", "stop", "come", "attack", "gather", "equip", "drop", "inventory", "none"]
    },
    "reply": { "type": "string" },
    "target": { "type": ["string", "null"] },
    "resource": { "type": ["string", "null"] },
    "amount": { "type": ["integer", "null"] }
  },
  "required": ["action", "reply", "target", "resource", "amount"],
  "additionalProperties": false
}
```

- `strict: true`, `additionalProperties: false`.
- O `enum` é a **fonte única** de ações; ao adicionar uma ação, atualize o schema **e** o
  `CompanionOrders`.
- `reply` curto (1 frase), porque é falado.

## Parser

- Aceitar JSON em code fence/whitespace.
- Aceitar aliases (`seguir` -> `follow`, `parar` -> `stop`, `vir` -> `come`, `atacar` ->
  `attack`, `coletar` -> `gather`).
- `amount`: número ou string numérica; `null`/0 = não especificado.
- `target`/`resource`: string ou `null`; nunca inventar.
- Se o JSON falhar: tentar extrair o primeiro objeto `{...}`; se ainda falhar, tratar como
  conversa (`none`) com o texto bruto? **Não**: melhor recusar com “não entendi” e logar.
- Se `action` desconhecida: `none` + log.
- Se `action` e `resource` discordarem numa coleta: o **recurso** manda.

## Ações iniciais

| Ação | Campos | Observação |
|---|---|---|
| `follow` | — | seguir |
| `stop` | — | parar |
| `come` | — | vir |
| `attack` | `target` | criatura |
| `gather` | `resource`, `amount` | recurso |
| `equip` | `target` | item do inventário |
| `drop` | `target`, `amount` | item do inventário |
| `inventory` | — | abrir inventário (a confirmar) |
| `none` | `reply` | conversa |

## Erros e fallback

1. `json_schema` strict.
2. Se o provedor recusar o formato (HTTP 400/erro citando `response_format`): `json_object`.
3. Se recusar de novo: sem `response_format` (formato antigo).
4. Só uma tentativa por formato; não repetir o mesmo corpo.
5. Timeout/5xx/rede: **não** repetir; reportar.

## Validação no jogo

- `attack`: alvo precisa existir no alcance/mira.
- `equip`/`drop`: item precisa estar no inventário do companheiro.
- `gather`: recurso reconhecido; `amount` é alvo total.
- `none`: só fala.

## Testes do parser (harness)

- JSON válido de cada ação.
- Code fence + texto ao redor.
- `target: null`, `amount: null`.
- `action` desconhecida.
- `action`/`resource` discordantes.
- JSON truncado.
- Acentuação PT-BR.
