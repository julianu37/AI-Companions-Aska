# Empacotamento de voz

## Estrutura da pasta do plugin

```
BepInEx\plugins\AI-Companions-Aska\
  AICompanionsAska.dll
  Whisper.net.dll
  sherpa-onnx.dll
  runtimes\win-x64\whisper.dll
  runtimes\win-x64\ggml-*.dll
  runtimes\win-x64\native\sherpa-onnx-c-api.dll
  runtimes\win-x64\native\onnxruntime.dll
  whisper\ggml-small-q5_1.bin        (opcional)
  tts\kokoro\model.int8.onnx         (opcional)
  tts\kokoro\voices.bin
  tts\kokoro\tokens.txt
  tts\kokoro\espeak-ng-data\...
  LICENSES\...
  Translations\pt-BR.json
  Translations\en.json
  Speech\pt-BR.json
  Speech\en.json
```

## Modelos

- **Nunca** commitar modelos no git (`*.bin`, `*.onnx`).
- Pacote completo (com modelos) vs “sem modelos” (download opcional): decidir na fase de
  release; o `README` deve dizer o que falta.
- Tamanho: Whisper small ~181 MB; Kokoro int8 ~80–90 MB + voices; espeak data ~10 MB.
- Se o modelo não existir, a feature degrada com aviso; não crasha.

## Nativos

- Copie a árvore `runtimes\win-x64` preservando a estrutura que o loader espera.
- Não dependa da pasta do jogo; use caminho absoluto na pasta do plugin.
- Um `DllImport` por nome resolve no processo depois do pré-carregamento.
- Se o módulo carregado não for o do plugin, logue (pode haver outra versão no PATH).

## Licenças

- Whisper.net/whisper.cpp: MIT.
- sherpa-onnx: Apache-2.0.
- onnxruntime: MIT.
- Kokoro-82M: Apache-2.0.
- espeak-ng data: **GPL-3.0** — se empacotar, inclua o texto da licença e os notices.
- BepInEx: LGPL-2.1; Il2CppInterop/HarmonyX: MIT.

## Instalação

1. BepInEx 6 IL2CPP na pasta do jogo; rodar uma vez.
2. Copiar a pasta do mod para `BepInEx\plugins\AI-Companions-Aska`.
3. Copiar os modelos para as subpastas `whisper\` e `tts\kokoro\` (ou usar o pacote completo).
4. Abrir o jogo e conferir o log.

## Checklist

- [ ] DLL + dependências gerenciadas.
- [ ] Nativos win-x64.
- [ ] Modelos (se incluídos) + licenças.
- [ ] Traduções/falas.
- [ ] README/CHANGELOG.
- [ ] Teste em pasta limpa (sem outras cópias).
