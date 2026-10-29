---
name: Fluxo de branches e commits
description: Política de branches, commits convencionais, merges --no-ff, tags de release e tratamento de spikes que falham. Use antes de criar branch, commitar, mergear ou publicar.
---

# Fluxo de branches e commits

`main` é a linha de release. **Toda** mudança de código/recurso nasce em uma branch e só chega
à `main` por merge revisado.

## Quando usar

Antes de qualquer `git add`/`commit`, ao iniciar uma feature/fix/spike, ao preparar um merge ou
release, ou ao revisar o histórico.

## Nomes

| Prefixo | Uso |
|---|---|
| `feat/` | funcionalidade |
| `fix/` | correção |
| `spike/` | experimento/RE (pode não ser mergeado) |
| `docs/` | documentação |
| `chore/` | build/infra/dependências |
| `test/` | harness/testes |

## Commits

`tipo(escopo): descrição no imperativo`

```
feat(chat): captura ChatMenu.SendPlayerMessage com prefixo /c
fix(llm): fallback para json_object quando o schema é recusado
docs(re): registra a superfície de PlayerParty
chore(build): adiciona target DeployToPlugins
spike(cpp2il): dump inicial do GameAssembly (não mergeado)
```

## Fluxo

```powershell
git switch main
git pull --ff-only
git switch -c feat/<slug>
# ... implementa, builda, testa ...
git add -A
git commit -m "feat(escopo): ..."
git switch main
git merge --no-ff feat/<slug>
git push origin main
git branch -d feat/<slug>
git push origin --delete feat/<slug>
```

## Regras

- Nunca commitar segredos, `Environment.props`, modelos, logs, saves ou binários do jogo.
- Um commit = uma mudança coerente; sem “wip” na `main`.
- Merge `--no-ff`; apague a branch depois.
- Tag `vX.Y.Z` só na `main`, depois da confirmação em jogo.
- Spike que falhou: registre em `docs/re/dead-ends.md`; mantenha a branch se tiver valor
  histórico, mas não mergeie código morto.
- Se a `main` quebrar, pare e conserte antes de qualquer outra coisa.

## Referências

- `docs/branches.md` — a política completa do projeto.
- `references/commit-examples.md` — exemplos de mensagens e corpo.
