using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiniTeams.ColegaIA;

/// <summary>
/// Cliente mínimo de la API de Mensajes de Claude (https://api.anthropic.com/v1/messages).
/// A propósito SIN SDK: se ve la forma real de la API (system + tools + messages) y el
/// "lazo de herramientas" (tool-use loop) que es EL concepto que queremos enseñar.
/// </summary>
sealed class ClaudeClient(string apiKey, string model)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private const string Endpoint = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";

    /// <summary>
    /// Manda la conversación a Claude y devuelve su respuesta en texto.
    /// Si Claude decide usar una herramienta, la ejecutamos y le devolvemos el resultado
    /// (repitiendo hasta que conteste con texto). <paramref name="tools"/> mapea nombre → ejecución.
    /// </summary>
    public async Task<string> AskAsync(
        string systemPrompt,
        string userMessage,
        JsonArray toolSpecs,
        IReadOnlyDictionary<string, Func<JsonObject, Task<string>>> tools,
        CancellationToken ct)
    {
        // Historial de la conversación con el modelo (crece con cada vuelta del lazo).
        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "user", ["content"] = userMessage },
        };

        // Máximo de vueltas al lazo de herramientas (cinturón de seguridad anti-bucle).
        for (var turn = 0; turn < 4; turn++)
        {
            var body = new JsonObject
            {
                ["model"] = model,
                ["max_tokens"] = 400,
                ["system"] = systemPrompt,
                ["messages"] = messages.DeepClone(),
            };
            if (toolSpecs.Count > 0) body["tools"] = toolSpecs.DeepClone();

            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            req.Headers.TryAddWithoutValidation("x-api-key", apiKey);
            req.Headers.TryAddWithoutValidation("anthropic-version", ApiVersion);
            req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var res = await Http.SendAsync(req, ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                return $"(No pude pensar ahora mismo: {(int)res.StatusCode}. {Trim(json)})";

            var root = JsonNode.Parse(json)!.AsObject();
            var content = root["content"]!.AsArray();
            var stopReason = root["stop_reason"]?.GetValue<string>();

            // Junta el texto que haya devuelto en esta vuelta.
            var text = new StringBuilder();
            foreach (var block in content)
                if (block?["type"]?.GetValue<string>() == "text")
                    text.Append(block!["text"]!.GetValue<string>());

            // ¿Quiere usar herramientas? Las ejecutamos y le devolvemos los resultados.
            if (stopReason == "tool_use")
            {
                // 1) Registramos su turno (el bloque assistant, tal cual).
                messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = content.DeepClone() });

                // 2) Ejecutamos cada tool_use y armamos los tool_result.
                var results = new JsonArray();
                foreach (var block in content)
                {
                    if (block?["type"]?.GetValue<string>() != "tool_use") continue;
                    var name = block!["name"]!.GetValue<string>();
                    var toolId = block["id"]!.GetValue<string>();
                    var input = block["input"]?.AsObject() ?? new JsonObject();

                    string output;
                    try { output = tools.TryGetValue(name, out var run) ? await run(input) : $"(herramienta desconocida: {name})"; }
                    catch (Exception ex) { output = $"(error en la herramienta: {ex.Message})"; }

                    results.Add(new JsonObject
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = toolId,
                        ["content"] = output,
                    });
                }
                messages.Add(new JsonObject { ["role"] = "user", ["content"] = results });
                continue; // otra vuelta: ahora Claude ya tiene los datos.
            }

            // Respuesta final en texto.
            return text.Length > 0 ? text.ToString().Trim() : "(me quedé sin palabras 😅)";
        }

        return "(estoy dándole muchas vueltas, mejor pregúntame de nuevo)";
    }

    private static string Trim(string s) => s.Length > 300 ? s[..300] + "…" : s;
}
