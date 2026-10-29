# Handoff — retomada do projeto

> Documento **vivo**: descreve onde o projeto parou e qual é o próximo passo seguro.
> Última atualização: **2026-10-29** (pausa da sessão `feat/recruit-follow`).

## 1. Estado em uma frase

O mod **v0.4.0 está confirmado em jogo** (chat endereçado + resposta por nome + LLM);
a **v0.5.0 está em andamento na branch `feat/recruit-follow`** e **não deve ser mergeada**:
o gate da party foi descoberto, o unlock pós-load está implementado mas **não confirmado em
jogo**, e o probe da party precisa ser reobservado depois da correção.

- `main` = `v0.4.0` (estável, tag e pré-release no GitHub).
- `feat/recruit-follow` = trabalho em andamento, pushado, **sem merge**.
- `General/DebugUnlockParty` default **false** no `ModConfig`.

## 2. O que foi conquistado nesta sessão

### Gate da party
- A criação/gestão da party é liberada por `SSSGame.PlayerHouse.allowPartyManagement`
  (bool na casa do jogador), ligado pela **Cabana do Chefe nível 2**.
- Membership **não** é estado da `PlayerParty`: fica no **villager**
  (`_playerParty`, `__PlayerPartyID`, `Rpc_ChangePlayerParty`). `PlayerParty` implementa
  `IWorkstation` (`SetTaskAgent`/`ReleaseTaskAgent`), e a `PartyFollowQuest` já registrada é
  quem faz o follow. Detalhes: `docs/re/party-unlock-plan.md`.

### O crash do unlock — resolvido na causa, não no sintoma
- Sintoma: `0xC0000005` em `GameAssembly.dll` RVA `0xF72955`
  (`WeatherManager::Instance_OnWeatherUpdate`), no boot/menu, com o toggle ligado.
- Causa raiz: `PlayerHouse::_OnActivate` é um **thunk de 7 bytes compartilhado por 29
  métodos**. O Harmony patcheia **por endereço**, o detour materializa `this` sem validar a
  classe nativa, e `__instance is PlayerHouse` é sempre verdadeiro → a escrita do bool caía
  em `WeatherManager`, `TaskManager`, `VillagerSelectionManager` etc. Corrupção de memória
  (referências com LSB virando `1`), não null-deref.
- Solução atual: **sem Harmony**. Poll de main thread (`FindAnyObjectByType<PlayerHouse>()`)
  com gates (`OwnerStructure` resolvido, cena válida/carregada, settle de 5 s) que escreve o
  bool **depois** do load, uma vez por instância, com `Disarm` em qualquer falha de interop.
- Regras novas (skill `aska-il2cpp`, armadilhas 14 e 15): **nunca** patchear método com
  `aliases > 1`; **nunca** usar `is T`/`as T` como guarda de tipo em postfix.
  Ferramenta: `docs/re/tools/rva-to-method.fsx` (mostra aliases).

## 3. O que está confirmado × o que não está

| Item | Status |
|---|---|
| v0.4.0: chat, nome, LLM, resposta no chat | **confirmado em jogo** (2026-10-29) |
| Build 0.5.0 carrega com o probe poll-only | **confirmado em jogo** (2026-10-29 19:22, menu): `Loading [AI Companions - ASKA 0.5.0]`, `probe armed (poll-only)`, `state=NoParty`, sem exceção e sem dump novo |
| Unlock pós-load arma e faz poll sem crashar | **confirmado em jogo** (2026-10-29 18:43, `state=NoHouse` no menu) |
| `FindAnyObjectByType<PlayerHouse>()` funciona neste build | **confirmado** (não houve `Disarm`/`MissingMethodException`) |
| Escrita `False -> true (trigger=post-load)` no save | **pendente de confirmação** |
| Probe mostrar `pc`/`authority`/`count`/nomes reais | **pendente** (probe reescrito como **poll tipado, sem Harmony**; reobservar em jogo) |
| Party criada com Thorin/Maria | **pendente** (UI nativa) |
| `follow`/`stop`/`come` | **não implementado** |
| Crashes de 18:19/18:21 (assinaturas diferentes) | **não atribuídos** (logs sobrescritos) |

## 4. Próximo passo (ordem recomendada)

1. **Bump já feito** para `0.5.0` (`PluginInfo` + `.csproj`): a linha `[AIC] ... 0.5.0` no
   `LogOutput.log` identifica o build. Confirme antes de qualquer teste.
2. **Controles** (ver `docs/testing.md` / matriz no relatório do QA):
   - A0: **sem o mod** (DLL fora de `plugins\AI-Companions-Aska\`), 3 loads do save.
     Se crashar aqui, o problema é do jogo/save, não da branch.
   - A1: mod carregado, `DebugUnlockParty=false`, 3 loads, sem crash.
   - A2: `DebugUnlockParty=true`, build novo: esperar `armed` + `trigger=post-load` e
     **nenhum** dump novo.
3. **Probe corrigido**: entrar no mundo e conferir `pc=<jogador>`, `authority=...`,
   `count`, e depois juntar **Thorin** pela UI nativa → `count 0->1`, `names=Thorin`.
4. Juntar **Maria** → `count 1->2`, `Maria` no `CompanionRegistry`.
5. Só então implementar `follow`/`stop`/`come` via `Rpc_ChangePlayerParty`
   (entrar/sair) — `come` só se o experimento de `FollowPlayerTask` aprovar; caso
   contrário, não expor a ação.
6. Save/reload + morte/despawn antes de qualquer merge.

## 5. Regras de segurança para a retomada

- **Backup** de `%LOCALAPPDATA%Low\Sand Sailor Studio\Aska\data` antes de rodar com unlock
  ligado. Saves que passaram pelo unlock antigo (que corrompia memória) são suspeitos.
- Preservar `LogOutput.log` de **cada** run (`%TEMP%\aska-runs\<label>\`), porque o BepInEx
  sobrescreve o arquivo a cada sessão.
- Se crashar: mapear o RVA com `dotnet fsi docs/re/tools/rva-to-method.fsx 0x<rva>` e
  **conferir `aliases`** antes de qualquer conclusão.
- Não mergear `feat/recruit-follow` na `main` sem a matriz A0/A1/A2 + party + ordens
  confirmadas pelo jogador.

## 6. Evidência (leia antes de mexer)

- `docs/re/party-gate-crash.md` — crash, RVAs, thunk de 29 aliases, probe em 5 passos.
- `docs/re/party-unlock-plan.md` — membership, fases de experimento, APIs.
- `docs/re/dead-ends.md` #21/#22 — thunk compartilhado e `is T` que mente.
- `docs/re/villager-ai-and-party.md` §7 — gate e correção da premissa.
- `.opencode/skills/aska-il2cpp/references/harmony-pitfalls.md` #14/#15.
- `docs/re/chat-reply-verification.md` — v0.4.0 confirmado.

## 7. Dívidas conhecidas (não bloqueiam a pausa)

- `PlayerHousePartyUnlock.TryUnlockNow()` sem chamador (reservado para um futuro
  `/c unlockparty`); remover ou ligar antes do merge.
- Poll perpétuo após o sucesso (1 Hz por 60 s, 5 s depois) — custo baixo, mas pode ser
  desarmado ao escrever.
- Hard reference a `SSSGame.PlayerHouse`: um rename do tipo no jogo derruba o carregamento
  do assembly (o modo de falha é "mod não carrega"). Reavaliar com resolução por nome se
  isso acontecer.
- Poll pega **uma** casa por vez (`FindAnyObjectByType`); em co-op ou com preview de
  construção pode pegar a instância errada.
