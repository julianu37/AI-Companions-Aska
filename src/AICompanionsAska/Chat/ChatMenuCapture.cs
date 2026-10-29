namespace AICompanionsAska.Chat;

/// <summary>
/// Guarda a última instância viva de <c>SSSGame.UI.ChatMenu</c>, capturada no
/// prefix do chat. É por ela que o companheiro adiciona a resposta no chat
/// (<c>ChatMenu._AddNewTextLine</c>).
/// <para>
/// Não existe busca de cena aqui de propósito: <c>FindObjectsByType(Type, ...)</c>
/// lança <c>MissingMethodException</c> no JIT do método que a referencia e
/// aborta o load do plugin (dead-end registrado em <c>docs/re/dead-ends.md</c>).
/// O capture vem exclusivamente do prefix Harmony.
/// </para>
/// </summary>
internal static class ChatMenuCapture
{
    private static object? _instance;

    internal static object? Instance => _instance;

    internal static void Set(object instance) => _instance = instance;

    internal static void Clear() => _instance = null;
}
