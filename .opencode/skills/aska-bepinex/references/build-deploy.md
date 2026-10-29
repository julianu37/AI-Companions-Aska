# Build e deploy

## `Environment.props`

`Environment.props` fica na raiz e é **gitignored**:

```xml
<Project>
  <PropertyGroup>
    <AskaDir>C:\Program Files (x86)\Steam\steamapps\common\ASKA</AskaDir>
    <DeployDir>$(AskaDir)\BepInEx\plugins\AI-Companions-Aska</DeployDir>
  </PropertyGroup>
</Project>
```

`Environment.props.example` é o modelo commitado.

## `.csproj` (resumo)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="..\..\Environment.props" Condition="Exists('..\..\Environment.props')" />
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <AssemblyName>AICompanionsAska</AssemblyName>
    <RootNamespace>AICompanionsAska</RootNamespace>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Version>0.1.0</Version>
  </PropertyGroup>

  <PropertyGroup>
    <AskaDir Condition="'$(AskaDir)' == ''">C:\Program Files (x86)\Steam\steamapps\common\ASKA</AskaDir>
    <BepInExDir>$(AskaDir)\BepInEx</BepInExDir>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="BepInEx.Core" HintPath="$(BepInExDir)\core\BepInEx.Core.dll" Private="false" />
    <Reference Include="BepInEx.Unity.IL2CPP" HintPath="$(BepInExDir)\core\BepInEx.Unity.IL2CPP.dll" Private="false" />
    <Reference Include="Il2CppInterop.Runtime" HintPath="$(BepInExDir)\core\Il2CppInterop.Runtime.dll" Private="false" />
    <Reference Include="0Harmony" HintPath="$(BepInExDir)\core\0Harmony.dll" Private="false" />
    <Reference Include="Assembly-CSharp" HintPath="$(BepInExDir)\interop\Assembly-CSharp.dll" Private="false" />
    <Reference Include="Il2Cppmscorlib" HintPath="$(BepInExDir)\interop\Il2Cppmscorlib.dll" Private="false" />
    <Reference Include="UnityEngine.CoreModule" HintPath="$(BepInExDir)\unity-libs\UnityEngine.CoreModule.dll" Private="false" />
  </ItemGroup>

  <Target Name="DeployToPlugins" AfterTargets="Build" Condition="'$(DeployDir)' != ''">
    <Copy SourceFiles="$(TargetPath)" DestinationFolder="$(DeployDir)" />
    <Message Importance="high" Text="Deployed $(TargetFileName) to $(DeployDir)" />
  </Target>
</Project>
```

## Referências

- `Private="false"` em **tudo** que vem do jogo/BepInEx; não copie DLLs do jogo para o output.
- Adicione `SandSailorStudio.dll`, `Fusion.Runtime.dll`, `UnityEngine.AudioModule.dll`,
  `UnityEngine.UI.dll`, `Unity.TextMeshPro.dll` etc. **só quando usar**.
- `System.*` do .NET 6 já vem do runtime; não leve `System.Text.Json` extra se não precisar.

## Deploy

```powershell
dotnet build AICompanionsAska.sln -c Release
```

- O target copia para `$(DeployDir)`.
- **Não** deixe duas cópias em `plugins\` (Vortex + manual) — mesmo GUID, carrega duas vezes.
- Feche o jogo antes de substituir a DLL (lock de arquivo no Windows).
- Confira a versão carregada no `LogOutput.log`.

## Versionamento

- `PluginInfo.Version` == `<Version>` do csproj.
- Bump **toda** vez que for testar uma DLL nova se o Smart App Control estiver ativo: ele
  bloqueia um hash já visto (`0x800711C7`); mudar a versão muda o hash.
- Tag `vX.Y.Z` na `main` só depois da confirmação.

## Vortex

- O Vortex instala o BepInEx e pode “possuir” a pasta; o deploy manual deve apontar para a
  mesma pasta que ele usa, ou desative o deploy do Vortex para este mod.
- O manifesto `vortex.deployment.aska-bepinex-new.json` indica o que ele gerencia.
- Se o Vortex reinstalar o BepInEx, o `plugins\` e o `config\` sobrevivem.

## Verificação

```powershell
# versão do DLL
(Get-Item .\src\AICompanionsAska\bin\Release\net6.0\AICompanionsAska.dll).VersionInfo

# versão carregada
Select-String -Path "C:\Program Files (x86)\Steam\steamapps\common\ASKA\BepInEx\LogOutput.log" `
  -Pattern "AI Companions - ASKA"
```
