# RE — ambiente do ASKA nesta máquina

**Status:** verificado em **2026-10-28** na instalação local. Serve como linha de base; um
update do jogo invalida estes números.

---

## 1. Identidade do jogo

| Item | Valor | Como verificar |
|---|---|---|
| Steam app id | `1898300` | `appmanifest_1898300.acf` |
| Nome | `ASKA` | idem |
| `buildid` | `25834809` | idem |
| `lastupdated` | `1793225737` (epoch) | idem |
| Instalação | `C:\Program Files (x86)\Steam\steamapps\common\ASKA` | `installdir` |
| Executável | `Aska.exe` | — |
| Versão do jogo | `Application.version = 0.4`; `Player.log`: `1.48.0910261842._PC.Release` | log do jogo |
| Engine | Unity **6000.3.12f1** | `LogOutput.log` |
| Scripting | **IL2CPP** | `GameAssembly.dll`, `Aska_Data\il2cpp_data` |
| Metadata | **v39** (`AF 1B B1 FA`, versão `0x27`) | header do `global-metadata.dat` |
| Timestamp do binário | **2026-10-09 16:10:14 UTC** | PE `TimeDateStamp` de `GameAssembly.dll` |

### Comandos

```powershell
# appmanifest
Get-Content "C:\Program Files (x86)\Steam\steamapps\appmanifest_1898300.acf" |
  Select-String '"name"|"buildid"|"lastupdated"|"installdir"'

# versão do Unity / BepInEx
Get-Content "C:\Program Files (x86)\Steam\steamapps\common\ASKA\BepInEx\LogOutput.log" -TotalCount 12

# metadata IL2CPP (magic + versão)
$p = "C:\Program Files (x86)\Steam\steamapps\common\ASKA\Aska_Data\il2cpp_data\Metadata\global-metadata.dat"
$b = [IO.File]::ReadAllBytes($p)[0..7]
($b | ForEach-Object { $_.ToString('X2') }) -join ' '

# timestamp do PE
$f = "C:\Program Files (x86)\Steam\steamapps\common\ASKA\GameAssembly.dll"
$fs = [IO.File]::OpenRead($f); $br = New-Object IO.BinaryReader($fs)
$fs.Position = 0x3C; $pe = $br.ReadInt32(); $fs.Position = $pe + 8; $ts = $br.ReadUInt32()
$br.Close(); $fs.Close()
[DateTimeOffset]::FromUnixTimeSeconds($ts).UtcDateTime
```

## 2. BepInEx

| Item | Valor |
|---|---|
| Versão | **6.0.0-be.791** |
| Commit | `b782da4ac54e1072d2857f4a2f77c80c3ef08005` |
| Runtime | **.NET 6.0.7** |
| Pasta | `...\ASKA\BepInEx` |
| Core | `...\BepInEx\core` (BepInEx.Core, BepInEx.Unity.IL2CPP, Il2CppInterop, 0Harmony, Cpp2IL) |
| Interop | `...\BepInEx\interop` (gerado no 1º boot; inclui `Assembly-CSharp.dll`) |
| Unity libs | `...\BepInEx\unity-libs` |
| Plugins | `...\BepInEx\plugins` |
| Log | `...\BepInEx\LogOutput.log` |
| Log do Unity/jogo | `%USERPROFILE%\AppData\LocalLow\Sand Sailor Studio\Aska\Player.log` |
| Config | `...\BepInEx\config` |

O BepInEx foi instalado pelo **Vortex**; existe um `vortex.deployment.aska-bepinex-new.json` na
raiz do jogo. O log de **2026-10-28** confirma **1 plugin** (`AI Companions - ASKA 0.1.0`);
antes do nosso, o mesmo log mostrava 0 plugins.

## 3. Formato das interop assemblies

As DLLs em `BepInEx\interop` são **wrappers gerados** pelo Il2CppInterop:

- expõem tipos, campos, propriedades e assinaturas de métodos;
- cada método chama `IL2CPP.il2cpp_runtime_invoke` / `GetIl2CppMethodByToken`;
- **não** contêm o corpo C# original;
- `MethodAddressToToken.db` / `MethodXrefScanCache.db` ajudam a mapear endereços.

Consequência: `ilspycmd -t <tipo>` serve para **superfície** (nomes, assinaturas, campos), não
para ler a lógica. Para lógica: **Cpp2IL** (metadata v39) ou análise nativa (Ghidra/IDA) ou um
probe em runtime.

## 4. Build do plugin

| Item | Valor |
|---|---|
| Target | `net6.0` |
| SDK | .NET 8.0.401 / 10.0.402 (ambos instalados) |
| Referências | `BepInEx\core\*`, `BepInEx\interop\*`, `BepInEx\unity-libs\*` |
| Saída | `src\AICompanionsAska\bin\Release\net6.0\AICompanionsAska.dll` |
| Deploy | `...\ASKA\BepInEx\plugins\AI-Companions-Aska\` |

## 5. Riscos conhecidos do ambiente

- **Atualização do jogo** pode mudar a metadata (v39 -> v40+) e renomear/remover membros; o
  plugin precisa falhar de forma legível, não em crash nativo.
- **Vortex** pode mover/reinstalar o BepInEx; conferir o caminho e a versão após updates.
- **Duas cópias do plugin** (Vortex + deploy manual) carregam o mesmo GUID duas vezes.
- **Smart App Control** (se ativo na máquina) pode bloquear DLL recém-compilada; a solução é
  mudar a versão/hash e reconstruir. Verificar `LogOutput.log`.
- **Antivírus/EDR** pode bloquear a DLL ou os nativos de voz; excluir a pasta do jogo se
  necessário (com cautela).

## 6. O que ainda não sabemos

- Se o `ChatMenu.SendPlayerMessage` é o único caminho de mensagem do jogador (há também
  `NetworkChatSession`/Photon Chat).
- Como a `PlayerParty` adiciona/remove villagers em runtime (o interop mostra `SetTaskAgent`,
  `_partyMembers`, `OnVillagerAdded`, mas não a lógica).
- Se o `DialogueMenu.Display` funciona para qualquer `Character` (villager) sem efeitos
  colaterais.
- O comportamento exato em co-op (autoridade, RPCs, persistência).

Cada item vira um probe/spike com resultado em `docs/re/`.
