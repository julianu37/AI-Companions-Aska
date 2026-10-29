# Changelog

Todas as mudanças relevantes do projeto. Formato baseado em Keep a Changelog;
versionamento semântico (`MAJOR.MINOR.PATCH`).

## [0.1.0] — 2026-10-28

### Adicionado

- Estrutura inicial do repositório `AI Companions - ASKA`.
- Equipe de agentes OpenCode (lead + 10 especialistas) em `.opencode/agents/`.
- Skills técnicas em `.opencode/skills/` (BepInEx, IL2CPP, RE, sistemas do ASKA, Unity 6,
  LLM, voz, co-op, QA/release, branches, localização).
- Documentação: visão, roadmap, arquitetura, design, branches, testes, referências e notas
  de engenharia reversa (`docs/re/`).
- Solução C# `net6.0` e plugin BepInEx 6 IL2CPP mínimo (`Plugin.cs`, `ModConfig`,
  `EnvironmentReport`), que carrega e registra o ambiente.
- Config inicial (`General`, `AI`, `Voice`, `UI`) e scripts de build/verificação.
- Licença MIT, notices e créditos de terceiros.

### Notas

- **Não há gameplay** nesta versão: sem chat, LLM, voz ou ordens.
- **Carregamento confirmado em jogo em 2026-10-28** (log + `.cfg` criados); evidência em
  [`docs/re/m0-load-verification.md`](docs/re/m0-load-verification.md).
- Próximas branches previstas: `feat/chat-hook`, `feat/llm-intent`, `feat/recruit-follow`,
  `feat/voice-stt`, `feat/voice-tts`.
