# Ferramentas de RE — comandos

## ilspycmd (superfície)

```powershell
# listar tipos
ilspycmd -l c "C:\...\BepInEx\interop\Assembly-CSharp.dll" > classes.txt

# um tipo
ilspycmd -t SSSGame.Villager "C:\...\BepInEx\interop\Assembly-CSharp.dll" > Villager.cs

# procurar um namespace/tipo
Select-String -Path classes.txt -Pattern "SSSGame\.PlayerParty"
```

> As interop não têm corpo; `ilspycmd` mostra assinaturas/campos + chamadas
> `il2cpp_runtime_invoke`. Não procure lógica ali.

## Cpp2IL

- Baixe a release atual (suporta Unity 6/metadata v39).
- Rode no `GameAssembly.dll` + `global-metadata.dat`.
- Saída: `cpp2il_out/` com assemblies/dumps; abra no ILSpy/dnSpy.
- Se falhar, confira a versão da metadata e use `--force-binary-path`/flags documentadas.

## Ghidra / IDA

- Abra `GameAssembly.dll` (x86-64).
- Use os RVAs/símbolos do Cpp2IL para localizar métodos.
- Foque em funções pequenas chamadas por métodos conhecidos; não tente “entender tudo”.

## dnSpyEx

- Para assemblies **gerenciadas** (interop/plugins), não para o jogo nativo.
- Útil para inspecionar atributos, campos e assinaturas.

## Frida / REA

- Probe em runtime: hook de função, leitura de campo, chamada de método.
- Útil quando o dump não é legível ou o comportamento depende de estado.
- Sempre com o jogo em single-player e save descartável.

## WER / crash

```powershell
Get-ChildItem "$env:LOCALAPPDATA\CrashDumps" -Filter "Aska.exe.*.dmp" |
  Sort-Object LastWriteTime -Descending | Select-Object -First 3
```

- Abra o `.dmp` no WinDbg/Visual Studio; pegue o módulo+offset.
- Mapeie o offset com Cpp2IL/Ghidra.

## Versão do jogo sem SteamDB

```powershell
Invoke-RestMethod "https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=1898300" |
  Select-Object -ExpandProperty appnews
```

- Use para saber se houve update; não dependa de data de arquivo do `Aska.exe`.

## Interop gerado

```powershell
Get-ChildItem "C:\...\ASKA\BepInEx\interop" | Measure-Object
Get-Content "C:\...\ASKA\BepInEx\LogOutput.log" -TotalCount 15
```

- O interop é regenerado no primeiro boot após um update do jogo.
- Se um tipo sumiu, o mod precisa degradar.

## Boas práticas

- Trabalhe com uma pergunta por vez.
- Salve dumps/notas em `docs/re/`.
- Não execute binários desconhecidos.
- Não copie código sem licença; fatos com crédito.
