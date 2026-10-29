# Configuração (BepInEx `ConfigEntry`)

## Bind

```csharp
General.Enabled = Config.Bind(
    "General", "Enabled", true,
    "Liga/desliga o mod.");

AI.BaseUrl = Config.Bind(
    "AI", "BaseUrl", "http://127.0.0.1:1234/v1",
    "Endpoint OpenAI-compatible do LM Studio.");

AI.ApiKey = Config.Bind(
    "AI", "ApiKey", "",
    "Chave do provedor. Vazia no LM Studio. Nunca commite este valor.");
```

## Seções do projeto

| Seção | Chaves |
|---|---|
| `General` | `Enabled`, `VerboseLog`, `Language` |
| `AI` | `BaseUrl`, `Model`, `ApiKey`, `Temperature`, `TimeoutSeconds`, `ChatPrefix`, `AddressByName`, `Memory`, `Personality` |
| `Voice` | `Enabled`, `PushToTalkKey`, `SttModel`, `TtsModel`, `TtsVoice`, `TtsVolume`, `TtsSpeed` |
| `UI` | `PanelKey`, `ShowHud` |

## Boas práticas

- Nome e descrição em **inglês** (padrão BepInEx); a UI traduz.
- Default seguro e documentado.
- `AcceptableValueRange<T>` para sliders; `AcceptableValueList<T>` para enums.
- Não coloque segredo no default; `ApiKey` vazio.
- Não logue o valor de `ApiKey`; masque (`abcd…`).
- BepInEx **não relê** o `.cfg` sozinho. Se precisar de hot-reload:

```csharp
private float _nextReload;
void Update()
{
    if (Time.unscaledTime < _nextReload) return;
    _nextReload = Time.unscaledTime + 5f;
    Config.Reload(); // relê o arquivo
}
```

- Leia a config **depois** do reload e **antes** do gate de `Enabled`.
- Não faça `Config.Reload()` por frame.
- Textos que mudam comportamento devem ser lidos no momento do uso, não copiados para campos
  estáticos no boot.

## Arquivo

`BepInEx\config\julianu37.AICompanionsAska.cfg`

- O BepInEx cria no primeiro boot.
- O jogador pode editar com o jogo fechado.
- A UI do mod deve salvar na hora (`Config.Save()`), sem reiniciar.

## Migração

- Renomear uma chave = perder o valor do jogador. Se for inevitável, leia a antiga, migre e
  remova.
- Mudar o default de uma chave existente **não** altera o valor já salvo.
- Documente mudanças de config no `CHANGELOG.md`.
