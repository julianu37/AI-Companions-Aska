---
name: QA e release do mod
description: Matriz de testes, leitura de LogOutput.log, triagem de crash nativo, performance, versionamento, empacotamento e release gate. Use antes de mergear, publicar ou declarar uma feature pronta.
---

# QA e release

O critério final é **confirmado em jogo pelo jogador**. Este skill cobre o que testar, como ler
o log e o que bloqueia um release.

## Quando usar

Antes de mergear na `main`, antes de publicar, ao investigar crash/stutter, ou ao revisar uma
entrega de outro agente.

## Checklist de merge

- [ ] `dotnet build AICompanionsAska.sln -c Release` passa.
- [ ] Plugin carrega e o log mostra a versão correta.
- [ ] Casos da fase testados (ou marcados como pendentes).
- [ ] Sem segredos/modelos/logs/`Environment.props` no diff.
- [ ] Docs e `AGENTS.md` atualizados.
- [ ] Branch com um assunto; merge `--no-ff`.

## Checklist de release

- [ ] Versão igual no plugin e no `.csproj`.
- [ ] `CHANGELOG.md` + release notes.
- [ ] README (requisitos, instalação, controles, config).
- [ ] `LICENSES/` com BepInEx/Il2CppInterop/Whisper/sherpa/Kokoro/espeak.
- [ ] Pacote testado em pasta limpa.
- [ ] Tag na `main` + GitHub Release.
- [ ] Nexus (se aprovado): descrição, requisitos, créditos, permissões.
- [ ] Nenhuma feature não testada exposta (mesmo com default `false`).

## Triagem de log

| Sintoma | Provável causa | Ação |
|---|---|---|
| `MissingMethodException` | membro removido/renomeado ou JIT | remover referência/isolar método |
| `Handle is not initialized` | lifecycle cedo de `Interaction` | patchar `Spawned`/`Update` |
| `coreclr.dll+0x1d1fdd` | crash nativo (inventário/interop) | WER + Cpp2IL; remover caminho |
| `0x80131506` | `PatchAll`/atributo | patch por alvo com try/catch |
| patch sem log | AOT inlining | trocar método/fire-verify |
| stutter | trabalho por frame | throttle/Stopwatch |

## Referências

- `references/test-matrix.md` — casos por fase.
- `references/log-triage.md` — padrões e comandos.
- `references/release-checklist.md` — pacote, Nexus, licenças.
