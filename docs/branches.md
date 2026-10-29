# Branches, commits e releases

Regra do projeto: **toda** mudança de código, recurso novo ou tentativa de correção nasce em
uma branch e só chega à `main` por merge revisado. A `main` é a linha de release.

---

## 1. Modelo

```
main ───●───────────────●──────────────●─────────►  (releases)
         \             / \            /
          ●──●──●─────●   ●──●──●────●
          feat/chat-hook  fix/order-target
```

- `main`: sempre compilável, sempre com a versão que o jogador pode testar.
- Branches de trabalho: curtas, com um objetivo, criadas a partir da `main` atualizada.
- Merge: `git merge --no-ff <branch>` (preserva o contexto da branch) e apaga a branch.
- PR: recomendado para mudanças maiores; o merge local `--no-ff` é aceitável para o dia a dia.

## 2. Nomes

| Prefixo | Uso | Exemplo |
|---|---|---|
| `feat/` | funcionalidade nova | `feat/chat-hook` |
| `fix/` | correção de bug | `fix/order-target` |
| `spike/` | experimento/RE que pode não ser mergeado | `spike/cpp2il-dump` |
| `docs/` | documentação | `docs/re-chat` |
| `chore/` | build, infra, dependências | `chore/net6-build` |
| `test/` | harness/testes | `test/intent-parser` |

Use kebab-case, curto e específico. Uma branch = um assunto.

## 3. Commits

Formato: `tipo(escopo): descrição no imperativo`.

```
feat(chat): captura ChatMenu.SendPlayerMessage com prefixo /c
fix(llm): fallback para json_object quando o schema é recusado
docs(re): registra a superfície de PlayerParty
chore(build): adiciona target DeployToPlugins
test(intent): cobre aliases de action e target nulo
spike(cpp2il): dump inicial do GameAssembly (não mergeado)
```

Regras:

- Um commit = uma mudança coerente; evite “wip” no histórico da `main`.
- O corpo explica **por que**, não só o que.
- Referencie issues/notas (`Refs: #12`, `Ver: docs/re/...`).
- Nunca commitar segredos, `Environment.props`, modelos, logs, saves ou binários do jogo.
- Se o commit for uma tentativa que falhou, deixe isso claro (`spike:` + corpo) e registre o
  aprendizado em `docs/re/dead-ends.md`.

## 4. Fluxo de trabalho

```powershell
git switch main
git pull --ff-only
git switch -c feat/chat-hook

# ... implementa, builda, testa ...
dotnet build AICompanionsAska.sln -c Release

git add -A
git commit -m "feat(chat): captura a mensagem do jogador"

# revisão/teste em jogo; se ok:
git switch main
git merge --no-ff feat/chat-hook
git push origin main
git branch -d feat/chat-hook
git push origin --delete feat/chat-hook
```

## 5. Critérios de merge

Antes de mergear na `main`:

- [ ] `dotnet build AICompanionsAska.sln -c Release` passa sem warnings novos relevantes.
- [ ] O plugin carrega no jogo e o log mostra a versão correta.
- [ ] O comportamento foi testado (ou marcado explicitamente como **pendente de confirmação**).
- [ ] Não há segredos, modelos, logs ou `Environment.props` no diff.
- [ ] `AGENTS.md`/docs atualizados se um fato durável mudou.
- [ ] A branch tem um objetivo claro e não mistura assuntos.

## 6. Releases e tags

- Versão no `PluginInfo.Version` **e** no `<Version>` do `.csproj` (sempre iguais).
- Tag anotada na `main`: `git tag -a v0.1.0 -m "AI Companions - ASKA 0.1.0"`.
- Release do GitHub com:
  - DLL compilada;
  - README/CHANGELOG;
  - licenças de terceiros (ver `THIRD_PARTY_NOTICES.md`);
  - instruções de instalação.
- Modelos de voz/LLM **não** vão no repositório; se forem no pacote, respeite as licenças e o
  tamanho.

## 7. Hotfix

1. `git switch main && git pull --ff-only`
2. `git switch -c fix/<slug>`
3. Correção mínima + teste.
4. Merge `--no-ff`, tag de patch (`v0.1.1`), push.

## 8. Experimentos que falham

- Mantenha a branch `spike/...` com o commit e uma nota no corpo/PR.
- Registre a conclusão em `docs/re/dead-ends.md` (o que foi tentado, por que falhou, o que
  fazer no lugar).
- Não mergeie código morto na `main`; se precisar do histórico, a branch/tag guarda.

## 9. Regra de ouro

Se em dúvida, **branch**. O custo de uma branch é baixo; o custo de uma `main` quebrada (e de
um save corrompido) é alto.
