using AICompanionsAska.AI;

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
ExpectAction("amount zero", """{"action":"gather","reply":"","target":null,"resource":"madeira","amount":0}""", "gather", null, "wood", null);

// Code fence, texto ao redor, propriedades com caixa diferente
ExpectAction("code fence",
    "```json\n{\"Action\":\"follow\",\"Reply\":\"ok\",\"Target\":null,\"Resource\":null,\"Amount\":null}\n```",
    "follow");
ExpectAction("texto ao redor",
    "Claro! {\"action\":\"stop\",\"reply\":\"ok\",\"target\":null,\"resource\":null,\"amount\":null} fim",
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

Console.WriteLine();
Console.WriteLine($"{checks - failures}/{checks} checks passed.");
return failures == 0 ? 0 : 1;
