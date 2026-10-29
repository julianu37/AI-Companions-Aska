# Whisper.net — STT local

## Pacotes

```xml
<PackageReference Include="Whisper.net" Version="1.9.1" />
<PackageReference Include="Whisper.net.Runtime" Version="1.9.1" />
```

- `Whisper.net` é o wrapper gerenciado; `Whisper.net.Runtime` traz os nativos em
  `runtimes\win-x64` (`whisper.dll`, `ggml-*.dll`).
- Em `net6.0`, os assemblies gerenciados vão para o output; copie-os para a pasta do plugin.
- Modelo ggml: `whisper\ggml-small-q5_1.bin` (~181 MB) ou `base` (~57 MB) para testes.

## Carregamento

```csharp
RuntimeOptions.LibraryPath = Path.Combine(pluginDir, "Whisper.net.dll");
RuntimeOptions.RuntimeLibraryOrder = new List<RuntimeLibrary> { RuntimeLibrary.Cpu };
var factory = WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions
{
    UseGpu = false
});
```

- Carregue **uma vez** e mantenha.
- `NativeGate` (lock) em volta de `ProcessAsync`: o nativo não é thread-safe.
- Se o modelo não existir, logue o caminho e desabilite a voz.

## Transcrição

```csharp
using var processor = factory.CreateBuilder()
    .WithLanguage("pt")
    .WithPrompt("segue, para, coleta madeira, ataca o javali, equipa o machado")
    .WithThreads(4)
    .Build();

using var wav = File.OpenRead(wavPath); // ou MemoryStream
var sb = new StringBuilder();
await foreach (var result in processor.ProcessAsync(wav))
    sb.Append(result.Text);
```

- `WithPrompt` com o vocabulário das ordens melhora nomes.
- `WithLanguageDetection()` se o idioma for `auto`.
- WAV: PCM 16-bit, 16 kHz, mono.
- Rodar em background; resultado por fila para a main thread.

## Áudio

- `Microphone.Start(device, false, maxSeconds, 16000)`.
- Trim de silêncio por RMS; descartar < 0,3 s.
- Converter samples float -> PCM16 e montar o header RIFF.
- Não gravar com chat/UI de texto ativo.

## Latência

- `small-q5_1` em CPU: ~1–3 s para 2–5 s de fala (depende da CPU).
- Pre-warm: carregar o modelo ao entrar no mundo, em background.
- Logar: duração do áudio, ms de transcrição, texto.

## Troubleshooting

| Sintoma | Causa | Solução |
|---|---|---|
| `DllNotFoundException` | nativos não copiados | copie `runtimes\win-x64` |
| modelo não encontrado | caminho errado | conferir `SttModel` |
| texto vazio | silêncio/limiar alto | baixar limiar, testar microfone |
| lento | CPU/ modelo grande | usar `base`/`small`, threads |
| acentos errados | idioma/prompt | fixar `pt` e prompt |
