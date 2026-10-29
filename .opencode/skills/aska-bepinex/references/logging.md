# Logging

## Fonte

```csharp
internal static ManualLogSource Log = null!;
// no Load:
Log = base.Log;
```

Prefixo do projeto: `[AIC]`.

## Níveis

| Nível | Uso |
|---|---|
| `LogInfo` | boot, versão, feature habilitada, resultado de ordem |
| `LogWarning` | modelo ausente, LLM offline, fallback |
| `LogError` | exceção, patch não aplicado, falha de rede |
| `LogDebug` | detalhe de prompt/JSON; só com `VerboseLog` |

## Fire-verify de patch

```csharp
[HarmonyPostfix]
private static void Postfix()
{
    if (_logged) return;
    _logged = true;
    Log.LogInfo("[AIC][PATCH] ChatMenu.SendPlayerMessage fired");
}
```

- Uma linha na primeira execução; não por chamada.
- Se a linha não aparecer, o patch não disparou (AOT inlining ou alvo errado).

## O que logar

- Versão do mod, Unity, BepInEx, caminho do jogo.
- Se as interop assemblies existem.
- LLM: URL (sem key), modelo, ms, tokens, fallback usado.
- STT: modelo, idioma, ms, tamanho do áudio.
- TTS: modelo, voz, RTF, ms.
- Ordem: ação, alvo, resultado (sucesso/recusa).
- Performance: só o que passar de 2 ms (`[AIC][PERF]`).

## O que NÃO logar

- API key (masque).
- Texto integral de prompts em produção (pode conter conversa privada); em `VerboseLog`, ok.
- Áudio, caminhos pessoais desnecessários, saves.
- Uma linha por frame.

## Formato

```text
[AIC] AI Companions - ASKA 0.1.0 | Unity 6000.3.12f1 | BepInEx 6.0.0-be.791
[AIC][CHAT] mensagem recebida (12 chars)
[AIC][LLM] POST 127.0.0.1:1234/v1 -> 200 em 842 ms (schema)
[AIC][ORDER] follow -> PartyFollowQuest
[AIC][STT] 1.8 s de áudio -> 1.2 s, "segue"
[AIC][TTS] 1 frase, 410 ms, RTF 0.62
```

## Diagnóstico

- `LogOutput.log` é sempre gravado.
- `ErrorLog.log` para erros do BepInEx.
- Console do BepInEx: `[Logging.Console] Enabled = true` no `BepInEx.cfg` (próximo boot).
