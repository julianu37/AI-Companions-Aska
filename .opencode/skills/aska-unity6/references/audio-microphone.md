# Áudio e microfone

## Microfone (Unity)

```csharp
const int SampleRate = 16000;
string device = null; // default
var clip = Microphone.Start(device, false, maxSeconds, SampleRate);
// aguardar Microphone.GetPosition(device) > 0
// ao soltar: Microphone.End(device)
```

- 16 kHz, mono; suficiente para Whisper.
- `loop: false`; limite de `maxSeconds` (ex.: 10 s).
- Trim de silêncio: RMS/limiar; descartar se muito curto.
- Converter para WAV PCM 16-bit em memória (RIFF header + samples).
- Não gravar com chat/console/UI de texto ativo.
- Listar devices (`Microphone.devices`) e permitir escolher por índice.

## STT

- Whisper.net: `WhisperFactory.FromPath(...)`, `CreateBuilder().WithLanguage(...)
  .WithPrompt(...).Build()`, `ProcessAsync(stream)`.
- Modelo carregado uma vez; `NativeGate` para serializar o nativo.
- Rodar em background; resultado volta por fila.
- Prompt inicial com o vocabulário das ordens (melhora nomes).
- Idioma: `pt`/`en` conforme o jogo; `auto` se não souber.

## TTS (Kokoro/sherpa-onnx)

- `OfflineTts` carregado uma vez; worker dedicado.
- Texto -> frases (split por `. ! ? …`), sintetizar a primeira e tocar enquanto a próxima é
  gerada.
- Cache por (texto, voz, velocidade) das linhas fixas; não cachear respostas únicas sem limite.
- `AudioClip.Create` com os samples; `AudioSource.PlayOneShot`/`Play`.
- Destruir o `AudioClip` ao terminar; uma fala por vez.

## Playback 3D

```csharp
var src = go.AddComponent<AudioSource>();
src.spatialBlend = 1f;
src.rolloffMode = AudioRolloffMode.Linear;
src.minDistance = 2f;
src.maxDistance = 30f;
src.volume = volume;
src.reverbZoneMix = 0f; // voz seca; ajuste se quiser ambiente
```

- O ASKA pode ter uma `AudioReverbZone` global; se a voz soar com eco, `reverbZoneMix = 0`.
- Não usar `AudioSource` do jogo; criar um próprio no companheiro.
- Pausar/parar ao morrer/sair do mundo.

## Diagnóstico

- Logar: device, sample rate, duração, ms de transcrição, texto.
- TTS: voz, idioma, ms, RTF, tamanho do áudio.
- Se o modelo não existir, logar o caminho esperado e seguir sem voz.
- Testar: silêncio, ruído, fala rápida, sotaque, volume baixo.
