# Third-party notices

This project is MIT-licensed (see `LICENSE`). The following components are used
at build time or at runtime and keep their own licenses. Nothing listed here is
redistributed by this repository unless a release explicitly bundles it; release
packages must ship the corresponding license files.

## Mod loader and interop

| Component | License | Source |
|---|---|---|
| BepInEx 6 (IL2CPP) | LGPL-2.1 | https://github.com/BepInEx/BepInEx |
| Il2CppInterop | MIT | https://github.com/BepInEx/Il2CppInterop |
| HarmonyX (`0Harmony.dll`) | MIT | https://github.com/BepInEx/HarmonyX |
| MonoMod | MIT | https://github.com/MonoMod/MonoMod |
| Cpp2IL | MIT | https://github.com/SamboyCoding/Cpp2IL |

## Speech

| Component | License | Source |
|---|---|---|
| Whisper.net | MIT | https://github.com/sandrohanea/whisper.net |
| whisper.cpp | MIT | https://github.com/ggml-org/whisper.cpp |
| Whisper ggml models | MIT | https://github.com/openai/whisper |
| sherpa-onnx | Apache-2.0 | https://github.com/k2-fsa/sherpa-onnx |
| onnxruntime | MIT | https://github.com/microsoft/onnxruntime |
| Kokoro-82M | Apache-2.0 | https://huggingface.co/hexgrad/Kokoro-82M |
| espeak-ng data | GPL-3.0 (data) | https://github.com/espeak-ng/espeak-ng |

> The espeak-ng data is used by Kokoro for phonemization. If a release bundles
> it, the GPL text and the espeak-ng notices must travel with the package. This
> is a distribution obligation, not a license for our source code.

## LLM (development default)

| Component | License | Source |
|---|---|---|
| Qwen2.5-3B-Instruct | Apache-2.0 | https://huggingface.co/Qwen/Qwen2.5-3B-Instruct |
| LM Studio | Proprietary (free for personal use) | https://lmstudio.ai/ |

The model and LM Studio are **not** bundled. The mod talks to an
OpenAI-compatible endpoint on the user's machine.

## Research and prior art

| Project | License | Use |
|---|---|---|
| AI Companions (Valheim) | MIT | Our own AI/voice architecture and selected code |
| Offline Companions Reborn | MIT | Prior art; not bundled |
| askamods research notes | No license declared (facts only) | Factual ASKA/IL2CPP findings, verified locally |

If you believe a credit is missing or a license is misstated, open an issue.
