using System.Collections.Concurrent;
using System.Data;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.Sqlite;
using MiniTeams.ColegaIA;

// ────────────────────────────────────────────────────────────────────────────
// Nova — el COLEGA CON IA del mini-Teams (Sesión 03).
//
// Es OTRO cliente del mismo contrato del hub (igual que el simulador): se conecta
// a /hub/chat?user=Nova, escucha ReceiveMessage y responde con SendMessage.
// La diferencia: donde el simulador tenía un guion fijo, Nova PIENSA — cada
// respuesta es una llamada real a la API de Claude, con una herramienta para
// buscar en el historial (la misma base SQLite que escribe la app).
//
//   set ANTHROPIC_API_KEY=sk-ant-...
//   dotnet run -- --api http://127.0.0.1:5210 --db ..\app-referencia\miniteams.db
// ────────────────────────────────────────────────────────────────────────────

string apiBase = GetArg("--api") ?? Environment.GetEnvironmentVariable("MINITEAMS_API") ?? "http://127.0.0.1:5210";
string dbPath  = GetArg("--db")  ?? "..\\app-referencia\\miniteams.db";
string model   = GetArg("--model") ?? Environment.GetEnvironmentVariable("CLAUDE_MODEL") ?? "claude-haiku-4-5-20251001";
string? apiKey = GetArg("--key") ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
const string Me = "Nova";

if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("❌ Falta la API key de Claude. Ponla en la variable ANTHROPIC_API_KEY (o pásala con --key).");
    Console.WriteLine("   PowerShell:  $env:ANTHROPIC_API_KEY = \"sk-ant-...\"   y vuelve a correr.");
    return;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var claude = new ClaudeClient(apiKey!, model);

Console.WriteLine("\n🧠 Nova — colega con IA de MiniTeams");
Console.WriteLine($"   Hub:    {apiBase}");
Console.WriteLine($"   Modelo: {model}");
Console.WriteLine($"   Base:   {dbPath} {(File.Exists(dbPath) ? "✓" : "(no la veo aún; usaré lo que escuche en el canal)")}\n");

// Lo que Nova ha "oído" en el canal (contexto de conversación en memoria).
var transcript = new ConcurrentQueue<string>();

// La herramienta que le damos a Claude: buscar en el historial real (SQLite, solo lectura).
var toolSpecs = new JsonArray
{
    new JsonObject
    {
        ["name"] = "buscar_historial",
        ["description"] = "Busca mensajes antiguos del canal por palabra clave. Úsala cuando te pregunten qué se dijo antes.",
        ["input_schema"] = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["termino"] = new JsonObject { ["type"] = "string", ["description"] = "Palabra o frase a buscar." },
                ["limite"]  = new JsonObject { ["type"] = "integer", ["description"] = "Máximo de resultados (por defecto 5)." },
            },
            ["required"] = new JsonArray { "termino" },
        },
    },
};
var tools = new Dictionary<string, Func<JsonObject, Task<string>>>(StringComparer.OrdinalIgnoreCase)
{
    ["buscar_historial"] = input => Task.FromResult(BuscarHistorial(dbPath, input)),
};

// Conexión al hub — idéntica a la del simulador.
await using var conn = new HubConnectionBuilder()
    .WithUrl($"{apiBase.TrimEnd('/')}/hub/chat?user={Uri.EscapeDataString(Me)}")
    .WithAutomaticReconnect()
    .Build();

var busy = 0;                 // evita que Nova conteste dos cosas a la vez.
var lastReply = DateTimeOffset.MinValue;

conn.On<string, string, string>("ReceiveMessage", (user, text, sentAt) =>
{
    if (user == Me) return;                        // no se escucha a sí misma.
    transcript.Enqueue($"{user}: {text}");
    while (transcript.Count > 30) transcript.TryDequeue(out _);

    // ¿La están llamando? Basta con nombrarla ("Nova" o "@nova").
    if (!MencionaANova(text)) return;
    if ((DateTimeOffset.UtcNow - lastReply).TotalSeconds < 2) return;    // anti-spam suave.
    if (Interlocked.Exchange(ref busy, 1) == 1) return;                  // ya está pensando.

    _ = ResponderAsync(user, text);
    return;
});

await conn.StartAsync(cts.Token);
Console.WriteLine($"🟢 {Me} en línea. Nómbrala en el chat (\"Nova, ...\") y te responde.\n   Ctrl+C para salir.\n");

try { await Task.Delay(Timeout.Infinite, cts.Token); }
catch (OperationCanceledException) { /* salida limpia */ }
Console.WriteLine("\n👋 Nova se desconecta.");

// ──────────────────────────── responder ────────────────────────────
async Task ResponderAsync(string user, string text)
{
    try
    {
        await conn.InvokeAsync("SetTyping", true, cts.Token);   // "Nova está escribiendo…" REAL.

        var contexto = string.Join("\n", transcript);
        var systemPrompt =
            "Eres Nova, una compañera de equipo con IA dentro de un chat estilo Microsoft Teams de PeopleWorks. " +
            "Hablas en español, cálida y BREVE (1-3 frases, como un mensaje de chat). No uses markdown pesado. " +
            "Si te preguntan por algo que se dijo antes y no está en el contexto reciente, usa la herramienta buscar_historial. " +
            $"Te diriges a la persona por su nombre cuando ayuda. Tú te llamas {Me}.";
        var userMessage =
            $"Conversación reciente del canal:\n{contexto}\n\n" +
            $"{user} te acaba de escribir: \"{text}\"\nResponde como Nova.";

        var reply = await claude.AskAsync(systemPrompt, userMessage, toolSpecs, tools, cts.Token);

        await conn.InvokeAsync("SetTyping", false, cts.Token);
        await conn.InvokeAsync("SendMessage", reply, cts.Token);
        lastReply = DateTimeOffset.UtcNow;
        Console.WriteLine($"🧠 Nova → {user}: {reply}");
    }
    catch (OperationCanceledException) { /* cierre en curso */ }
    catch (Exception ex) { Console.WriteLine($"⚠️  {ex.Message}"); }
    finally { Interlocked.Exchange(ref busy, 0); }
}

// ──────────────────────────── herramienta ────────────────────────────
// Lee la MISMA base SQLite que escribe la app (solo lectura). Si no existe, degrada con gracia.
static string BuscarHistorial(string dbPath, JsonObject input)
{
    var termino = input["termino"]?.GetValue<string>() ?? "";
    var limite = input["limite"]?.GetValue<int?>() ?? 5;
    limite = Math.Clamp(limite, 1, 20);
    if (!File.Exists(dbPath)) return "No tengo acceso al historial ahora mismo.";

    try
    {
        using var cn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;Cache=Shared");
        cn.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT User, Text FROM Messages WHERE Text LIKE $t ORDER BY Id DESC LIMIT $n";
        cmd.Parameters.AddWithValue("$t", $"%{termino}%");
        cmd.Parameters.AddWithValue("$n", limite);

        var sb = new StringBuilder();
        using var r = cmd.ExecuteReader();
        while (r.Read()) sb.AppendLine($"{r.GetString(0)}: {r.GetString(1)}");
        return sb.Length > 0 ? sb.ToString().Trim() : $"No encontré mensajes con \"{termino}\".";
    }
    catch (Exception ex) { return $"No pude leer el historial ({ex.Message})."; }
}

// ──────────────────────────── helpers ────────────────────────────
static bool MencionaANova(string text) =>
    text.Contains("nova", StringComparison.OrdinalIgnoreCase) ||
    text.Contains("@nova", StringComparison.OrdinalIgnoreCase);

string? GetArg(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
