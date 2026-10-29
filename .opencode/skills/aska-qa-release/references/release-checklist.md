# Release checklist

## Código

- [ ] `dotnet build AICompanionsAska.sln -c Release` limpo.
- [ ] Versão no `PluginInfo.Version` == `<Version>`.
- [ ] Sem `TODO`/feature não testada exposta.
- [ ] `VerboseLog` default `false` no release.
- [ ] Sem segredos/caminhos pessoais.

## Teste

- [ ] Carregamento no log.
- [ ] Matriz da fase confirmada em jogo.
- [ ] Save/reload.
- [ ] Co-op (se a fase promete).
- [ ] Performance sem stutter.

## Pacote

- [ ] DLL + dependências gerenciadas.
- [ ] Nativos win-x64 (voz).
- [ ] Modelos (se incluídos).
- [ ] `LICENSES/` (BepInEx, Il2CppInterop, Whisper, sherpa, Kokoro, espeak).
- [ ] `Translations/`, `Speech/`.
- [ ] README, CHANGELOG, guia de instalação.

## Publicação

- [ ] Tag `vX.Y.Z` na `main`.
- [ ] GitHub Release com o zip.
- [ ] Nexus (se aprovado): descrição, requisitos, créditos, permissões.
- [ ] Link da wiki/README.
- [ ] Aviso de que o ASKA precisa do BepInEx 6 IL2CPP.

## Pós-release

- [ ] Monitorar issues/comentários.
- [ ] Registrar bugs em `docs/` com versão/log.
- [ ] Próxima branch a partir da `main`.

## Proibido

- Publicar feature não testada (mesmo com default `false`).
- Publicar sem licenças de terceiros.
- Publicar com segredo no `.cfg`/log.
- Publicar DLL de uma versão diferente da tag.
