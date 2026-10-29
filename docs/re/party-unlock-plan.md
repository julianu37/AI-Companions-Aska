# Plano - party nativa, recrutamento e ordens `follow`/`stop`/`come`

**Branch:** `feat/recruit-follow`
**Data:** 2026-10-29
**Status:** **pausado em 2026-10-29, sem merge.** O probe foi corrigido para acesso tipado, mas
o estado real da party (`_pc`/`_partyMembers`/`_hasAuthority`) e a escrita do bool em jogo
ainda **não** foram reobservados.
**Alvo:** ASKA build `25834809` (binário 2026-10-09), Unity `6000.3.12f1`, IL2CPP metadata v39
**Escopo deste documento:** mapear a criação/entrada na party nativa, decidir como implementar
`follow`/`stop`/`come` e definir o experimento seguro em jogo. **Nada foi compilado, deployado
ou executado nesta etapa.** O jogo pode estar aberto; o plano não assume que ele esteja fechado.

> **Regra de ouro do projeto:** assinatura de interop não é comportamento. Tudo que não foi
> visto no `LogOutput.log` fica marcado como **superfície** ou **hipótese**, com o experimento
> que confirma/refuta.

---

## 0. Legenda de status

| Marca | Significado |
|---|---|
| **[CONFIRMADO]** | Visto em jogo/log nesta build, com data e trecho de log. |
| **[SUPERFÍCIE]** | Extraído das interop assemblies com `ilspycmd` (campos, assinaturas, `CallerCount`). Não prova o corpo nem o comportamento. |
| **[HIPÓTESE]** | Inferência a partir da superfície; precisa do experimento da §5. |
| **[PENDENTE]** | Ainda não observado; não usar como base de código de release. |

---

## 1. TL;DR - decisões propostas

1. **Não existe uma chamada "CreateParty"** na superfície. A party nasce quando o primeiro
   villager entra nela. O caminho nativo mais provável é
   `Villager.Rpc_ChangePlayerParty(NetworkId da party)`, chamado pela UI nativa
   (`PlayerToVillagerInteractionSession.BeginAddToParty`).
2. **`PlayerParty.SetTaskAgent(ITaskAgent)` é o "apply" local**, não o entry point de rede.
   `PlayerParty` implementa o contrato `SSSGame.AI.IWorkstation` (mesmo conjunto de métodos:
   `SetTaskAgent`, `ReleaseTaskAgent`, `GetAgentsCount`, `GetAgentsCapacity`,
   `GetAssignedVillagers`, `FindTaskAgent`, `ResumeActivity`, ...). Chamar `SetTaskAgent`
   sozinho tende a adicionar à lista `_partyMembers` sem gravar
   `Villager._playerParty`/`__PlayerPartyID`, ou seja, sem marcador, sem persistência e com
   risco de dessincronização. **Não usar como caminho principal.**
3. **Com a party criada, `follow` e `stop` são nativos**, sem quest customizada:
   - `follow` = entrar na party (RPC) e deixar a `PartyFollowQuest` já registrada no
     `QuestRunner` de cada membro cuidar do movimento.
   - `stop` = sair da party (RPC com o sentinela nativo) ou abortar a ordem atual; qual dos
     dois é o "parar" vanilla será decidido na fase 1 (§5.4). A recomendação é **`stop` =
     sair da party** (é o único estado nativo que faz o villager parar de seguir e voltar ao
     trabalho); "cancelar a ordem atual" vira uma ação separada (`cancel`) no futuro.
   - `come` = **não existe ordem nativa "venha aqui"**. Com a party ativa, o villager já
     segue; se estiver longe, o `HurryUp` nativo pode fazê-lo correr. A única opção nativa
     testável é iniciar um `FollowPlayerTask` de curto alcance no `TaskRunner` do villager -
     experimento separado, sem teletransporte.
4. **Não criar `Quest`/`Task` customizada.** `Quest`, `Task` e `DynamicTask` são tipos
   IL2CPP; subclasse gerenciada não tem vtable nativa e não é suportada pelo Il2CppInterop.
   Usar as quests/tasks existentes.
5. **Primeiro experimento em jogo é vanilla:** criar a party pela UI nativa, sem nenhuma
   escrita do mod, e observar o probe. Isso responde de graça "quem cria a party", "o que a
   UI chama" e "o que muda em `_networkParty`/`_pc`/`_hasAuthority`". Só depois replicar via
   `Rpc_ChangePlayerParty`.
6. **Se a party ainda não existe e `_networkParty`/`_pc` são nulos**, o mod não deve tentar
   "inventar" a criação: exigir uma criação manual pela UI (uma vez) e então gerenciar os
   membros seguintes. Criar a party do zero fica como investigação separada (§7, Q3).

### 1.1 Resposta curta à pergunta "dá para fazer só com SetTaskAgent/ReleaseTaskAgent/AbortOrder/_followQuest?"

| Ordem | Só com essas APIs? | O que usar de fato | Quest/task customizada? |
|---|---|---|---|
| `follow` | **Não só com `SetTaskAgent`.** | `Villager.Rpc_ChangePlayerParty(id)` (entrada nativa) + `_followQuest` já registrada no membro | **Não** |
| `stop` | **Depende da semântica vanilla.** | sair da party via RPC com o sentinela, **ou** `TryAbortOrder`/`AbortOrder` se o "stop" vanilla só cancela a ordem | **Não** |
| `come` | **Não.** Não existe ordem nativa de "venha". | `FollowPlayerTask(party, 1f, 2.5f)` no `TaskRunner` (task nativa) ou apenas o follow nativo | **Não** (task nativa; quest customizada seria inviável) |

`ReleaseTaskAgent` isolado é apply local: não grava `Villager._playerParty`/`__PlayerPartyID`,
então não substitui o RPC. `AbortOrder` não remove da party; ele aborta a ordem de interação
atual.

---

## 2. Fatos: confirmado x superfície x hipótese

### 2.1 Fatos de jogo - [CONFIRMADO] gate/crash; [NÃO OBSERVADO] estado da party

- **[CONFIRMADO] Gate:** a criação/gestão da party é liberada por
  `SSSGame.PlayerHouse.allowPartyManagement`; a Cabana do Chefe nível 2 liga o bool.
- **[HISTÓRICO/ENGANOSO] Log do unlock antigo:** o postfix Harmony imprimiu
  `PlayerHouse allowPartyManagement: False -> true`, mas, pelo bug de alias descrito abaixo,
  ele podia estar rodando em um objeto que não era `PlayerHouse`. Esse log **não** prova que a
  bool de uma casa real foi alterada; serve só para mostrar que o patch disparava.
- **[CONFIRMADO] Crash do unlock antigo:** o patch Harmony em `PlayerHouse._OnActivate`
  disparou **16x**, mas não por AOT/inline: `_OnActivate` é um **thunk compartilhado** em
  `GameAssembly + 0x8009B0`, com **29 aliases** (`WeatherManager.Awake`, `TaskManager.Awake`,
  `SettlementManager.Awake`, `VillagerSelectionManager.Awake`, ...). O detour materializa
  `this` como o tipo declarado e `Il2CppObjectPool.Get<T>` **não valida a classe nativa**;
  `__instance is PlayerHouse` é sempre verdadeiro. A escrita do bool caiu em objetos de
  outras classes e corrompeu memória; os RVAs de crash foram `0xF72955` (`WeatherManager`),
  `0x4525346` (`UnityEngine.Object.op_Equality`) e `0x112B70D`
  (`VillagerSelectionManager`). Evidência completa: `docs/re/party-gate-crash.md`.
