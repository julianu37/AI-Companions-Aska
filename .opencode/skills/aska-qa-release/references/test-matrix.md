# Matriz de testes por fase

## M0 — scaffold

- [ ] Build limpo.
- [ ] Plugin carrega; log com versão.
- [ ] Config criada.
- [ ] Sem crash ao entrar no mundo.

## M1 — chat + LLM + follow

- [ ] `/c segue` -> segue.
- [ ] `/c para` -> para.
- [ ] `/c bom dia` -> resposta.
- [ ] Sem prefixo -> ignora.
- [ ] LLM offline -> aviso, sem travar.
- [ ] JSON inválido -> fallback.
- [ ] Latência logada.
- [ ] Save/reload.

## M2 — persona/memória

- [ ] Preset muda o tom.
- [ ] Texto livre ganha do preset.
- [ ] Memória de 4 trocas.
- [ ] Memória 0 desliga.
- [ ] Não vaza entre companheiros.
- [ ] Limpa na morte/troca de mundo.

## M3 — ordens com alvo

- [ ] `attack <criatura>` com alvo válido.
- [ ] `attack` sem alvo -> recusa.
- [ ] `equip <item>` do inventário.
- [ ] `equip` item ausente -> recusa.
- [ ] `drop <item>`/quantidade.
- [ ] `gather <recurso> <n>` com teto.
- [ ] `amount` já atingido -> “já temos”.

## M4 — STT

- [ ] Push-to-talk “segue”.
- [ ] Silêncio -> nada.
- [ ] Ruído -> plausível/aviso.
- [ ] Modelo ausente -> aviso.
- [ ] Latência aceitável.
- [ ] Mesmo caminho do chat.

## M5 — TTS

- [ ] Fala a resposta.
- [ ] Fala a linha do mod (config).
- [ ] Uma por vez.
- [ ] Cache de linha fixa.
- [ ] Modelo ausente -> só texto.
- [ ] Eco controlado.
- [ ] Sem stutter.

## M6 — UI

- [ ] Abre/fecha com tecla/Esc/X.
- [ ] Salva config na hora.
- [ ] Input não vaza teclas.
- [ ] Não abre sobre menu/inventário.
- [ ] Fecha na morte/saída do mundo.

## M7 — co-op

- [ ] Host recruta; cliente vê.
- [ ] Cliente ordena.
- [ ] Host sai.
- [ ] Cliente entra no meio.
- [ ] Save/reload.
- [ ] Sem desync/crash.

## M8 — release

- [ ] Instalação limpa.
- [ ] Log limpo.
- [ ] Licenças.
- [ ] README/config.
- [ ] Nexus (se aprovado).
