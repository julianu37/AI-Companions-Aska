# Metadata IL2CPP v39

## Identificação

```powershell
$p = "C:\...\ASKA\Aska_Data\il2cpp_data\Metadata\global-metadata.dat"
$b = [IO.File]::ReadAllBytes($p)[0..7]
($b | ForEach-Object { $_.ToString('X2') }) -join ' '
```

- Magic: `AF 1B B1 FA`
- Versão: `0x27` = **39**
- Unity: **6000.3.12f1**

## O que muda

- A versão da metadata define o layout dos headers/tabelas. Ferramentas antigas podem recusar
  ou ler errado.
- Il2CppDumper antigo pode não suportar v39; use **Cpp2IL** atualizado ou o dump do BepInEx.
- O BepInEx be.791 inclui suporte a metadata recente (Unity 6+), mas o interop gerado continua
  sendo uma projeção lossy.

## Diagnóstico

- `Cpp2IL` falhou: confira a versão; tente flags de metadata; veja o log.
- `ilspycmd` abre o interop, mas não o `GameAssembly.dll`: normal (não é .NET).
- `MissingMethodException` após update: membro removido/renomeado; regenere o interop e compare.

## Limitações

- A metadata não contém corpos C#; contém o suficiente para o Cpp2IL reconstruir IL/pseudocódigo.
- Nomes podem ser ofuscados/estripados; confie em assinatura/estrutura.
- ScriptableObjects (FSM) podem não ter corpo legível; use probe.

## Referências

- Cpp2IL: https://github.com/SamboyCoding/Cpp2IL
- BepInEx: https://github.com/BepInEx/BepInEx
- Il2CppInterop: https://github.com/BepInEx/Il2CppInterop