- **[CONFIRMADO] Poll pós-load (fire-verify de 2026-10-29 18:43):** com o caminho sem Harmony,
  o log mostrou `[AIC][PARTY] DebugUnlockParty armed (no Harmony patch)` e
  `[AIC][DEBUG] [PARTY] unlock poll: id=0 state=NoHouse` no menu, **sem crash e sem `Disarm`**.
  Isso confirma que `FindAnyObjectByType<PlayerHouse>()` (singular) não lançou
  `MissingMethodException` nesta build.
- **[PENDENTE] Escrita do bool em jogo:** o poll armou e pollou no menu; a escrita
  `allowPartyManagement = true` em uma `PlayerHouse` real e o efeito na UI da party ainda
  **não foram observados**.
- **[CONFIRMADO pelo jogador] Save de teste:** 5 companions; `Thorin` e `Maria` foram
  renomeados.
- **[NÃO OBSERVADO - probe cego] Estado da party antes da correção:** o probe antigo usava
  `Type.GetField("_pc")`, `"_partyMembers"`, `"_partyPets"`, `"_followQuest"` e
  `"_hasAuthority"`. No interop esses membros são **propriedades**, não campos; o
  `GetField` devolvia `null` e o log imprimia `pc=? authority=False members=0 pets=0`
  **artificialmente**. Não concluir que `_pc` é nulo, que `_hasAuthority` é falso ou que a
  party está vazia. O estado real será reobservado depois da correção tipada do probe
  (`CompanionPartyProbe.cs`).

### 2.2 [SUPERFÍCIE] extraída hoje (2026-10-29)

Comando usado (somente leitura, não é build):

```powershell
$dll = "C:\Program Files (x86)\Steam\steamapps\common\ASKA\BepInEx\interop\Assembly-CSharp.dll"
ilspycmd -t SSSGame.PlayerParty $dll
ilspycmd -t SSSGame.Villager $dll
ilspycmd -t SSSGame.PlayerToVillagerInteractionConfig $dll
ilspycmd -t SSSGame.AI.IWorkstation $dll
ilspycmd -t SSSGame.AI.PartyFollowQuest $dll
ilspycmd -t SSSGame.AI.FollowPlayerTask $dll
ilspycmd -t SSSGame.AI.QuestRunner $dll
ilspycmd -t SSSGame.Network.NetworkPlayerParty $dll
```

#### `SSSGame.PlayerParty : Fusion.SimulationBehaviour`

Campos/propriedades relevantes - **atenção:** `_pc`, `_partyMembers`, `_partyPets`,
`_followQuest` e `_hasAuthority` são **propriedades com getter no interop**, não campos.
`Type.GetField(...)` devolve `null`; o probe precisa acessar `party._pc` etc. (ou
`GetProperty` se o tipo não for referenciável).

```text
PlayerCharacter _pc { get; }
List<Villager> _partyMembers { get; }
List<Pet> _partyPets { get; }
List<IWorkstation> _boundWorkstations
PartyFollowQuest _followQuest { get; }
PartyInteractQuest _interactQuest
PartyCombatQuest _combatQuest
NetworkSession session
NetworkPlayerParty _networkParty
List<Interaction> _localInteractionOrders
bool _hasAuthority { get; }
PlayerRef _authority
vFSMBehaviour followFsm / interactFsm / combatFsm
InteractSlot interactSlotParty
string villagerInteractWidgetText
```

`Pet` é `SSSGame.Pet : Creature` (não é `Villager`) e também tem `GetName()` override; o probe
lê os pets por esse método.

Os demais itens da lista acima podem ser propriedades ou campos; o probe só depende dos cinco
marcados com `{ get; }`.

Métodos relevantes:

```text
bool SetTaskAgent(ITaskAgent agent)          // virtual final newslot (contrato IWorkstation)
bool ReleaseTaskAgent(ITaskAgent agent)      // virtual final newslot
int  GetAgentsCount()
int  GetAgentsCapacity()
ITaskAgent FindTaskAgent(Predicate<ITaskAgent>)
List<ITaskAgent> GetAssignedVillagers()
void ResumeActivity()
void RemoveAllWorkers()
bool TryOrderToInteract(Interaction interaction)
void AddInteractionOrder(Interaction interaction)
bool TryAbortOrder(Villager villager)
void AbortOrder(Villager villager)
bool FocusOnTarget(IAttackTarget target)
bool TryFocusOnTarget(IAttackTarget target)
bool Deserialize(DataObject source)
DataObject Serialize(DataObject destination = null)
```

Evidência de que `PlayerParty` implementa `IWorkstation`: o conjunto de métodos é idêntico ao
de `SSSGame.AI.IWorkstation` (não só os "de party"), e as entradas de metadata aparecem como
`Public_Virtual_Final_New_*` - o padrão de implementação de interface no IL. O `ilspycmd` não
lista a interface na declaração da classe, mas a assinatura fecha.

`CallerCount` (xref estático, não prova semântica):

| Método | CallerCount | Leitura |
|---|---:|---|
| `SetTaskAgent` | 1 | um único call site nativo direto |
| `ReleaseTaskAgent` | 1 | um único call site nativo direto |
| `GetAgentsCapacity` | 2 | provavelmente `SetTaskAgent` + UI |
| `Deserialize` | 1 | save/load |

**Cuidado com `CallerCount`:** ele é só o xref estático. Não indica AOT/inline, não prova que
um patch vai disparar e não conta chamadas indiretas/virtuais. O caso `PlayerHouse._OnActivate`
é a prova: o patch disparou **16x**, mas porque o endereço é um thunk compartilhado por 29
métodos - não porque o método tivesse corpo próprio.

#### `SSSGame.Villager : SSSGame.Character`

Membros de party:

```text
PlayerParty _playerParty            // campo direto
NetworkId __PlayerPartyID           // backing field público (get/set direto)
NetworkId _PlayerPartyID            // propriedade privada, get/set nativo (rede)
PlayerParty GetParty()
bool HasParty()
void Rpc_ChangePlayerParty(NetworkId playerPartyId)   // público no interop
GameObject partyMarker
SpriteRenderer partyMarkerIcon / partyMarkerBorder
```

`Rpc_ChangePlayerParty` tem `CallerCount(2)`; a propriedade privada `_PlayerPartyID` tem
`CallerCount(1)` no get e no set. `GetParty()` tem `CallerCount(0)` **no xref estático**
(pode ser chamado por caminho indireto/gerenciado; é API pública), enquanto `HasParty()` tem
21 - é o caminho usado pela vila/IA.

#### `SSSGame.PlayerToVillagerInteractionConfig` / `PlayerToVillagerInteractionSession`

Config (asset compartilhado):

```text
InteractSlot partyInteractSlot
Sprite partyJoinIcon / partyDismissIcon / partyStopIcon
string partyJoinText / partyDismissText / partyStopText
bool enablePartyOptions
```

Sessão (a UI por trás da interação jogador -> villager):

```text
PlayerParty _pp
bool _setFollowBar
SessionFunction _function
void BeginAddToParty(InputAction.CallbackContext obj)
void StopVillager(InputAction.CallbackContext obj)
bool _BeginAddToParty_b__31_0(ITaskAgent ta)     // predicado sobre ITaskAgent
bool _RefreshWidgetData_b__39_0(ITaskAgent ta)   // predicado sobre ITaskAgent
```

`_pp` é `PlayerParty`; os dois predicados recebem `ITaskAgent`. Isso é a assinatura de
`FindTaskAgent(ta => ...)` / `SetTaskAgent(...)`, não de uma chamada de UI genérica.

#### `SSSGame.Network.NetworkPlayerParty : Fusion.NetworkBehaviour`

Só há RPCs de **ordem**, não de membership:

