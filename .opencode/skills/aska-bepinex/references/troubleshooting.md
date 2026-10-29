# Troubleshooting do plugin

## O plugin não aparece no log

- Confirme que o DLL está em `BepInEx\plugins\AI-Companions-Aska\`.
- Confirme que o arquivo não está com extensão `.dll.off`/`.disabled`.
- Confirme que o BepInEx está ativo (`doorstop_config.ini`, `winhttp.dll`).
- Veja `BepInEx\LogOutput.log` e `BepInEx\ErrorLog.log`.
- Se o BepInEx não carrega nenhum plugin, o problema é a instalação do BepInEx, não o mod.

## `FileLoadException` / `0x800711C7`

- Smart App Control/antivírus bloqueou a DLL.
- Mude a versão (hash novo) e reconstrua; ou exclua a pasta do jogo no antivírus (com cautela).
- Confirme que a DLL não está corrompida/parcial.

## `MissingMethodException`

- O jogo atualizou e removeu/renomeou um membro do interop.
- A falha pode ocorrer no **JIT do método inteiro**, não no call site; isole a referência em um
  método separado ou remova.
- Regenere o interop (rode o jogo uma vez) e confira a assinatura com `ilspycmd`.

## `Handle is not initialized`

- Patch em lifecycle cedo de `Interaction`/prefab.
- Mova para `Spawned()`/`Update()` posterior; não patche `Initialize`.

## `0x80131506` / `Fatal error. Internal CLR error`

- `PatchAll()`/atributo resolvendo tipo do jogo cedo demais.
- Remova `PatchAll()` e os `[HarmonyPatch(typeof(...))]`; patche por alvo com `try/catch` e uma
  lambda para o `typeof`.

## `coreclr.dll+0x1d1fdd`

- Crash nativo (frequentemente família de inventário ou wrapper velho).
- Mapeie o offset com Cpp2IL/WER; remova o patch/caminho.
- Não tente “consertar” com try/catch: o throw é no trampoline.

## O patch não dispara

- AOT inlining: método pequeno/privado/single-caller.
- Alvo errado (tipo derivado vs base).
- Assinatura errada (parâmetros diferentes).
- Fire-verifique; troque para virtual/público multi-caller.

## Config não é criada

- `Config.Bind` não foi chamado no `Load()`.
- Exceção antes do bind; veja o log.
- Permissão de escrita em `BepInEx\config`.

## Mudança no `.cfg` não faz efeito

- BepInEx não relê sozinho. Chame `Config.Reload()` com throttle.
- A chave foi lida para um campo estático no boot; leia no uso.

## Versão errada no log

- O jogo carregou outra cópia (Vortex + manual).
- O build não copiou para o `DeployDir` certo.
- O `PluginInfo.Version` e o `<Version>` estão diferentes.
- A DLL está bloqueada e o Windows não substituiu.

## O jogo trava ao abrir

- Patch em lifecycle cedo, state-sync do Fusion, inventário, ou `PatchAll`.
- Remova o patch mais recente e confirme; depois isole por bissecção de patches.
- Veja `ErrorLog.log` e o último `[AIC]` antes do travamento.

## O jogo atualizou

- Rode uma vez para regenerar o interop.
- Revalide as assinaturas usadas (`ilspycmd`).
- Se um tipo sumiu, degrade a feature com log claro.
- Registre a mudança em `docs/re/`.
