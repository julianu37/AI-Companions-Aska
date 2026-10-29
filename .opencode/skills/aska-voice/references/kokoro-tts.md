# Kokoro-82M via sherpa-onnx — TTS local

## Pacotes

```xml
<PackageReference Include="org.k2fsa.sherpa.onnx" Version="1.13.8" />
<PackageReference Include="org.k2fsa.sherpa.onnx.runtime.win-x64" Version="1.13.8"
                  GeneratePathProperty="true" />
```

- O runtime win-x64 traz `runtimes\win-x64\native\sherpa-onnx-c-api.dll` + `onnxruntime.dll`.
- Copie os nativos para a pasta do plugin; pré-carregue por caminho absoluto antes do primeiro
  uso (`SetDllDirectory`/`LoadLibraryEx`), depois restaure.
- Modelo: `tts\kokoro\model.int8.onnx`, `voices.bin`, `tokens.txt`, `espeak-ng-data\`.

## Config

```csharp
var config = new OfflineTtsConfig();
config.Model.Kokoro.Model = modelPath;
config.Model.Kokoro.Voices = voicesPath;
config.Model.Kokoro.Tokens = tokensPath;
config.Model.Kokoro.DataDir = espeakDataDir;
config.Model.Kokoro.Language = "pt-br";
config.Model.NumThreads = 4;
config.MaxNumSentences = 1;
var tts = new OfflineTts(config);
```

- `Language` nunca vazio; Kokoro é multilíngue e a lib recusa sem idioma.
- Vozes PT-BR: `pf_dora` (42, fem.), `pm_alex` (43, masc.), `pm_santa` (44, masc.).
- `NumThreads` 4; medir RTF.

## Síntese

```csharp
var audio = tts.Generate(text, speakerId, speed);
// audio.Samples (float[]), audio.SampleRate
```

- Worker dedicado + `ConcurrentQueue`; teto de pedidos (ex.: 3).
- Frases: split por `. ! ? …`; sintetizar a 1ª e tocar enquanto a próxima é gerada.
- Cache por (texto, voz, velocidade) das linhas fixas; limite por bytes (ex.: 32 MB).
- Não cachear respostas únicas sem limite.

## Playback

- `AudioClip.Create` com `audio.Samples`; `AudioSource` 3D no companheiro.
- Uma fala por vez; destruir o clip ao terminar.
- `reverbZoneMix = 0` se o eco do ASKA atrapalhar.
- `Shutdown`: parar worker (`Join`), `Dispose` do `OfflineTts`; se o `Join` estourar, manter
  o modelo carregado para evitar AV.

## RTF

- RTF = tempo de síntese / duração do áudio. < 1 = mais rápido que tempo real.
- Kokoro int8 em CPU (4 threads): ~0,6–0,9.
- Logar por frase; se > 1, reduzir threads? (medir) ou usar modelo menor.

## Troubleshooting

| Sintoma | Causa | Solução |
|---|---|---|
| `DllNotFoundException` | nativos ausentes | copiar `runtimes\win-x64\native` |
| erro de idioma | `Language` vazio | `pt-br`/`en-us` |
| voz errada | speaker id | conferir `voices.bin` |
| áudio cortado | frase longa | split por frase |
| eco | reverb zone | `reverbZoneMix = 0` |
| crash ao sair | Dispose com worker vivo | parar/Join antes |
