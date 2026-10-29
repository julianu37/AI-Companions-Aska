using BepInEx.Configuration;

namespace AICompanionsAska.Core;

/// <summary>
/// Configuração inicial do mod. As seções e chaves são o contrato da fase 1;
/// nenhuma delas altera o jogo nesta versão.
/// </summary>
internal static class ModConfig
{
    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<bool> VerboseLog = null!;
    internal static ConfigEntry<string> Language = null!;

    internal static ConfigEntry<string> BaseUrl = null!;
    internal static ConfigEntry<string> Model = null!;
    internal static ConfigEntry<string> ApiKey = null!;
    internal static ConfigEntry<float> Temperature = null!;
    internal static ConfigEntry<int> TimeoutSeconds = null!;
    internal static ConfigEntry<string> ChatPrefix = null!;
    internal static ConfigEntry<bool> AddressByName = null!;
    internal static ConfigEntry<int> Memory = null!;
    internal static ConfigEntry<string> Personality = null!;

    internal static ConfigEntry<bool> VoiceEnabled = null!;
    internal static ConfigEntry<string> PushToTalkKey = null!;
    internal static ConfigEntry<string> SttModel = null!;
    internal static ConfigEntry<string> TtsModel = null!;
    internal static ConfigEntry<int> TtsVoice = null!;
    internal static ConfigEntry<float> TtsVolume = null!;
    internal static ConfigEntry<float> TtsSpeed = null!;

    internal static ConfigEntry<string> PanelKey = null!;
    internal static ConfigEntry<bool> ShowHud = null!;

    internal static void Bind(ConfigFile config)
    {
        // General
        Enabled = config.Bind(
            "General", "Enabled", true,
            "Turns the mod on/off. In 0.1.x it gates the chat capture hook (no LLM/voice yet).");

        VerboseLog = config.Bind(
            "General", "VerboseLog", true,
            "Verbose logging (ignored chat messages, prompt/JSON/intents). Turn it off before a release.");

        Language = config.Bind(
            "General", "Language", "auto",
            "Mod language: auto (follow the game), pt-BR or en.");

        // AI
        BaseUrl = config.Bind(
            "AI", "BaseUrl", "http://127.0.0.1:1234/v1",
            "OpenAI-compatible endpoint. Default: local LM Studio. Use 127.0.0.1, not localhost.");

        Model = config.Bind(
            "AI", "Model", "qwen2.5-3b-instruct",
            "Model identifier loaded in LM Studio.");

        ApiKey = config.Bind(
            "AI", "ApiKey", "",
            "Provider API key. Empty for LM Studio. Never commit this value.");

        Temperature = config.Bind(
            "AI", "Temperature", 0.3f,
            "LLM temperature (0 = deterministic).");

        TimeoutSeconds = config.Bind(
            "AI", "TimeoutSeconds", 30,
            "Timeout per LLM request, in seconds.");

        ChatPrefix = config.Bind(
            "AI", "ChatPrefix", "/c",
            "Prefix that addresses a chat message to the companion, e.g. \"/c follow me\". " +
            "Case-insensitive, trimmed, and must be followed by a space or the end of the message. " +
            "Empty = chat hook disabled.");

        AddressByName = config.Bind(
            "AI", "AddressByName", false,
            "Also accept messages that start with the companion name. " +
            "Reserved for a later phase: NOT implemented yet (only the prefix is captured). " +
            "Default false; enabling it now has no effect.");

        Memory = config.Bind(
            "AI", "Memory", 4,
            "How many recent exchanges the companion remembers (0 = off, max 12).");

        Personality = config.Bind(
            "AI", "Personality", "",
            "Free-text personality (PT-BR or EN). Empty = default.");

        // Voice
        VoiceEnabled = config.Bind(
            "Voice", "Enabled", false,
            "Turns push-to-talk (STT) on. Not implemented in 0.1.0.");

        PushToTalkKey = config.Bind(
            "Voice", "PushToTalkKey", "V",
            "Push-to-talk key.");

        SttModel = config.Bind(
            "Voice", "SttModel", @"whisper\ggml-small-q5_1.bin",
            "Whisper ggml model path, relative to the plugin folder.");

        TtsModel = config.Bind(
            "Voice", "TtsModel", @"tts\kokoro\model.int8.onnx",
            "Kokoro model path, relative to the plugin folder.");

        TtsVoice = config.Bind(
            "Voice", "TtsVoice", 42,
            "Kokoro speaker id. 42 = pf_dora (pt-BR female), 43 = pm_alex, 44 = pm_santa.");

        TtsVolume = config.Bind(
            "Voice", "TtsVolume", 1.0f,
            "Companion speech volume.");

        TtsSpeed = config.Bind(
            "Voice", "TtsSpeed", 1.0f,
            "Speech speed (0.5 to 2.0).");

        // UI
        PanelKey = config.Bind(
            "UI", "PanelKey", "I",
            "AI panel key (from the UI phase).");

        ShowHud = config.Bind(
            "UI", "ShowHud", true,
            "Show the companion HUD (from the UI phase).");
    }
}