```text
void SendTargetToFocus(IAttackTarget target)
void SendInteractOrder(Interaction interaction)
void ModifyLocalInteractionOrderToTarget(PlayerRef target, Interaction interaction, OrderModification operation)
void AbortAllInteractionOrders(Interaction interaction)
void Rpc_AbortOrder(PlayerRef _, NetworkId villagerId)
void Rpc_FocusTarget(PlayerRef _, NetworkId creatureID)
```

**Consequência importante:** a membership não é um estado da `PlayerParty` (que é
`SimulationBehaviour`, sem estado de rede). Ela é estado do **villager**
(`__PlayerPartyID`/`_playerParty`). A `PlayerParty._partyMembers` é, muito provavelmente, um
espelho local derivado - hipótese a confirmar na fase 1.

#### `SSSGame.AI.IWorkstation` (contrato que a party implementa)

```text
bool SetTaskAgent(ITaskAgent agent)
bool ReleaseTaskAgent(ITaskAgent agent)
ITaskAgent FindTaskAgent(Predicate<ITaskAgent>)
int GetAgentsCount()
int GetAgentsCapacity()
List<ITaskAgent> GetAssignedVillagers()
List<IWorkstation> BoundWorkstations
void ResumeActivity()
...
```

#### Quests/tasks

```text
PartyFollowQuest(PlayerParty party, vFSMBehaviour behaviour)
  PlayerParty Party { get; protected set; }
  vFSMBehaviour fsmBehaviour
  QuestData CreateQuestData()
  float GetPriority(QuestData questData)
  PartyFollowQuestData.HurryUp : bool

PartyInteractQuest(PlayerParty party, vFSMBehaviour behaviour)
  List<InteractOrder> interactOrders
  InteractOrder CreateInteractionOrder(Interaction interaction)

PartyCombatQuest : CombatQuest
  PlayerParty party
  void FocusTarget(IAttackTarget target)

QuestRunner
  void AddQuest(Quest quest)
  void RemoveQuest(Quest quest)
  Quest GetActiveQuest()
  QuestData GetQuestData(Quest quest)
  bool PendingChangeHurryUp

TaskRunner
  bool StartTask(Task t)
  void AbandonTask(Task task)
  Task GetActiveTask()
  T GetActiveTask<T>()

FollowPlayerTask(PlayerParty playerPartyManager, float minRange = 2f, float maxRange = 5f)
```

`FollowPlayerTask` tem construtor público - é uma task nativa que pode ser criada pelo mod.
`Quest`/`Task` não têm construtor público utilizável para subclasse gerenciada.

### 2.3 [HIPÓTESE] cadeia nativa de entrada/saída

A combinação de `CallerCount`, nomes e campos sugere:

```text
UI nativa (PlayerToVillagerInteractionSession.BeginAddToParty)
  -> Villager.Rpc_ChangePlayerParty(idDaParty)
      -> [state authority] Villager._PlayerPartyID = id
          -> setter nativo resolve a PlayerParty pelo NetworkId
              -> PlayerParty.SetTaskAgent(villager.TryCast<ITaskAgent>())
                  -> _partyMembers.Add(villager)
                  -> villager._playerParty = this
                  -> QuestRunner.AddQuest(_followQuest/_interactQuest/_combatQuest)
                  -> OnVillagerAdded(villager)
                  -> partyMarker visível / persistência por __PlayerPartyID
```

Saída:

```text
UI (dismiss/stop)
  -> Villager.Rpc_ChangePlayerParty(sentinela)
      -> _PlayerPartyID = sentinela
          -> PlayerParty.ReleaseTaskAgent(villager)
              -> remove quests, _partyMembers.Remove, ResumeActivity()
```

Por que essa cadeia e não `SetTaskAgent` direto: `SetTaskAgent` só tem 1 call site nativo
(consistente com "chamado de dentro do setter de `_PlayerPartyID`"), enquanto o RPC tem 2 call
sites (entrar e sair). O `SetTaskAgent` é o apply; o RPC é a porta de entrada.

**Status:** hipótese forte. A fase 1 (§5.3/§5.4) confirma ou refuta cada elo, sem escrever
nada no jogo.

---

## 3. Fluxo nativo de criação da party

### 3.1 O que é "criar a party"

A superfície não tem `PlayerParty.Create()`/`Initialize(PlayerCharacter)`. A `PlayerParty` é
um componente de cena que já existe. O probe **antigo** relatava `_pc=null`,
`_hasAuthority=false` e `members=0`, mas isso era artefato do acesso por `GetField` a
propriedades - **não** é observação válida. Com o probe tipado, há duas leituras possíveis:

1. **A party é ativada/vinculada ao jogador no primeiro join** (a UI cria/spawna o
   `NetworkPlayerParty` ou seta `_pc`/`_authority`). Nesse caso, o mod **não** consegue criar
   a party sozinho: precisa de uma criação manual única pela UI.
2. **A party já está vinculada**, mas o probe antigo não conseguia ler `_pc`/`_authority`; a
   party real pode ter `_pc != null` desde o load. Nesse caso, o probe tipado já mostra o
   estado correto e a criação manual é desnecessária.

A fase 1 distingue as duas: logar **todas** as instâncias de `PlayerParty`, com
`_pc`, `_pc.Object.HasInputAuthority`, `_hasAuthority`, `_networkParty` e `members`, antes e
depois do primeiro join vanilla. Não assumir nenhum dos dois cenários antes dessa leitura.

> **Decisão de segurança:** até a fase 1, o mod não tenta criar a party. Se o jogador quiser
> testar ordens já, cria a party uma vez pela UI nativa. O mod gerencia os membros seguintes e
> as ordens.

### 3.2 Entrada de um villager (caminho recomendado)

1. Identificar a `PlayerParty` local (§6.3).
2. Validar: villager vivo, `Object.HasStateAuthority`, `party._networkParty.Object.Id` válido,
   `GetAgentsCount() < GetAgentsCapacity()`, `!villager.HasParty()`.
   **Não** usar `party.Object.Id` como fallback: se a `PlayerParty` estiver no mesmo
   `NetworkObject` do jogador, isso é o id do jogador, não da party. O id tem que vir do
   `NetworkPlayerParty` (`_networkParty.Object.Id`); se ele for nulo, abortar.
3. Chamar `villager.Rpc_ChangePlayerParty(partyId)` - **uma vez**, na main thread, fora de
   qualquer callback de lifecycle.
4. Verificar em 1-2 s: `villager.GetParty()` é a mesma party (comparar ponteiro nativo),
   `villager.HasParty()`, `_partyMembers` contém o villager, `__PlayerPartyID` == partyId, e
   `QuestRunner.GetQuestData(party._followQuest) != null`.
5. Se o RPC não tiver efeito, **não** cair automaticamente para `SetTaskAgent`. Registrar e
   parar; a fase 4 é um experimento separado e consciente.

### 3.3 Saída de um villager

- O sentinela de "sem party" não está na superfície. A fase 1 lê
  `villager.__PlayerPartyID` antes do join, em party e depois do dismiss vanilla; o valor
  observado (provavelmente `default(NetworkId)`, `IsValid == false`) vira a constante do mod.
- Caminho recomendado: `villager.Rpc_ChangePlayerParty(sentinela)`.
- `PlayerParty.ReleaseTaskAgent(villager)` isolado é apply local; usar só como fallback de
  desenvolvimento, nunca em co-op.
- `AbortOrder(villager)`/`TryAbortOrder(villager)` **não** remove da party: eles abortam a
  ordem de interação atual (`PartyInteractQuest`). Não confundir com "parar de seguir".

### 3.4 O que a `PartyFollowQuest` espera

- Um `PlayerParty` não nulo e um `vFSMBehaviour` (`followFsm`) atribuído. O construtor é
  `PartyFollowQuest(PlayerParty, vFSMBehaviour)`; a party provavelmente cria a quest no
  `Awake`/`Start`/primeiro join e guarda em `_followQuest`.
