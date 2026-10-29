# AI Companions - ASKA

> **Companheiros com IA para ASKA** — converse e dê ordens por **chat ou voz**, com um
> **LLM local** (LM Studio + Qwen2.5-3B-Instruct) e **voz local** (Whisper + Kokoro).
> Um projeto independente, inspirado no nosso [AI Companions do Valheim](https://github.com/julianu37/AI-Companions),
> mas construído sobre os sistemas nativos de **villagers**, **PlayerParty** e **quests** do ASKA —
> **sem** a base do Offline Companions.

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**Status:** `v0.3.0` — **LLM intent**. O plugin captura `/c ...` no chat, chama o LM Studio
local (`qwen2.5-3b-instruct`) e **entende/loga a intenção** em JSON; a cadeia foi confirmada
com self-test em jogo em 2026-10-28. Ainda **não executa ordens**, não responde no chat e não
tem voz/UI.

---

## O que o mod vai fazer

- **Conversar e comandar por chat**: uma mensagem endereçada ao companheiro vira uma ordem
  (`segue`, `para`, `colete 10 madeira`, `ataca o javali`, `equipa o machado`) ou uma resposta
  de conversa.
- **Comandar por voz**: push-to-talk, transcrição **dentro do jogo** (Whisper.net), sem serviço
  externo.
- **O companheiro fala**: TTS local (Kokoro-82M via sherpa-onnx), na posição dele no mundo.
- **Personalidade e memória**: cada companheiro tem nome, tom e um histórico curto.
- **LLM local por padrão**: LM Studio em `http://127.0.0.1:1234/v1`, modelo
  `qwen2.5-3b-instruct`; a API é compatível com OpenAI, então outros provedores podem ser
  adicionados depois.
- **Companheiro = villager recrutado**: usamos um villager existente da vila (via `PlayerParty`
  e as quests nativas), preservando movimento, inventário, sobrevivência e IA do jogo.

## Requisitos

- **ASKA** no Steam (app id `1898300`), versão atual.
- **BepInEx 6.0.0-be.791 (IL2CPP)** instalado na pasta do jogo.
  O instalador do Vortex já deixa `BepInEx\` pronto; o jogo precisa ter rodado uma vez para
  gerar as **interop assemblies**.
- **.NET SDK 8 ou 10** para compilar (o plugin tem como alvo `net6.0`, o runtime do BepInEx).
- **LM Studio** (para o LLM local) e o modelo **Qwen2.5-3B-Instruct** carregado.
- **Microfone** (só para as ordens por voz).

## Instalação

> Release de scaffold: <https://github.com/julianu37/AI-Companions-Aska/releases/tag/v0.1.0>
> (pré-release, sem gameplay).

1. Instale o **BepInEx 6 (IL2CPP)** na pasta do ASKA e abra o jogo uma vez.
2. Copie o conteúdo do release (ou a pasta `AI-Companions-Aska` do build local) para:
   `...\ASKA\BepInEx\plugins\AI-Companions-Aska\`.
3. Abra o jogo **pelo Steam** e confira `BepInEx\LogOutput.log`:
   deve aparecer a versão que você compilou/baixou (neste branch, `AI Companions - ASKA 0.2.0`)
   e `loaded successfully`.
4. (Quando o LLM entrar) abra o LM Studio, carregue o `qwen2.5-3b-instruct` e inicie o
   **Local Server** em `127.0.0.1:1234`.

## Build

```powershell
# 1. copie o exemplo e ajuste os caminhos da sua máquina
Copy-Item Environment.props.example Environment.props
# edite AskaDir se o jogo não estiver no caminho padrão

# 2. compile (o build copia o DLL para BepInEx\plugins\AI-Companions-Aska)
dotnet build AICompanionsAska.sln -c Release
```

O `Environment.props` é **local** e não vai para o repositório. O build referencia os DLLs da
**sua** instalação do jogo e do BepInEx; nenhum binário do jogo vive no repositório.

## Controles (planejados)

| Tecla | Ação |
|---|---|
| `Enter` (chat do jogo) | Digite `/<prefixo> ...` ou `Nome, ...` para falar com o companheiro |
| `V` (segurar) | Push-to-talk (configurável) |
| `I` | Painel da IA (configurável) — a partir da fase de UI |

> O prefixo `/c` está ativo desde a `0.2.0` (captura no log); as teclas de voz/painel ainda não.
> O BepInEx Configuration Manager (se instalado) usa a tecla dele; não é uma tecla do mod.

## Arquitetura (resumo)

```
Chat do jogo / voz
        │
        ▼
  captura + normalização (prefixo/nome)
        │
        ▼
  LLM local (OpenAI-compatible, JSON schema)
        │
        ▼
  intenção (action, reply, target, resource, amount)
        │
        ├── conversa  ──► fala (texto no DialogueMenu + TTS)
        └── ordem     ──► PlayerParty / quests / tasks nativas do villager
```

O companheiro é um **villager nativo** recrutado para a `PlayerParty` do jogador. A IA do mod
não substitui o movimento/combate/sobrevivência do jogo: ela escolhe **o que** o villager deve
fazer e usa as APIs nativas para executar.

## Estrutura do repositório

| Caminho | O que é |
|---|---|
| `src/AICompanionsAska/` | O plugin (C#, `net6.0`); `Plugin.cs` é a entrada |
| `src/AICompanionsAska/Core/` | Configuração (`ModConfig`) e logging (`AicLog`) |
| `src/AICompanionsAska/Diagnostics/` | Relatório de ambiente no boot |
| `src/AICompanionsAska/AI/` | LLM, intenções, ordens (próximas fases) |
| `src/AICompanionsAska/Voice/` | STT/TTS, microfone, playback (próximas fases) |
| `src/AICompanionsAska/Companion/` | Recrutamento, perfil, memória (próximas fases) |
| `src/AICompanionsAska/UI/` | Painel e HUD (próximas fases) |
| `.opencode/agents/` | Equipe de agentes que desenvolve o projeto |
| `.opencode/skills/` | Skills técnicas da equipe |
| `docs/` | Visão, roadmap, arquitetura, RE, testes e referências |
| `scripts/` | Build, deploy e verificação do ambiente |

## Fluxo de branches (regra do projeto)

- `main` é a linha de release: sempre compilável e testada.
- **Toda** feature, correção, spike ou recurso novo nasce de `main` em uma branch:
  `feat/<slug>`, `fix/<slug>`, `spike/<slug>`, `docs/<slug>`, `chore/<slug>`.
- Commits pequenos e convencionais (`feat:`, `fix:`, `docs:`, `test:`, `chore:`).
- Merge para `main` só com build + revisão + confirmação em jogo (quando aplicável);
  merge `--no-ff`, branch apagada depois.
- Nunca commitar segredos, `Environment.props`, modelos, logs, saves ou binários do jogo.

Detalhes: [`docs/branches.md`](docs/branches.md).

## Documentação

- [`AGENTS.md`](AGENTS.md) — orientação canônica da equipe de agentes.
- [`docs/vision.md`](docs/vision.md) — o objetivo do projeto.
- [`docs/roadmap.md`](docs/roadmap.md) — fases e status.
- [`docs/architecture.md`](docs/architecture.md) — como o mod funciona por dentro.
- [`docs/design.md`](docs/design.md) — decisões de design (companheiro, LLM, voz).
- [`docs/re/`](docs/re/) — engenharia reversa do ASKA/IL2CPP.
- [`docs/testing.md`](docs/testing.md) — matriz de testes e triagem de logs.

## Créditos e licença

- **ASKA** pertence à Sand Sailor Studio. Este projeto não redistribui o jogo nem assets.
- **BepInEx**, **Il2CppInterop**, **HarmonyX**, **Whisper.net**, **sherpa-onnx**, **Kokoro** e
  **Qwen** mantêm suas próprias licenças; veja [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
- As notas públicas do projeto [askamods](https://github.com/simpsonbm1/askamods) foram usadas
  como **referência factual** de modding de ASKA; nenhum código/asset foi copiado.
- Projeto licenciado sob **MIT** — veja [`LICENSE`](LICENSE).

---

# English

> **AI companions for ASKA** — talk and give orders by **chat or voice**, with a **local LLM**
> (LM Studio + Qwen2.5-3B-Instruct) and **local speech** (Whisper + Kokoro). Built on ASKA's
> native **villagers**, **PlayerParty** and **quests** — **not** on the Offline Companions base.

**Status:** `v0.3.0` — **LLM intent**. The plugin captures `/c ...` from chat, calls local LM
Studio (`qwen2.5-3b-instruct`) and **parses/logs the intent** as JSON; the chain was confirmed
with an in-game self-test on 2026-10-28. It does **not execute orders**, reply in chat or
provide voice/UI yet.

### Requirements

- **ASKA** on Steam (app id `1898300`), current build.
- **BepInEx 6.0.0-be.791 (IL2CPP)** in the game folder (Vortex installs it; run the game once
  so the interop assemblies are generated).
- **.NET SDK 8 or 10** to build (`net6.0` target, matching BepInEx's runtime).
- **LM Studio** with **Qwen2.5-3B-Instruct** for the local LLM.
- A **microphone** for voice orders.

### Build

```powershell
Copy-Item Environment.props.example Environment.props
# edit AskaDir if needed
dotnet build AICompanionsAska.sln -c Release
```

### Branch policy

`main` is release-only. Every feature/fix/spike starts from `main` on a branch
(`feat/`, `fix/`, `spike/`, `docs/`, `chore/`), is reviewed and tested, and is merged with
`--no-ff`. Never commit secrets, local props, models, logs, saves or game binaries.

See [`AGENTS.md`](AGENTS.md) and [`docs/`](docs/) for the full project guide.
