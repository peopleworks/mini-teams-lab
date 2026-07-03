using MiniTeams.Simulador;
using Microsoft.AspNetCore.SignalR.Client;

// ────────────────────────────────────────────────────────────────────────────
// Simulador de Equipo — conecta N "personas" al hub del mini-Teams y reproduce
// un guion (con "escribiendo…" y presencia reales). Si un HUMANO escribe algo
// con una palabra clave, un bot le responde.
//
//   dotnet run -- --api http://localhost:5099
//   dotnet run -- --api http://127.0.0.1:5099 --guion ..\otro-guion.json
// ────────────────────────────────────────────────────────────────────────────

string apiBase = GetArg("--api") ?? Environment.GetEnvironmentVariable("MINITEAMS_API") ?? "http://localhost:5000";
string? guionPath = GetArg("--guion");
var guion = GuionLoader.Load(guionPath);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine("\n🤖 Simulador de Equipo — MiniTeams");
Console.WriteLine($"   API: {apiBase}");
Console.WriteLine($"   Equipo simulado: {guion.Personas.Count} personas · {guion.Timeline.Count} líneas de guion\n");

// 1) Conectar cada persona al hub (identidad por ?user=<DisplayName>).
var bots = new Dictionary<string, Bot>(StringComparer.OrdinalIgnoreCase);
foreach (var p in guion.Personas)
{
    var bot = new Bot(p.DisplayName, p.Emoji, apiBase);
    try
    {
        await bot.ConnectAsync(cts.Token);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ No pude conectar a {apiBase}. ¿Está corriendo la app? ({ex.Message})");
        Console.WriteLine("   Tip: pasa el puerto real con  --api http://localhost:XXXX  (usa 127.0.0.1 si localhost falla).");
        return;
    }
    bots[p.Username] = bot;
    Console.WriteLine($"🟢 {p.Emoji} {p.DisplayName} en línea");
    await Task.Delay(400, cts.Token);
}
Console.WriteLine();

var personaNames = guion.Personas.Select(p => p.DisplayName).ToHashSet(StringComparer.OrdinalIgnoreCase);

// 2) Reacciones: un bot escucha el canal; si un HUMANO (no un bot) escribe algo
//    con un trigger, otro bot le responde. Pequeño anti-spam por tiempo.
var reactor = bots.Values.First();
var lastReaction = DateTimeOffset.MinValue;
reactor.Connection.On<string, string, string>("ReceiveMessage", async (user, text, _) =>
{
    if (personaNames.Contains(user)) return;                            // ignora a los propios bots
    if ((DateTimeOffset.UtcNow - lastReaction).TotalSeconds < 4) return; // no spamear
    var reply = MatchReaction(guion, text, user);
    if (reply is null) return;
    lastReaction = DateTimeOffset.UtcNow;
    var speaker = bots.Values.ElementAt(Random.Shared.Next(bots.Count));
    try { await speaker.SayAsync(reply, 900, cts.Token); } catch { /* cierre en curso */ }
    Console.WriteLine($"↩️  {speaker.Emoji} {speaker.DisplayName} → {user}: {reply}");
});

// 3) Reproducir el guion línea por línea.
Console.WriteLine("🎬 Reproduciendo el guion... (Ctrl+C para salir)\n");
foreach (var line in guion.Timeline)
{
    if (cts.IsCancellationRequested) break;
    if (!bots.TryGetValue(line.From, out var bot)) continue;
    try
    {
        await Task.Delay(line.DelayMs, cts.Token);
        await bot.SayAsync(line.Text, line.TypingMs, cts.Token);
    }
    catch (OperationCanceledException) { break; }
    Console.WriteLine($"{bot.Emoji} {bot.DisplayName}: {line.Text}");
}

// 4) Charla ociosa + respuestas, hasta Ctrl+C, para que el canal nunca se vea muerto.
if (!cts.IsCancellationRequested)
    Console.WriteLine("\n💬 Guion terminado. Charla ociosa + respuestas activas (Ctrl+C para salir)...\n");
try
{
    while (!cts.IsCancellationRequested && guion.IdleChatter.Count > 0)
    {
        await Task.Delay(Random.Shared.Next(12_000, 30_000), cts.Token);
        var bot = bots.Values.ElementAt(Random.Shared.Next(bots.Count));
        var text = guion.IdleChatter[Random.Shared.Next(guion.IdleChatter.Count)];
        await bot.SayAsync(text, 900, cts.Token);
        Console.WriteLine($"{bot.Emoji} {bot.DisplayName}: {text}");
    }
}
catch (OperationCanceledException) { /* salida limpia */ }

Console.WriteLine("\n👋 Cerrando bots...");
foreach (var b in bots.Values) await b.DisposeAsync();

// ──────────────────────────── helpers ────────────────────────────
string? GetArg(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static string? MatchReaction(Guion g, string content, string senderName)
{
    var low = content.ToLowerInvariant();
    foreach (var r in g.Reactions)
    {
        if (r.Triggers.Any(t => low.Contains(t.ToLowerInvariant())))
        {
            var reply = r.Replies[Random.Shared.Next(r.Replies.Length)];
            return reply.Replace("{name}", senderName);
        }
    }
    return null;
}

// ──────────────────────────── un bot ─────────────────────────────
/// <summary>Una persona simulada: su propia conexión SignalR al hub del mini-Teams.</summary>
sealed class Bot(string displayName, string emoji, string api) : IAsyncDisposable
{
    public string DisplayName => displayName;
    public string Emoji => emoji;

    public HubConnection Connection { get; } =
        new HubConnectionBuilder()
            .WithUrl($"{api.TrimEnd('/')}/hub/chat?user={Uri.EscapeDataString(displayName)}")
            .WithAutomaticReconnect()
            .Build();

    public Task ConnectAsync(CancellationToken ct) => Connection.StartAsync(ct);

    /// <summary>Muestra "escribiendo…", espera, y envía el mensaje — como una persona real.</summary>
    public async Task SayAsync(string text, int typingMs, CancellationToken ct)
    {
        await Connection.InvokeAsync("SetTyping", true, ct);
        await Task.Delay(typingMs, ct);
        await Connection.InvokeAsync("SetTyping", false, ct);
        await Connection.InvokeAsync("SendMessage", text, ct);
    }

    public async ValueTask DisposeAsync() => await Connection.DisposeAsync();
}
