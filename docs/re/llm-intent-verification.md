# LLM intent — verificação

- **Data:** 2026-10-28
- **Plugin:** AI Companions - ASKA 0.3.0
- **LLM:** LM Studio local, `http://127.0.0.1:1234/v1`, modelo `qwen2.5-3b-instruct`
- **Objetivo:** provar que o cliente OpenAI-compatible, o schema estrito, o parser e a
  cadeia `ChatCapture` -> brain -> LLM -> drenagem funcionam com o modelo real.

## 1. Harness do parser

`dotnet run --project tests/LlmIntentHarness -c Release`:

```text
PASS  follow
PASS  seguir
PASS  para
PASS  vir
PASS  atacar
PASS  coletar
PASS  equipar
PASS  largar
PASS  inventory
PASS  none
PASS  resource manda
PASS  amount string
PASS  amount zero
PASS  code fence
PASS  texto ao redor
PASS  ação desconhecida
PASS  json invalido
PASS  vazio
PASS  ToString sem newline

19/19 checks passed.
```

## 2. Cadeia em jogo (self-test)

`AI.SelfTest=true` injeta `segue` no `ChatCapture` no boot; o brain enfileira e chama o LLM:

```text
[AIC][LLM] brain started | AI.Enabled=True | model=qwen2.5-3b-instruct |
           url=http://127.0.0.1:1234/v1 | structured=True
[AIC][LLM-SELFTEST] enfileirando 'segue' via ChatCapture (language=pt-BR, model=qwen2.5-3b-instruct)
[AIC][CHAT] addressed from selftest (5 chars): "segue"
[AIC][LLM] HTTP 200 em 226 ms | mode=JsonSchema | action=follow target=- resource=- amount=- reply="Sigamos em frente."
[AIC][LLM] intent em 226 ms (mode=JsonSchema): action=follow target=- resource=- amount=- reply="Sigamos em frente."
```

O `json_schema` estrito foi aceito na primeira tentativa (`mode=JsonSchema`). A chamada
seguinte, em uma execução anterior, levou **2.487 ms**; o cache do modelo explica a variação.

## 3. O que está provado

- O endpoint LM Studio responde no formato OpenAI.
- O schema `companion_intent` estrito é aceito pelo `qwen2.5-3b-instruct`.
- O parser transforma o JSON em `action=follow` (e nos outros 18 casos do harness).
- A cadeia `ChatCapture.Captured` -> `CompanionBrain` -> `LlmClient` -> `Drain` funciona e loga
  o intent na main thread.
- O runner (`ClassInjector` + `GameObject`) é registrado e o `Update` drena a fila.
- O self-test não bloqueia o boot (a chamada roda em `Task`).

## 4. O que ainda falta

- **Chat real -> LLM em um mundo:** o capture do chat foi confirmado em 0.2.0 e a cadeia foi
  confirmada via self-test; falta o teste integrado `/c segue` em um mundo com o LLM ligado.
  Deve ser feito na branch `feat/recruit-follow`.
- **Execução de ordens:** o intent é apenas logado; nenhum villager se move.
- **Fallback de formato:** o caminho `json_schema` funcionou; `json_object`/sem formato não foi
  exercitado com o modelo real (o código existe e o parser é o mesmo).
- **Co-op:** não testado.
- **Privacidade:** o texto do chat vai para o log só com `VerboseLog=true`; a chamada HTTP é
  local (LM Studio).
