# Referências e créditos

## Documentação oficial

- **BepInEx 6** — https://docs.bepinex.dev/ (plugin, configuração, logging, IL2CPP)
- **BepInEx (repositório)** — https://github.com/BepInEx/BepInEx
- **Il2CppInterop** — https://github.com/BepInEx/Il2CppInterop
- **HarmonyX** — https://github.com/BepInEx/HarmonyX
- **Cpp2IL** — https://github.com/SamboyCoding/Cpp2IL
- **Unity 6 Manual** — https://docs.unity3d.com/6000.0/Documentation/Manual/
- **Unity UI (uGUI)** — https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/
- **TextMeshPro** — https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/manual/
- **Photon Fusion 2** — https://doc.photonengine.com/fusion/current/

## LLM

- **LM Studio** — https://lmstudio.ai/
- **LM Studio — OpenAI compatibility** — https://lmstudio.ai/docs/api/openai-api
- **Qwen2.5-3B-Instruct** — https://huggingface.co/Qwen/Qwen2.5-3B-Instruct
- **OpenAI Chat Completions** — https://platform.openai.com/docs/api-reference/chat
- **JSON Schema** — https://json-schema.org/

## Voz

- **Whisper.net** — https://github.com/sandrohanea/whisper.net
- **whisper.cpp** — https://github.com/ggml-org/whisper.cpp
- **sherpa-onnx** — https://github.com/k2-fsa/sherpa-onnx
- **Kokoro-82M** — https://huggingface.co/hexgrad/Kokoro-82M
- **onnxruntime** — https://github.com/microsoft/onnxruntime

## ASKA / modding

- **ASKA (Steam)** — https://store.steampowered.com/app/1898300/ASKA/
- **ASKA Nexus** — https://www.nexusmods.com/games/aska
- **askamods (pesquisa pública)** — https://github.com/simpsonbm1/askamods
  - Notas de arquitetura/IL2CPP e dead-ends. **Fatos**, não código; verificar na nossa build.
- **ASKA UI (Nexus)** — https://www.nexusmods.com/aska/mods/83
- **Smart Villagers (Nexus)** — https://www.nexusmods.com/aska/mods/137

## Nosso projeto irmão

- **AI Companions (Valheim)** — https://github.com/julianu37/AI-Companions
  - Camada de IA/voz (LLM, intenção, persona, memória, Whisper.net, Kokoro/sherpa-onnx).
    Reaproveitamos a arquitetura e, onde marcado, o código (MIT).
- **Offline Companions Reborn** — https://github.com/MaddhattrRocks/Offline-Companions-Reborn
  - Prior art do mod do Valheim; **não** é portado para o ASKA.

## Ferramentas de RE usadas

- **ilspycmd** — superfície das interop assemblies (`-l`, `-t`).
- **Cpp2IL** — dump de corpos IL2CPP (metadata v39).
- **Ghidra / IDA / dnSpyEx** — análise nativa quando necessário.
- **Frida / REA** — probes em runtime (quando disponível).
- **Windows Error Reporting (WER)** — offsets de crash nativo.

## Como citar

Se você reutilizar ideias ou trechos, mantenha os créditos e as licenças. Se encontrar uma
afirmação neste repositório que não bate com a sua build, abra uma issue com a versão do jogo,
o log e o comando/teste que reproduz.