- A quest é **por villager** via `QuestRunner`: cada membro precisa tê-la registrada
  (`AddQuest`) e o `GetPriority` dela precisa vencer as quests de trabalho/sobrevivência.
- A quest usa o `_pc` da party como alvo de follow e o `HurryUp` para correr quando o
  jogador se afasta. O `PartyFollowQuestData.HurryUp` é sobrescrito pela quest.
- **Não instanciar uma segunda `PartyFollowQuest`.** Se `_followQuest` for nulo com membros
  presentes, isso é bug de init: logar `followFsm` e esperar/entender o caminho nativo; não
  criar a quest manualmente sem confirmação.

### 3.5 O que a `PartyInteractQuest` / `PartyCombatQuest` esperam

- `PartyInteractQuest` recebe `InteractOrder` criados por `CreateInteractionOrder(Interaction)`
  e os distribui entre os membros (`considerations`/`workingAgents`). É o caminho de
  `gather`/`build`/`harvest`.
- `PartyCombatQuest` recebe `FocusTarget(IAttackTarget)`; é o caminho de `attack`.
- Nenhuma das duas precisa de quest customizada. Ficam para as fases seguintes; não misturar
  com o experimento de membership.

---

## 4. Ordens: mapeamento e decisão

### 4.1 `follow`

**Semântica:** "fica comigo / segue". **API nativa:** entrar na party via
`Rpc_ChangePlayerParty`; a `PartyFollowQuest` faz o resto.

Pré-condições (recusar com mensagem clara se falhar):

- villager existe, não está morto;
- `party._networkParty != null` e `partyId.IsValid`;
- `villager.Object.HasStateAuthority` (single-player/host);
- `party.GetAgentsCount() < party.GetAgentsCapacity()` (se `Capacity == 0`, a party não está
  liberada -> pedir a Cabana do Chefe lvl 2 ou usar o unlock de dev);
- `!villager.HasParty()` (ou `villager.GetParty()` é a nossa party -> já está seguindo).

Verificação: `_partyMembers` + `GetParty()` + `GetQuestData(_followQuest)` + `_activeQuest`
+ distância ao jogador caindo/estabilizando.

### 4.2 `stop`

Há duas coisas diferentes no jogo:

| Operação | API provável | Efeito |
|---|---|---|
| Parar a ordem atual (coletar/construir) | `TryAbortOrder(villager)` / `AbortOrder(villager)` / `NetworkPlayerParty.Rpc_AbortOrder` | mantém na party; o villager volta a seguir |
| Sair da party / parar de seguir | `Rpc_ChangePlayerParty(sentinela)` | remove da party; volta ao trabalho/escala |

A UI tem `partyStopIcon`/`partyStopText` e `partyDismissIcon`/`partyDismissText` - ou seja,
provavelmente **as duas** existem. A fase 1 (§5.4) observa qual botão muda o quê.

**Recomendação inicial:** `stop` do LLM = **parar de seguir** = sair da party (RPC com
sentinela). É o único estado nativo que interrompe o follow e devolve o villager ao trabalho.
"Cancelar a ordem atual" fica como ação separada (`cancel`) quando `gather`/`attack` entrarem.
Se a fase 1 mostrar que o botão "Stop" vanilla mantém o membro na party, inverter o mapeamento
e usar `AbortOrder` para `stop`.

**Importante:** não usar `ReleaseTaskAgent` direto como `stop` em produção; ele é apply local
e pode deixar o `__PlayerPartyID`/marcador inconsistentes.

### 4.3 `come`

Não existe `ComeToPlayerTask`/ordem nativa de "venha aqui". Opções, em ordem de preferência:

1. **Nada além do follow nativo.** Se o villager já é membro, o `PartyFollowQuest` já o traz;
   se estiver longe, o `HurryUp` nativo pode fazê-lo correr. Resposta honesta: "já estou indo".
2. **`FollowPlayerTask` de curto alcance** (experimento separado):
   ```csharp
   var task = new FollowPlayerTask(party, 1.0f, 2.5f);
   villager.GetTaskRunner().StartTask(task);
   // ... aguardar chegar perto (distância < 2,5 m) ou timeout; então AbandonTask(task)
   ```
   É uma task nativa, sem teletransporte. Riscos: a party follow quest pode substituí-la no
   próximo tick; a task pode nunca terminar (follow é contínuo) e precisa de `AbandonTask`.
   Só testar depois que o membership estiver confirmado, com um companion e timeout.
3. **Custom task/quest:** rejeitado. Não há vtable gerenciada para tipos IL2CPP; seria
   frágil e concorrente com a FSM nativa.

**Decisão:** `come` só entra no código depois da fase 5. Até lá, o mod responde com o follow
nativo e não promete uma ordem que não existe.

### 4.4 Ordens futuras (não implementar nesta branch)

