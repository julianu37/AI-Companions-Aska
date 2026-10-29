using AICompanionsAska.AI;
using AICompanionsAska.Chat;

int failures = 0;
int checks = 0;

void Check(string name, bool condition, string? detail = null)
{
    checks++;
    if (condition)
    {
        Console.WriteLine($"PASS  {name}");
        return;
    }

    failures++;
    Console.WriteLine($"FAIL  {name}{(detail is null ? string.Empty : $" -> {detail}")}");
}

void ExpectAction(string name, string json, string action, string? target = null,
    string? resource = null, int? amount = null)
{
    if (!LlmIntent.TryParse(json, out LlmIntent intent, out string error))
    {
        Check(name, false, $"parse falhou: {error}");
        return;
    }

    bool ok = intent.Action == action &&
              intent.Target == target &&
              intent.Resource == resource &&
              intent.Amount == amount;
    Check(name, ok, intent.ToString());
}

// Aliases e variações
ExpectAction("follow", """{"action":"follow","reply":"Deixa comigo.","target":null,"resource":null,"amount":null}""", "follow");
ExpectAction("seguir", """{"action":"seguir","reply":"Vou.","target":null,"resource":null,"amount":null}""", "follow");
ExpectAction("para", """{"action":"para","reply":"Paro.","target":null,"resource":null,"amount":null}""", "stop");
ExpectAction("vir", """{"action":"vir","reply":"Já vou.","target":null,"resource":null,"amount":null}""", "come");
ExpectAction("atacar", """{"action":"atacar","reply":"Ataco.","target":"javali","resource":null,"amount":null}""", "attack", "javali");
ExpectAction("coletar", """{"action":"coletar","reply":"Já vou.","target":null,"resource":"madeira","amount":10}""", "gather", null, "wood", 10);
ExpectAction("equipar", """{"action":"equipar","reply":"Equipo.","target":"machado","resource":null,"amount":null}""", "equip", "machado");
ExpectAction("largar", """{"action":"largar","reply":"Largo.","target":"pedra","resource":null,"amount":5}""", "drop", "pedra", null, 5);
ExpectAction("inventory", """{"action":"inventory","reply":"Abrindo.","target":null,"resource":null,"amount":null}""", "inventory");
ExpectAction("none", """{"action":"none","reply":"Bom dia!","target":null,"resource":null,"amount":null}""", "none");

// "resource manda": action wood + resource stone -> gather stone
ExpectAction("resource manda", """{"action":"wood","resource":"stone","amount":5}""", "gather", null, "stone", 5);

// amount como string e 0 -> null
ExpectAction("amount string", """{"action":"gather","reply":"","target":null,"resource":"pedra","amount":"7"}""", "gather", null, "stone", 7);
ExpectAction("amount decimal", """{"action":"gather","reply":"","target":null,"resource":"pedra","amount":7.0}""", "gather", null, "stone", 7);
ExpectAction("amount zero", """{"action":"gather","reply":"","target":null,"resource":"madeira","amount":0}""", "gather", null, "wood", null);

// Code fence, texto ao redor, propriedades com caixa diferente
ExpectAction("code fence",
    "```json\n{\"Action\":\"follow\",\"Reply\":\"ok\",\"Target\":null,\"Resource\":null,\"Amount\":null}\n```",
    "follow");
ExpectAction("texto ao redor",
    "Claro! {\"action\":\"stop\",\"reply\":\"ok\",\"target\":null,\"resource\":null,\"amount\":null} fim",
    "stop");
ExpectAction("objeto extra depois",
    "{\"action\":\"stop\",\"reply\":\"ok\",\"target\":null,\"resource\":null,\"amount\":null} note {\"x\":1}",
    "stop");

// Inválidos
Check("ação desconhecida", !LlmIntent.TryParse("""{"action":"dance","reply":"","target":null,"resource":null,"amount":null}""", out _, out _));
Check("json invalido", !LlmIntent.TryParse("{action: follow", out _, out _));
Check("vazio", !LlmIntent.TryParse("   ", out _, out _));

// ToString não vaza quebras de linha
if (LlmIntent.TryParse("""{"action":"none","reply":"linha1\nlinha2","target":null,"resource":null,"amount":null}""", out LlmIntent multi, out _))
{
    Check("ToString sem newline", !multi.ToString().Contains('\n'), multi.ToString());
}
else
{
    Check("ToString sem newline", false, "parse falhou");
}

// Protocolo puro
Check("BuildUrl default",
    LlmProtocol.BuildUrl("http://127.0.0.1:1234/v1") == "http://127.0.0.1:1234/v1/chat/completions");
Check("BuildUrl trailing slash",
    LlmProtocol.BuildUrl("http://127.0.0.1:1234/v1/") == "http://127.0.0.1:1234/v1/chat/completions");
Check("BuildUrl ja completo",
    LlmProtocol.BuildUrl("https://x/v1/chat/completions") == "https://x/v1/chat/completions");
Check("BuildUrl host raiz",
    LlmProtocol.BuildUrl("http://127.0.0.1:1234") == "http://127.0.0.1:1234/v1/chat/completions");
Check("BuildUrl groq",
    LlmProtocol.BuildUrl("https://api.groq.com/openai/v1") == "https://api.groq.com/openai/v1/chat/completions");

string schema = LlmProtocol.BuildSchemaJson(LlmIntent.ValidActions);
Check("schema additionalProperties", schema.Contains("\"additionalProperties\":false"));
Check("schema enum follow", schema.Contains("\"follow\""));
Check("schema required", schema.Contains("\"action\"") && schema.Contains("\"amount\""));

Check("format rejection response_format", LlmProtocol.IsFormatRejection(400, "response_format is not supported"));
Check("format rejection json_object", LlmProtocol.IsFormatRejection(422, "json_object not allowed"));
Check("format rejection model", !LlmProtocol.IsFormatRejection(400, "model not found"));
Check("format rejection 500", !LlmProtocol.IsFormatRejection(500, "json_schema failed"));

// Endereçamento por nome (puro)
string[] one = { "Thorin" };
string[] two = { "Thor", "Thorin" };

Check("name virgula", NameAddressing.TryStrip("Thorin, bom dia", one, out string n1, out string r1) &&
                      n1 == "Thorin" && r1 == "bom dia");
Check("name dois pontos", NameAddressing.TryStrip("thorin: oi", one, out string n2, out string r2) &&
                         n2 == "Thorin" && r2 == "oi");
Check("name espaco", NameAddressing.TryStrip("Thorin bom dia", one, out string n3, out string r3) &&
                     n3 == "Thorin" && r3 == "bom dia");
Check("name sozinho", !NameAddressing.TryStrip("Thorin", one, out _, out _));
Check("name prefixo falso", !NameAddressing.TryStrip("Thorinzinho, oi", one, out _, out _));
Check("name vazio", !NameAddressing.TryStrip("Thorin,   ", one, out _, out _));
Check("name mais longo ganha", NameAddressing.TryStrip("Thorin, oi", two, out string n4, out string r4) &&
                              n4 == "Thorin" && r4 == "oi");
Check("sem nomes", !NameAddressing.TryStrip("Thorin, oi", Array.Empty<string>(), out _, out _));

Console.WriteLine();
Console.WriteLine($"{checks - failures}/{checks} checks passed.");
return failures == 0 ? 0 : 1;
