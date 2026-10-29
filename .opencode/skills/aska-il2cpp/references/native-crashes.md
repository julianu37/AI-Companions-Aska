# Crash nativo — diagnóstico

## Sintomas

- O processo morre sem stack gerenciada.
- `LogOutput.log` termina no meio; `ErrorLog.log` pode ter `Fatal error. Internal CLR error.`
- WER aponta `coreclr.dll+0x1d1fdd` ou similar.
- `%LOCALAPPDATA%\CrashDumps\Aska.exe.*.dmp` pode existir.

## Causas comuns

1. Patch em método da família de inventário (`Item`, `ItemCollection`, `ItemEventContext`).
2. Wrapper velho de objeto de mundo mantido após sair/recarregar.
3. Patch em lifecycle cedo (`Interaction`/prefab).
4. `PatchAll()`/atributo resolvendo tipo cedo.
5. Conflito com outro mod que patcheia o mesmo método.

## Procedimento

1. **Reproduza** com o mínimo de mods (desabilite outros).
2. **Localize** a última linha do log e o offset do WER.
3. **Mapeie** o offset para um método:
   - Cpp2IL no `GameAssembly.dll` (metadata v39);
   - `dumpbin /headers`/Ghidra para o RVA;
   - `MethodAddressToToken.db` do interop.
4. **Remova** o caminho suspeito e confirme que o jogo carrega.
5. **Documente** em `docs/re/dead-ends.md` com sintoma, causa e alternativa.

## O que NÃO fazer

- Não envolver a chamada em `try/catch` e achar que resolve: o throw é no trampoline/glue.
- Não insistir no mesmo método com “mais um parâmetro”.
- Não usar `PatchAll()` para “ver se pega”.
- Não atribuir ao jogo sem provar: bissecção de plugins primeiro.

## Prevenção

- Patch mínimo, alvo seguro, fire-verify.
- Não cachear wrappers por mundo sem limpeza.
- Testar com os mods populares (Aska Plus, ASKA UI, Smart Villagers).
- Logar a versão do jogo/plugin no boot.
