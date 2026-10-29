---
name: Voz local (Whisper + Kokoro)
description: Stack de voz do mod — Whisper.net para STT, sherpa-onnx/Kokoro para TTS, captura de microfone, VAD/trim, filas, playback 3D, nativos, modelos e licenças. Use ao implementar ou depurar voz.
---

# Voz local — Whisper (STT) + Kokoro (TTS)

A voz é **100% local**: transcrição in-process com Whisper.net e síntese com Kokoro-82M via
sherpa-onnx. Nada de áudio saindo da máquina.

## Quando usar

Sempre que o trabalho envolver: push-to-talk, microfone, transcrição, síntese, playback,
modelos ggml/onnx, nativos, latência de voz, eco ou empacotamento de modelos.

## Stack

| Parte | Tecnologia | Modelo/pasta |
|---|---|---|
| STT | Whisper.net + whisper.cpp | `whisper\ggml-small-q5_1.bin` (181 MB) |
| TTS | sherpa-onnx + Kokoro-82M | `tts\kokoro\model.int8.onnx` + `voices.bin` + `tokens.txt` + `espeak-ng-data` |
| Captura | `UnityEngine.Microphone` | 16 kHz mono, WAV PCM 16-bit |
| Playback | `AudioSource` 3D | na posição do companheiro |

## Regras

- **Main thread livre:** STT/TTS em background; filas drenadas no `Update`.
- **Um modelo por vez:** `NativeGate` para STT; worker único para TTS.
- **Modelo ausente:** degrade com aviso; chat continua.
- **Nativos:** copie `runtimes\win-x64` para a pasta do plugin e pré-carregue por caminho
  absoluto; restaure o diretório depois.
- **Playback:** uma fala por vez; `AudioClip` destruído ao terminar; `reverbZoneMix = 0` se o
  eco do ASKA atrapalhar.
- **Microfone:** não grave com chat/console/UI de texto ativo.
- **Idioma:** segue o idioma do jogo; voz explícita de outro idioma não é usada.
- **Licenças:** espeak-ng data é GPL-3.0; se empacotar, leve o texto.
- **Tamanho:** modelos são grandes; não commitar; pacote “sem modelos” é uma opção.

## Referências

- `references/whisper-stt.md` — factory, opções, prompt, idioma, gate, latência.
- `references/kokoro-tts.md` — OfflineTts, vozes, cache, worker, playback, RTF.
- `references/packaging.md` — nativos, pastas, licenças, tamanho, instalação.