| Ação | API nativa | Observação |
|---|---|---|
| `attack` | `party.FocusOnTarget(IAttackTarget)` -> `PartyCombatQuest.FocusTarget` | precisa de alvo validado (foco do jogador/raycast); não chutar alvo |
| `gather` | `party.TryOrderToInteract(Interaction)` -> `PartyInteractQuest.CreateInteractionOrder` | precisa de um `Interaction` do mundo; o mod não cria recurso |
| `equip`/`drop` | `CharacterEquipment`/`VillagerInventory` | família de inventário: **não** patchar; capturar por lifecycle/polling (dead-end #4) |
| `inventory` | UI nativa | a confirmar |

---

## 5. Experimento seguro em jogo

### 5.1 Pré-condições e backup

- **Single-player only.** Sem segundo cliente, sem co-op.
- **Backup do save** antes de qualquer escrita:
  ```powershell
  $src = "$env:USERPROFILE\AppData\LocalLow\Sand Sailor Studio\Aska\data"
  $dst = "$env:TEMP\aska-save-backup-20261029"
  Copy-Item $src $dst -Recurse
  ```
  A pasta ativa é `data\savegame_<id>_<data>` e o jogo mantém `_bak1`/`_bak2`. Copie a pasta
  `data` inteira; não edite o save.
- `General/VerboseLog=true`; `General/DebugUnlockParty=false` na fase 0/1 (a menos que o dev
  de IL2CPP já tenha um fix confirmado).
- `AI/DebugPartyCommands` (novo, default `false`) só na fase 3.
- **O save tem 5 companions; a capacidade da party pode ser menor que 5.** Não tentar entrar
  com todos de uma vez: Thorin primeiro, Maria segundo, e os demais ficam fora até
  `GetAgentsCapacity()` ser conhecido. Se a capacidade for 3, os outros dois não entram - isso
  não é bug do mod.
- O jogo pode estar aberto: este plano **não** reinicia, não recompila e não faz deploy.
- Se precisar reiniciar para carregar um build novo, o jogador faz; o agente não lança o jogo.

### 5.2 Fase 0 - confirmar o gate sem escrita do mod

Objetivo: ter `allowPartyManagement=true` de forma estável e saber se a party está
"vinculada" ao jogador.

1. Preferência: subir a **Cabana do Chefe para o nível 2** ("Cabana do Chefe Orgulhoso") no
   save de teste e confirmar o log do probe:
   `allowParty=True` e `capacity>0`.
2. Se não for possível, usar `DebugUnlockParty=true` com o poll atual (sem Harmony/`Initialize`)
   e validar o load: o log deve mostrar o bool `true` (`trigger=post-load`) e **não** deve
   haver crash.
3. Observar, com a party recém-carregada (antes de qualquer join):
   - `_pc` (nome), `pcInputAuth`;
   - `_hasAuthority`;
   - `_networkParty` e `_networkParty.Object.Id`;
   - `followFsm`/`interactFsm`/`combatFsm` não nulos;
   - `_followQuest`/`_interactQuest`/`_combatQuest` (nulos ou não);
   - `GetAgentsCount()`/`GetAgentsCapacity()`.

**Critério de sucesso:** `allowParty=True` (se unlock), `capacity>0`, party local identificada
(`_pc != null` e, de preferência, `pcInputAuth=True`), sem crash.

**Se `capacity==0` mesmo com `allowParty=True`:** o bool não é o único gate; a capacidade
pode ser calculada no init da party. Registrar e não chamar `SetTaskAgent` (vai falhar).

### 5.3 Fase 1 - primeiro join pela UI vanilla (sem código do mod)

Esta é a fonte de verdade. Não escrever nada no jogo.

1. Interagir com **Thorin** e escolher a opção de party (`partyJoinText`).
2. Observar o log do probe em 1 Hz antes/depois:
   - `_partyMembers.Count` 0 -> 1 e nome `Thorin`;
   - `_pc` e `_hasAuthority` mudaram?;
   - `_networkParty`/`NetworkId` mudaram? (a party foi criada neste momento?);
   - `_followQuest` passou de nulo para `PartyFollowQuest`?;
   - `villager.__PlayerPartyID` (raw) e `villager.GetParty()`/`HasParty()`;
   - `QuestRunner.GetQuestData(_followQuest)` non-null?;
   - `QuestRunner.GetActiveQuest()` (nome nativo da classe) - é `PartyFollowQuest` ou uma
     quest de trabalho?;
   - `villager.GetWorkstation()`/`IsAvailableForWork()` antes/depois (o join libera o
     trabalho?);
   - `partyMarker` ativo? (se o probe puder ler).
3. Andar 10-20 m e confirmar que Thorin segue; medir a distância por 10 s.
4. **Não** salvar ainda; só observar.

**Patches de observação (opcional, read-only, temporários):** para transformar "inferência pelo
estado" em "ordem de chamadas", dá para patchizar **só postfixes que logam** e remover depois
da fase:

```text
SSSGame.PlayerParty.SetTaskAgent(SSSGame.AI.ITaskAgent)
SSSGame.PlayerParty.ReleaseTaskAgent(SSSGame.AI.ITaskAgent)
SSSGame.PlayerToVillagerInteractionSession.BeginAddToParty(UnityEngine.InputSystem.InputAction.CallbackContext)
```

Regras:
- postfix **não escreve** nada; loga `__result`, `agent.GetName()` e `_partyMembers.Count`;
- `BeginAddToParty` é privado e pode ser um alvo instável (alias de endereço); se não disparar,
  não concluir que a UI não passa por ele; use o estado (`__PlayerPartyID`/`SetTaskAgent`) como
  prova;
- **não** patchear `Villager.Rpc_ChangePlayerParty` nesta fase (método de rede); para saber se
  o caminho de rede foi usado, basta comparar `__PlayerPartyID` antes/depois;
- fire-verify cada postfix com uma linha na primeira execução; remover todos antes do merge.

**O que isso resolve:** entry point real, se a party é criada no primeiro join, quando
`_followQuest` nasce, se as três quests são registradas por membro, e se `HurryUp` aparece.

### 5.4 Fase 2 - segundo membro e semântica de `stop` (vanilla)

1. Repetir o join com **Maria**; anotar o que muda em relação ao primeiro (o caminho
   incremental é o que o mod vai replicar; o primeiro pode ter criação de party).
2. Abrir a interação com Thorin e testar **cada** botão de party separadamente:
   - `partyStopText`/`partyStopIcon`: `_partyMembers.Count` muda? Thorin para de seguir? Volta
     ao trabalho? `__PlayerPartyID` continua válido?
   - `partyDismissText`/`partyDismissIcon` (se existir): `Count` decrementa?
     `__PlayerPartyID` fica inválido? Qual valor exato?
3. Anotar o sentinela de "sem party" (`default`? `NetworkId` inválido? outro valor?).

**Tabela de decisão do `stop`:**

| Observação vanilla | Mapeamento do mod |
|---|---|
| Stop mantém `Count` e o villager para de seguir/fica parado | `stop` -> `AbortOrder`/`TryAbortOrder` (ordem), não remove |
| Stop decrementa `Count` e o villager volta ao trabalho | `stop` -> `Rpc_ChangePlayerParty(sentinela)` |
| Stop não faz nada visível; Dismiss remove | `stop` -> RPC com sentinela; documentar "dismiss = stop" |
| Ambos removem | preferir o botão que não abre confirmação/diálogo |

### 5.5 Fase 3 - join pelo mod (debug command, uma vez)

**Pré-requisito:** fases 0-2 confirmadas; party já criada pela UI; um companion fora da party
(ex.: Maria) para não mexer no Thorin.

1. Implementar `CompanionPartyCommand` (§6.5) atrás de `AI/DebugPartyCommands=true`.
2. Comando: `/party join Maria` (texto exato, sem LLM).
3. O comando é enfileirado no chat e executado no `CompanionBrainRunner.Update`, no máximo 1
   por segundo, com `party._pc != null` e pelo menos 10 s após o load.
4. Sequência de log obrigatória:
   ```text
   [AIC][PARTY] cmd=join target="Maria" party=#<ptr> pc="Thorin" pcInputAuth=True
   [AIC][PARTY] before: netParty=#<ptr/0> partyId=<raw> valid=<bool> count=<n> cap=<n>
   [AIC][PARTY] villager: ptr=#<ptr> name="Maria" dead=False stateAuth=True
                hasParty=False partyMatch=False rawId=<raw> active="<NativeClass>"
   [AIC][PARTY] call: Villager.Rpc_ChangePlayerParty(<partyId>)
   [AIC][PARTY] after 2s: count=<n+1> names=<...> hasParty=True partyMatch=True
                rawId=<partyId> qdata(follow/interact/combat)=True/... active="<NativeClass>"
   ```
5. Se o RPC lançar exceção gerenciada ou não mudar nada em 5 s: **parar**, não repetir, não
   cair para `SetTaskAgent` no mesmo run. Registrar a linha e ir para a fase 4 em outro run.
6. Se funcionar: andar, confirmar o follow, depois testar `stop` pelo mod (fase 3b) e comparar
   com o vanilla da fase 2.

**Critério de sucesso:** o estado após o comando é idêntico ao estado após o join vanilla
(contagem, nome, `GetParty`, `__PlayerPartyID`, quests registradas, marcador, follow).

### 5.6 Fase 4 - fallback `SetTaskAgent` (somente se o RPC falhar)

Objetivo: separar "o RPC não é o caminho" de "o mod não tem autoridade".

1. Em um run separado (sem salvar depois), chamar **uma vez**:
   ```csharp
   var agent = villager.TryCast<ITaskAgent>();
   bool ok = party.SetTaskAgent(agent);
   ```
2. Logar `ok`, `_partyMembers.Count`, `villager.GetParty()`/`HasParty()`,
   `__PlayerPartyID`, `quests`, `partyMarker`.
3. Interpretação:
   - `ok=true` e `GetParty()!=null`/`__PlayerPartyID` válido -> o `SetTaskAgent` faz o apply
     completo; o RPC pode ser só um wrapper de rede. Ainda assim, preferir o RPC em co-op.
   - `ok=true` mas `GetParty()==null`/`__PlayerPartyID` inválido -> apply parcial (lista sem
     estado do villager). **Não usar em produção**; documentar como dead-end.
   - `ok=false` -> capacidade/gate ou tipo; logar `GetAgentsCapacity()` e `allowParty`.
4. **Não salvar** se o resultado for parcial. Se salvar por acidente, restaurar o backup.

### 5.7 Fase 5 - `come`, save/reload, morte/despawn

Somente depois de `follow` e `stop` confirmados.

1. `come`: um companion, `FollowPlayerTask(party, 1f, 2.5f)` + `StartTask`; logar a task ativa,
   a distância por 10 s e `AbandonTask` ao chegar/timeout. Se a party follow quest substituir
   a task imediatamente, registrar "não viável" e manter `come` = follow nativo.
2. Save/reload: salvar pelo menu do jogo, sair para o menu, entrar de novo. Verificar:
   - `_partyMembers` restaurado com os mesmos nomes;
   - `villager.GetParty()`/`HasParty()`/`__PlayerPartyID` restaurados;
   - `_followQuest` registrada de novo em cada membro;
   - follow funciona sem comando extra.
3. Morte/despawn: matar (ou remover) um membro de teste e verificar que o probe não crasha,
   que o nome sai do registry e que a party não fica com referência morta. Se o membro
   ressuscitar/sumir, a ordem deve recusar, não re-adicionar automaticamente.
4. Co-op: **fora desta branch**. Só depois de tudo acima.

### 5.8 Critérios de abortar imediatamente

- Crash nativo / log termina sem stack gerenciada (ver `docs/re/dead-ends.md` §18).
- `_partyMembers.Count` muda sem comando, duplica nome ou entra em loop.
- Villager fica congelado/T-pose, perde o trabalho para sempre ou o `QuestRunner` entra em
  `PartyFollowQuest` sem conseguir sair.
- `_followQuest == null` com membros > 0 e `followFsm == null` (init incompleto).
- `Object.HasStateAuthority == false` no single-player (não chamar RPC).
- Qualquer `MissingMethodException`/`NullReferenceException` no trampoline: remover o
  caminho, registrar em `dead-ends.md`, não "tentar de novo com outro método" no mesmo run.

### 5.9 O que enviar ao dev/QA

Do `BepInEx\LogOutput.log`, apenas as linhas `[AIC][PARTY]` do run, o nome/data do save, a
fase e o resultado (funcionou/não funcionou). Não enviar o save nem o log inteiro. O dev
registra em `docs/re/party-unlock-verification.md` (novo arquivo, após o teste).

---

## 6. Implementação planejada

### 6.1 Arquivos e responsabilidades

| Arquivo | Papel | Escreve no jogo? |
|---|---|---|
| `Companion/CompanionPartyProbe.cs` | probe read-only; loga estado da party/membros; registra nomes | não |
| `Companion/CompanionPartyContext.cs` (novo) | guarda a party local e o mapa nome -> `Villager`; limpa quando a party some (`NoParty`) / no unload | não |
| `Companion/CompanionPartyOrders.cs` (novo) | funções puras de intenção -> chamada nativa (`TryFollow`, `TryStop`, `TryCome`), com resultado e motivo | sim (nativo) |
| `Companion/CompanionPartyCommand.cs` (novo) | comandos de debug `/party ...`, fila e execução no `Update` | sim (dev) |
| `Core/ModConfig.cs` | `AI/DebugPartyCommands`, cooldown, limite de log | não |
| `Plugin.cs` | aplica/limpa o probe e o command; log de versão | não |
| `Patches/ChatPatch.cs` | já entrega o texto; o command lê o `ChatCapture` | não |

### 6.2 Extensão do probe (read-only)

Objetivo: transformar o probe atual em evidência suficiente para as fases 1-2, sem escrever
nada.

> **Correção aplicada em 2026-10-29:** o probe antigo usava `Type.GetField` para `_pc`,
> `_partyMembers`, `_partyPets`, `_followQuest` e `_hasAuthority`. No interop esses membros são
> **propriedades**; o `GetField` devolvia `null` e o log era ficção (`pc=?`,
> `authority=False`, `members=0`, `pets=0`). O probe atual acessa as propriedades de forma
> tipada, lê cada uma em `try/catch` separado e loga `?` **por membro** quando a leitura falha
> (`WarnOnce`), em vez de inventar um valor. Ele também é **100% poll**: não patcheia
> `Spawned`/`Start`/`Despawned`.

Campos a logar por instância de `PlayerParty` (o probe atual já cobre o essencial; os demais
são extensão futura):

```text
id=<GetInstanceID()> pc="<nome>" pcNull=<bool> pcInputAuth=<bool> pcStateAuth=<bool>
authority=<bool> authorityRef=<raw>
netParty=<bool> netId=<raw> netIdValid=<bool>
cap=<int> count=<int>
fsm=f/<i>/<c>  followQuest=<bool> interactQuest=<bool> combatQuest=<bool>
houseAllow=<bool>   // da PlayerHouse capturada (ver §6.7)
members=[...] pets=[...]
```

Por membro:

```text
name="<GetName()>" ptr=<ptr> dead=<IsDead> stateAuth=<Object.HasStateAuthority>
hasParty=<HasParty()> partyMatch=<ptr == party ptr> rawPartyId=<__PlayerPartyID>
ws=<GetWorkstation()!=null> avail=<IsAvailableForWork()>
qActive=<classe nativa da GetActiveQuest()> qFollow=<GetQuestData(follow)!=null>
qInteract=<...> qCombat=<...> hurry=<PartyFollowQuestData.HurryUp> pendingHurry=<PendingChangeHurryUp>
dist=<Vector3.Distance(villager.GetPosition(), pc.transform.position)>
```

Regras do probe poll-only:
- **Descoberta:** `UnityEngine.Object.FindAnyObjectByType<PlayerParty>()` **singular**, dentro de
  `try/catch`. Nunca `FindObjectsByType<T>()`/plural (dead-end #10/#19).
- **Nulidade:** `party == null` (Unity `==`); não usar `WasCollected` (não existe para
  `MonoBehaviour` real). Se não há party, logar `state=NoParty` **uma vez**.
- **Chave/dedupe:** `party.GetInstanceID()` (int) + assinatura dos nomes; logar só quando a
  assinatura muda. Não guardar o wrapper entre mundos (elimina o dead-end #16).
- **Leitura:** acesso tipado (`party._pc`, `party._partyMembers`, `party._partyPets`,
  `party._hasAuthority`, `party._followQuest`), cada um em `try/catch` próprio; `?` naquele
  campo/membro + `WarnOnce` quando falhar.
- **Erro de interop:** `FindAnyObjectByType`/`GetInstanceID` que lançar `MissingMethodException`
  ou equivalente desarma o probe com `WarnOnce` (`Disarm`) e mantém o jogo vivo.
- **Registro:** com `count > 0` (membros ou pets), chamar `CompanionRegistry.Refresh()` **antes**
  de `Register` para o refresh inicial não apagar `Maria`.
- **Sem Harmony:** nada de `Spawned`/`Start`/`Despawned`. `Apply()` respeita
  `General/Enabled` e só arma o poll; `Tick()` lê na main thread, 1 Hz.
- `GetQuestData`/`GetActiveQuest` em `try/catch` (uma quest não registrada pode lançar em
  alguma variante; tratar como "sem dado").
- Não logar `Vector3` direto (dead-end #12): `.ToString("0.0")` na distância.

Esboço (o código real degrada por leitura):

```csharp
internal static void Tick()
{
    if (!Applied) return;
    if (Time.unscaledTime < _nextPoll) return;
    _nextPoll = Time.unscaledTime + 1f;

    PlayerParty? party;
    try { party = UnityEngine.Object.FindAnyObjectByType<PlayerParty>(); }
    catch (Exception ex) { Disarm($"FindAnyObjectByType: {ex.Message}"); return; }

    if (party == null) { LogNoPartyOnce(); return; }

    int key = party.GetInstanceID();
    LogParty(party, key);
}

private static void LogParty(PlayerParty party, int key)
{
    var pc = party._pc;                 // try/catch por leitura
    var authority = party._hasAuthority;
    var follow = party._followQuest;
    var members = party._partyMembers;
    var pets = party._partyPets;
    // ... monta a assinatura, registra nomes se count > 0, loga se mudou
}
```

> O probe não deve chamar `GetAgentsCapacity()` em loop por frame; 1 Hz é suficiente. Se a
> chamada for cara, cachear por 5 s.

### 6.3 Contexto da party e mapa de nomes

- `CompanionPartyContext` seleciona a party local, em ordem de preferência:
  1. `_pc != null && _pc.Object.HasInputAuthority`;
  2. `_pc != null && _hasAuthority`;
  3. `_pc != null` (single-player, uma só);
  4. nenhuma -> `Ready=false`; ordens recusam com "crie a party pela UI".
- Mantém `Dictionary<string, Villager>` (case-insensitive, `Trim`), reconstruído quando a
  assinatura de `_partyMembers` muda (1 Hz), não por frame.
- **Chamar `CompanionRegistry.Refresh()` antes de `Register`.** O primeiro `Refresh` compara
  com `_lastConfig` vazio e reescreve `_names` a partir do `.cfg`; se o probe registrar
  `Maria` antes disso, o primeiro refresh a apaga. O `LogParty` chama `Refresh` quando
  `count > 0`, imediatamente antes dos `Register`.
- O probe **não guarda o wrapper** da party: a cada `Tick`, `FindAnyObjectByType<PlayerParty>()`
  reencontra a instância e a chave é `GetInstanceID()`. Se não houver party, loga
  `state=NoParty` uma vez e não mexe em wrapper antigo (elimina o dead-end #16).
- Nomes duplicados: se duas entradas tiverem o mesmo nome, recusar a ordem por nome e pedir
  o nome completo/renomear; não escolher "o primeiro" silenciosamente.

### 6.4 API de ordens (pura)

```csharp
internal enum PartyOrderResult { Ok, NotReady, UnknownVillager, Dead, Full, NoAuthority, NotMember, NativeError }

internal static PartyOrderResult TryFollow(PlayerParty party, Villager v, out string reason)
internal static PartyOrderResult TryStop(PlayerParty party, Villager v, out string reason)
internal static PartyOrderResult TryCome(PlayerParty party, Villager v, out string reason)
```

Regras:
- A função só chama o nativo; não loga em excesso, não faz retry, não move o jogador.
- O chamador (chat/LLM) transforma o resultado em fala/chat.
- `follow` valida `HasParty`/`GetParty`; `stop` usa o sentinela confirmado na fase 2;
  `come` só existe se a fase 5 aprovar.
- Nunca chamar `SetTaskAgent` como fallback automático.

### 6.5 Comando de debug

- Config `AI/DebugPartyCommands` (bool, default `false`).
- Texto exato, sem LLM:
  - `/party status`
  - `/party join <nome>`
  - `/party leave <nome>` (usa o sentinela confirmado)
  - `/party stop <nome>` (aborta a ordem atual, para comparar com o vanilla)
- Fila `ConcurrentQueue<PartyCommand>`; `CompanionBrainRunner.Update` drena no máximo 1 por
  segundo, na main thread.
- Gate de segurança:
  - `ModConfig.Enabled` e `DebugPartyCommands`;
  - `party` local pronta e `_pc != null`;
  - `villager.Object != null && villager.Object.HasStateAuthority`;
  - `Time.unscaledTime > _worldReadyAt + 10f` (evita o load);
  - `!villager.IsDead`.
- Uma escrita por comando; se falhar, não repetir. O log diz o motivo.
- Não executar dentro do patch do chat: o callback do chat pode estar no meio do submit da
  UI; a fila garante o `Update` posterior.

### 6.6 Config

Adicionar em `ModConfig`:

```text
AI/DebugPartyCommands (bool, false)
AI/DebugPartyCommandCooldownSeconds (float, 1.0)
```

Não adicionar `DebugUnlockParty` a release; manter `false` por padrão. O `.cfg` não recarrega
sozinho (dead-end #14): para o teste, reiniciar o jogo depois de mudar.

### 6.7 `PlayerHousePartyUnlock` - validar a implementação atual (poll, sem Harmony)

O crash do unlock antigo **não foi** AOT/inline. `PlayerHouse._OnActivate` aponta para um
**thunk compartilhado** em `GameAssembly + 0x8009B0`, com **29 aliases** (`WeatherManager.Awake`,
`TaskManager.Awake`, `SettlementManager.Awake`, ...). O patch Harmony disparou **16x** - em
métodos diferentes - e o detour materializou `this` como `PlayerHouse` sem validar a classe
nativa; `__instance is PlayerHouse` é sempre verdadeiro. A escrita do bool caiu em objetos de
outras classes e corrompeu memória. Detalhes: `docs/re/party-gate-crash.md`.

O working tree de 2026-10-29 já implementou o desenho correto (sem Harmony):

1. `PlayerHousePartyUnlock.Apply()` só arma o poll (`_enabled = true`); não toca no jogo.
2. `PlayerHousePartyUnlock.Tick()` é chamado pelo `CompanionBrainRunner.Update` e, após
   `MinSecondsAfterBoot` (5 s) + `SettleSeconds` (5 s), faz `Attempt()`.
3. `Attempt()` usa `FindAnyObjectByType<PlayerHouse>()` **singular** (a variante plural estoura
   `MissingMethodException`, dead-end #10/#19), exige `OwnerStructure != null` e cena carregada,
   lê o bool e só então escreve `true`. Sem Harmony, sem `Initialize`, sem prefab.
4. `Unapply()` desarma o poll e **não** desfaz a escrita (evita outro toque em estado do jogo);
   o bool volta ao normal no reload do mundo/save.

**Fire-verify já confirmado (2026-10-29 18:43):**

```text
[AIC][PARTY] DebugUnlockParty armed (no Harmony patch)
[AIC][DEBUG] [PARTY] unlock poll: id=0 state=NoHouse
```

No menu, sem crash e sem `Disarm` -> `FindAnyObjectByType<PlayerHouse>()` não lançou
`MissingMethodException`.

**O que falta validar (não é "fix" ainda):**

- escrita do bool em uma `PlayerHouse` real em jogo, com a linha
  `PlayerHouse allowPartyManagement: False -> true (debug unlock, trigger=post-load)`;
- releitura do bool em 1 Hz pelo probe tipado (`houseAllow=True`) depois do load;
- `GetAgentsCapacity() > 0` com o bool true (se continuar 0, o bool não é o único gate);
- comportamento em quit-to-menu/reload (o poll reaplica? o bool volta a false?);
- `FindAnyObjectByType<PlayerHouse>()` pode achar uma casa que não é a do jogador; logar nome/
  `OwnerStructure` e, se houver mais de uma, escolher pela casa do jogador (a confirmar).

Se o poll estabilizar, manter. Se o crash voltar, **abandonar o unlock de dev** e exigir a
Cabana do Chefe lvl 2. Não vale arriscar o save por conveniência de teste.

### 6.8 Autoridade e co-op

- Single-player: o villager é state authority; `villager.Object.HasStateAuthority` deve ser
  `true`. É o gate prático para o RPC.
- `PlayerParty._hasAuthority` é um bool manual; o probe antigo **relatava** `false` por artefato
  de `GetField`, então não tratar isso como observação. Não usar esse bool sozinho como prova de
  autoridade: logar `_hasAuthority` e `villager.Object.HasStateAuthority`.
- `PlayerParty` não é `NetworkBehaviour`; a membership é estado do villager
  (`__PlayerPartyID`). Em co-op, cada cliente tem sua `PlayerParty` local espelhando os
  villagers; a escrita precisa passar pelo RPC do villager.
- Cliente não escreve `_partyMembers`, `_playerParty` nem `__PlayerPartyID` diretamente.
- Se o RPC nativo não puder ser chamado por um cliente, o mod precisa de um RPC próprio
  host-relay (fora desta branch). Até lá: cliente só pede, host executa.
- Testar com dois jogadores só depois de single-player 100% confirmado; um villager só pode
  estar em uma party (`__PlayerPartyID` é um único id).

### 6.9 Persistência

- Nativo: `PlayerParty.Serialize/Deserialize(DataObject)` com `c_PartyMembersKey`; o lambda
  interno procura `Villager` por `villagerPersistentID`. O villager também carrega
  `__PlayerPartyID` no estado de rede.
- Não escrever no save, não editar `c_PartyMembersKey`, não duplicar IDs.
- Após o join, salvar pelo jogo e recarregar. Se `_partyMembers` restaurar mas
  `_followQuest` não estiver registrada, é init incompleto: logar e esperar; **não** chamar
  `AddQuest` manualmente até entender o caminho nativo (pode duplicar).
- Morte/despawn: ignorar nulos e mortos no probe; remover do mapa de nomes; a ordem recusa.
  O `OnVillagerRemoved` nativo deve limpar `_partyMembers`, mas validar.

### 6.10 Performance e segurança

- Polling 1 Hz; nenhum `FindObjects*`; nenhum loop por frame em villagers.
- Não patchar `QuestRunner.Update`, `Character.Spawned`, `CopyBackingFieldsToState`,
  `CopyStateToBackingFields`, lifecycle de `Interaction` ou a família de inventário.
- Não usar `Harmony.PatchAll` nem `typeof(GameType)` em atributos de patch.
- Não teletransportar, não duplicar item, não ignorar fome/stamina.
- Não criar segunda `PartyFollowQuest`/`PartyInteractQuest`/`PartyCombatQuest`.
- O probe é **100% poll**: `FindAnyObjectByType<PlayerParty>()` singular, 1 Hz, sem Harmony e
  sem postfix de lifecycle. A leitura das propriedades fica no `Tick`, fora de callbacks nativos.
- Fire-verify cada patch novo com uma linha de log na primeira execução.

---

## 7. Riscos e incógnitas

| # | Incógnita | Status | Como confirmar |
|---|---|---|---|
| Q1 | `Rpc_ChangePlayerParty` é o entry point real e faz todo o apply? | hipótese forte | fase 1 (vanilla) + fase 3 (mod) |
| Q2 | `SetTaskAgent` sozinho é apply parcial? | hipótese | fase 4 (uma vez, sem salvar) |
| Q3 | A party é criada no primeiro join ou já existe vinculada? | pendente | fase 0/1: logar `_pc`/`_networkParty` antes/depois |
| Q4 | `GetAgentsCapacity()` lê o quê? O bool libera a capacidade? | pendente | fase 0: logar `cap` com `allow=false/true` |
| Q5 | Sentinela de "sem party" | pendente | fase 2: ler `__PlayerPartyID` após dismiss |
| Q6 | `stop` vanilla remove ou só aborta? | pendente | fase 2: contar membros |
| Q7 | `_followQuest` nasce quando? Quem a registra no `QuestRunner`? | pendente | fase 1: `GetQuestData` antes/depois |
| Q8 | `FollowPlayerTask` via `StartTask` é viável com a party ativa? | pendente | fase 5, isolado |
| Q9 | RPC funciona em cliente/co-op? | pendente | fase 6 (fora desta branch) |
| Q10 | O poll pós-load de `PlayerHouse` estabiliza sem crash? | pendente | load com log; sem "fix" presumido |
| Q11 | `PlayerPartyWidget`/UI têm algum "create party" escondido? | não há método de create na superfície | fase 1 observa a UI |
| Q12 | O save restaura quests ou só a lista? | pendente | fase 5 save/reload |

---

## 8. Critérios de aceite

- [ ] Fase 0: `allowPartyManagement=true` estável (sem crash), party local identificada,
      `capacity>0`.
- [ ] Fase 1: primeiro join vanilla aumenta `members` em 1 (a partir do valor real lido pelo
      probe tipado), registra `_followQuest` no membro e Thorin segue.
- [ ] Fase 2: segundo join vanilla idem; semântica de `stop`/`dismiss` documentada com o
      sentinela exato.
- [ ] Fase 3: `/party join Maria` produz o mesmo estado que o join vanilla (contagem, nome,
      `GetParty`, `__PlayerPartyID`, quests, marcador, follow).
- [ ] Fase 3b: `/party leave Maria` remove e devolve ao trabalho; sem resíduo de quest.
- [ ] `Thorin, segue` / `Thorin, para` / (se aprovado) `Thorin, vem` funcionam por nome, com
      Maria como segundo alvo; nome inválido é recusado com mensagem clara.
- [ ] Save/reload mantém a party e o follow sem comando extra.
- [ ] Morte/despawn de um membro não crasha e não deixa referência morta.
- [ ] Nenhum crash nativo; `LogOutput.log` com as linhas `[AIC][PARTY]` do run.
- [ ] Evidência registrada em `docs/re/party-unlock-verification.md` e
      `villager-ai-and-party.md` atualizado.

---

## 9. Próximos passos (handoff)

1. **Dev IL2CPP:** validar o poll já implementado (`PlayerHousePartyUnlock.Tick`, sem Harmony)
   em load completo; se o crash voltar, mapear o RVA `0xF72955` e considerar abandonar o
   unlock de dev. Não declarar fix sem load completo.
2. **Dev villager AI:** o `CompanionPartyProbe` foi corrigido para **acesso tipado**
   (`party._pc`, `_partyMembers`, `_partyPets`, `_hasAuthority`, `_followQuest`); reobservar o
   estado real antes de qualquer conclusão (o `members=0 authority=False pc=?` anterior era
   artefato). Depois, estender o probe (§6.2) e o contexto (§6.3), implementar
   `CompanionPartyCommand` atrás de config e rodar fases 0-2 sem escrita.
3. **Jogador/QA:** rodar fase 1 (vanilla) primeiro, enviar as linhas `[AIC][PARTY]`, o nome do
   save e o resultado. Só depois habilitar o comando de debug.
4. **Quando retomar:** implementar `follow`/`stop` em `CompanionPartyOrders` e ligar ao LLM;
   `come` só após a fase 5. A branch fica **pausada, sem merge**, até a confirmação em jogo.
5. **Se o RPC falhar:** registrar em `docs/re/dead-ends.md` com sintoma exato e manter a
   criação manual pela UI; não gravar `_partyMembers` na mão.

---

## 10. Resumo dos caminhos nativos (cola rápida)

```text
Entrar:   villager.Rpc_ChangePlayerParty(party._networkParty.Object.Id)
Sair:     villager.Rpc_ChangePlayerParty(<sentinela confirmado>)
Seguir:   PartyFollowQuest registrada por membro (SetTaskAgent/RPC) - não criar outra
Parar:    sair da party (recomendado) OU party.TryAbortOrder(villager) (só ordem atual)
Vir:      FollowPlayerTask(party, 1f, 2.5f) + villager.GetTaskRunner().StartTask(task) [experimento]
Atacar:   party.FocusOnTarget(IAttackTarget)  [fase futura]
Coletar:  party.TryOrderToInteract(Interaction)  [fase futura]
Checar:   party._partyMembers, villager.GetParty()/HasParty(), __PlayerPartyID,
          villager.GetQuestRunner().GetQuestData(party._followQuest),
          GetActiveQuest() (classe nativa), distância ao _pc
```
