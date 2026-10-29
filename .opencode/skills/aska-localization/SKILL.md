---
name: Localização PT-BR/EN
description: Como manter interface, falas, prompts e documentação em PT-BR e EN — chaves estáveis, JSON de tradução, idioma do jogo, prompts por idioma, identidade locale-invariant e checklist de release.
---

# Localização PT-BR/EN

O mod nasce em **PT-BR + EN**. Strings espalhadas pelo código são dívida técnica; centralize
desde o começo.

## Quando usar

Sempre que adicionar texto de UI, fala do companheiro, mensagem de erro, prompt de LLM, README
ou release notes.

## Estrutura proposta

```
Translations/
  pt-BR.json
  en.json
Speech/
  pt-BR.json
  en.json
```

- Chaves estáveis (`hc_ai_follow`, `hc_ai_no_target`, `hc_ai_llm_offline`).
- Placeholders `{0}`, `{1}` documentados.
- PT-BR é o padrão; EN é fallback.
- Falas do companheiro em arquivo separado da UI (para voz/TTS).

## Regras

- **Nunca** hardcodar texto visível no `Plugin.cs`/painel.
- **Nunca** traduzir nomes de ação internos (`follow`, `attack`): eles são contrato.
- O **prompt** segue o idioma do jogo; o contrato JSON continua em inglês (chaves/ações).
- Nomes de itens/criaturas: usar o nome localizado do jogo quando existir; comparar também por
  id/prefab, nunca só por string traduzida.
- Identidade locale-invariant: para lógica, use id/prefab/asset name; para exibição, use o nome
  localizado.
- Mantenha os dois arquivos com o mesmo conjunto de chaves; um teste/harness deve falhar se
  faltar chave.
- README: PT-BR primeiro, seção EN no fim.
- Voz: o idioma do TTS acompanha o idioma do jogo; uma voz de outro idioma não é usada.

## Checklist

- [ ] Todas as chaves existem em PT-BR e EN.
- [ ] Nenhum texto hardcoded na UI.
- [ ] Placeholders consistentes.
- [ ] Nomes de itens/criaturas resolvidos pelo jogo, não por string fixa.
- [ ] Prompt no idioma do jogo; contrato JSON intacto.
- [ ] README/release notes nos dois idiomas (ou seção EN).
