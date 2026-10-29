---
name: ASKA BepInEx 6 IL2CPP
description: Como criar e operar um plugin BepInEx 6 IL2CPP para ASKA — BasePlugin, ciclo de vida, config, logging, interop, build net6.0, deploy/Vortex, versionamento e conflitos. Use sempre que escrever ou revisar o plugin.
---

# ASKA — BepInEx 6 IL2CPP

O ASKA é **IL2CPP** (Unity 6). O BepInEx 6 carrega plugins **.NET 6** e gera as interop
assemblies que dão acesso ao código do jogo. Todo plugin do projeto é uma classe
`BepInEx.Unity.IL2CPP.BasePlugin` decorada com `[BepInPlugin]`.

## Quando usar

Sempre que o trabalho envolver: `Plugin.cs`, `[BepInPlugin]`, `ConfigEntry`, `ManualLogSource`,
referências a `BepInEx\core`/`interop`/`unity-libs`, build/deploy, Vortex, versão do plugin ou
conflito entre mods. Combine com `aska-il2cpp` para patches e com `aska-game-systems` para a
API do jogo.

## Alvo desta máquina

- ASKA (Steam `1898300`), Unity **6000.3.12f1**, IL2CPP, metadata **v39**.
- BepInEx **6.0.0-be.791** (IL2CPP), runtime **.NET 6.0.7**.
- Jogo: `C:\Program Files (x86)\Steam\steamapps\common\ASKA`.
- Interop: `...\BepInEx\interop`; Unity libs: `...\BepInEx\unity-libs`.
- Target do plugin: **`net6.0`**, C#, `AllowUnsafeBlocks`.

## Fluxo

1. Leia `references/plugin-lifecycle.md` para o esqueleto e o ciclo de vida.
2. Configure em `references/config.md` (seções, defaults, reload, segredos).
3. Logue em `references/logging.md` (prefixo, fire-verify, sem spam).
4. Build/deploy em `references/build-deploy.md` (`Environment.props`, referências, target).
5. Se o plugin não carregar, `references/troubleshooting.md`.
6. Template pronto em `templates/PluginTemplate.cs`.

## Regras

- **`BasePlugin`**, não `BaseUnityPlugin`.
- `Load()` é main thread: nada pesado; carregue modelos/threads depois.
- **Sem segredos** no código; `ApiKey` no `.cfg` local.
- **Sem `.dll` do jogo** no repositório; referencie a instalação.
- **Um GUID, uma cópia**: não deixe Vortex + deploy manual duplicados.
- **Versão igual** no `PluginInfo.Version` e no `<Version>` do `.csproj`.
- **Fire-verify** cada patch com log; AOT inlining é silencioso.
- **Não** use `Harmony.PatchAll()` cego em tipos do jogo; patche por alvo com try/catch.
- Se uma interop sumir após update, degrade com log claro — não crash nativo.
