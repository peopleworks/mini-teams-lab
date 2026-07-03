using System.Text.Json;

namespace MiniTeams.Simulador;

/// <summary>Una persona-bot. Se conecta al hub como /hub/chat?user=&lt;DisplayName&gt;.</summary>
public record Persona(string Username, string DisplayName, string Emoji = "🙂");

/// <summary>Una línea del guion: quién habla (Username), qué dice, y tiempos para que se sienta humano.</summary>
public record ScriptLine(string From, string Text, int DelayMs = 1500, int TypingMs = 1200);

/// <summary>Respuesta automática: si un HUMANO escribe algo con un trigger, un bot contesta.</summary>
public record Reaction(string[] Triggers, string[] Replies);

/// <summary>El guion completo, cargado desde guion.json.</summary>
public record Guion(
    List<Persona> Personas,
    List<ScriptLine> Timeline,
    List<Reaction> Reactions,
    List<string> IdleChatter);

/// <summary>Carga el guion desde disco (o usa uno embebido de respaldo).</summary>
public static class GuionLoader
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static Guion Load(string? path)
    {
        foreach (var candidate in Candidates(path))
        {
            if (candidate is not null && File.Exists(candidate))
            {
                Console.WriteLine($"📖 Guion: {Path.GetFullPath(candidate)}");
                return JsonSerializer.Deserialize<Guion>(File.ReadAllText(candidate), Opts)
                       ?? throw new InvalidOperationException("guion.json vacío o inválido.");
            }
        }

        Console.WriteLine("📖 Guion: (embebido — no se encontró guion.json)");
        return JsonSerializer.Deserialize<Guion>(Embedded, Opts)!;
    }

    private static IEnumerable<string?> Candidates(string? path)
    {
        yield return path;
        yield return "guion.json";
        yield return Path.Combine(AppContext.BaseDirectory, "guion.json");
    }

    /// <summary>Guion mínimo de respaldo, por si no aparece guion.json.</summary>
    public const string Embedded = """
    {
      "personas": [
        { "username": "ana",   "displayName": "Ana Torres", "emoji": "👩‍💻" },
        { "username": "luis",  "displayName": "Luis Peña",  "emoji": "🧑‍💻" },
        { "username": "botia", "displayName": "Bot IA",     "emoji": "🤖" }
      ],
      "timeline": [
        { "from": "ana",   "text": "¡Buenas! esto es un mini Teams en tiempo real 👀", "delayMs": 1200, "typingMs": 1200 },
        { "from": "luis",  "text": "hecho con .NET y SignalR 😄", "delayMs": 1500, "typingMs": 1200 },
        { "from": "botia", "text": "si alguien escribe algo, le respondo 🤖", "delayMs": 1500, "typingMs": 1200 }
      ],
      "reactions": [
        { "triggers": ["hola","buenas","hey"], "replies": ["¡Hola {name}! 👋", "¡Ey {name}! 🎉"] }
      ],
      "idleChatter": [ "127.0.0.1, no localhost 😉", "SignalR entregando en milisegundos ⚡" ]
    }
    """;
}
