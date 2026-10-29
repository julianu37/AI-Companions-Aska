# Exemplos de commit

## Feature

```text
feat(chat): captura ChatMenu.SendPlayerMessage com prefixo /c

O chat do ASKA passa por ChatMenu.SendPlayerMessage(string). O patch só
processa mensagens endereçadas (/c ou nome do companheiro) para não
sequestrar o chat em co-op.

Refs: docs/re/chat-and-input.md
```

## Fix

```text
fix(llm): cai para json_object quando o schema é recusado

O LM Studio pode retornar 400 em response_format json_schema com modelos
que não suportam o modo. A cadeia tenta json_schema -> json_object -> sem
formato, uma vez cada, e loga a queda.

Refs: docs/re/dead-ends.md
```

## Docs

```text
docs(re): registra a superfície de PlayerParty

Levantamento com ilspycmd em 2026-10-28: _partyMembers, _followQuest,
SetTaskAgent. Comportamento ainda não confirmado em runtime.
```

## Chore

```text
chore(build): adiciona target DeployToPlugins

Copia a DLL para BepInEx\plugins\AI-Companions-Aska depois do build.
Environment.props é local e gitignored.
```

## Spike

```text
spike(cpp2il): dump inicial do GameAssembly (não mergeado)

Cpp2IL metadata v39; dump em _explore/cpp2il_out. Ainda não legível para
Villager; próximo passo é Ghidra no método X.

Refs: docs/re/environment.md
```

## Regras

- Imperativo, curto, específico.
- Corpo explica **por que**, não o diff.
- Um assunto por commit.
- Sem “wip” na `main`.
- Sem segredos/modelos/logs.
