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
            "Liga/desliga o mod. Nesta versao 0.1.0 nao ha gameplay para desligar.");

        VerboseLog = config.Bind(
            "General", "VerboseLog", true,
            "Log detalhado (prompt/JSON/intencoes). Desligue antes de publicar.");

        Language = config.Bind(
            "General", "Language", "auto",
            "Idioma do mod: auto (segue o jogo), pt-BR ou en.");

        // AI
        BaseUrl = config.Bind(
            "AI", "BaseUrl", "http://127.0.0.1:1234/v1",
            "Endpoint OpenAI-compatible. Padrao: LM Studio local. Use 127.0.0.1, nao localhost.");

        Model = config.Bind(
            "AI", "Model", "qwen2.5-3b-instruct",
            "Identificador do modelo carregado no LM Studio.");

        ApiKey = config.Bind(
            "AI", "ApiKey", "",
            "Chave do provedor. Vazia no LM Studio. Nunca commite este valor.");

        Temperature = config.Bind(
            "AI", "Temperature", 0.3f,
            "Temperatura do LLM (0 = deterministico).");

        TimeoutSeconds = config.Bind(
            "AI", "TimeoutSeconds", 30,
            "Timeout por requisicao ao LLM, em segundos.");

        ChatPrefix = config.Bind(
            "AI", "ChatPrefix", "/c",
            "Prefixo que endereca uma mensagem ao companheiro no chat. Vazio = desligado.");

        AddressByName = config.Bind(
            "AI", "AddressByName", true,
            "Tambem aceitar mensagens que comecam com o nome do companheiro.");

        Memory = config.Bind(
            "AI", "Memory", 4,
            "Quantas trocas recentes o companheiro lembra (0 = desligado, maximo 12).");

        Personality = config.Bind(
            "AI", "Personality", "",
            "Personalidade em texto livre (PT-BR). Vazio = padrao.");

        // Voice
        VoiceEnabled = config.Bind(
            "Voice", "Enabled", false,
            "Liga o push-to-talk (STT). Ainda nao implementado na 0.1.0.");

        PushToTalkKey = config.Bind(
            "Voice", "PushToTalkKey", "V",
            "Tecla de push-to-talk.");

        SttModel = config.Bind(
            "Voice", "SttModel", @"whisper\ggml-small-q5_1.bin",
            "Caminho do modelo ggml do Whisper, relativo a pasta do plugin.");

        TtsModel = config.Bind(
            "Voice", "TtsModel", @"tts\kokoro\model.int8.onnx",
            "Caminho do modelo Kokoro, relativo a pasta do plugin.");

        TtsVoice = config.Bind(
            "Voice", "TtsVoice", 0,
            "Voz Kokoro: 0 = automatica do idioma; 42 = pf_dora; 43 = pm_alex; 44 = pm_santa.");

        TtsVolume = config.Bind(
            "Voice", "TtsVolume", 1.0f,
            "Volume da voz do companheiro.");

        TtsSpeed = config.Bind(
            "Voice", "TtsSpeed", 1.0f,
            "Velocidade da voz (0.5 a 2.0).");

        // UI
        PanelKey = config.Bind(
            "UI", "PanelKey", "I",
            "Tecla do painel da IA (a partir da fase de UI).");

        ShowHud = config.Bind(
            "UI", "ShowHud", true,
            "Mostrar o HUD do companheiro (a partir da fase de UI).");
    }
}
