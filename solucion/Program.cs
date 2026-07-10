using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

// ─────────────────────────────────────────────────────────────────────────────
// MiniTeams — app de REFERENCIA / respaldo del guion en vivo.
// Implementa EXACTAMENTE el contrato del hub que espera el simulador:
//   /hub/chat?user=<nombre>
//   C→S: SendMessage(text), SetTyping(isTyping), SendReaction(emoji)
//   S→C: ReceiveMessage(user, text, sentAt), UserTyping(user, isTyping), PresenceChanged(online[]), ReceiveReaction(user, emoji)
// Videollamada 1:1 (señalización WebRTC; el vídeo va P2P, no por el server):
//   C→S: CallUser/AcceptCall/DeclineCall/HangUp(targetUser), SendSignal(targetUser, signal)
//   S→C: IncomingCall/CallAccepted/CallDeclined/CallEnded(fromUser), ReceiveSignal(fromUser, signal)
// Historial: EF Core + SQLite (miniteams.db). Al conectar, recibes los últimos 50.
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
// Identifica cada conexión por su ?user= para poder dirigir la señalización con Clients.User(nombre).
builder.Services.AddSingleton<IUserIdProvider, NameUserIdProvider>();
builder.Services.AddDbContext<ChatDb>(o => o.UseSqlite("Data Source=miniteams.db"));

var app = builder.Build();

// Crea la base (sin migraciones, para la clase).
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<ChatDb>().Database.EnsureCreated();

app.UseDefaultFiles();   // sirve wwwroot/index.html en la raíz
app.UseStaticFiles();
app.MapHub<ChatHub>("/hub/chat");
app.Run();

/// <summary>Mensaje persistido en SQLite.</summary>
sealed class Message
{
    public int Id { get; set; }
    public string User { get; set; } = "";
    public string Text { get; set; } = "";
    public string SentAt { get; set; } = "";   // ISO 8601
}

/// <summary>Contexto EF Core con un único archivo SQLite.</summary>
sealed class ChatDb(DbContextOptions<ChatDb> options) : DbContext(options)
{
    public DbSet<Message> Messages => Set<Message>();
}

/// <summary>Hub de chat en tiempo real para un único canal "general".</summary>
sealed class ChatHub(ChatDb db) : Hub
{
    // connectionId -> nombre de usuario (presencia en memoria).
    private static readonly ConcurrentDictionary<string, string> Online = new();

    private string User => Online.TryGetValue(Context.ConnectionId, out var u) ? u : "?";
    private static string[] OnlineUsers => Online.Values.Distinct().OrderBy(x => x).ToArray();

    public override async Task OnConnectedAsync()
    {
        var raw = Context.GetHttpContext()?.Request.Query["user"].ToString();
        var user = string.IsNullOrWhiteSpace(raw) ? "Anónimo" : raw!;
        Online[Context.ConnectionId] = user;
        Console.WriteLine($"[+] {user} conectado");

        // Le enviamos SOLO a quien acaba de entrar los últimos 50 mensajes (en orden).
        var history = await db.Messages
            .OrderByDescending(m => m.Id).Take(50)
            .ToListAsync(Context.ConnectionAborted);
        history.Reverse();
        foreach (var m in history)
            await Clients.Caller.SendAsync("ReceiveMessage", m.User, m.Text, m.SentAt);

        await Clients.All.SendAsync("PresenceChanged", OnlineUsers);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Online.TryRemove(Context.ConnectionId, out var user))
            Console.WriteLine($"[-] {user} desconectado");
        await Clients.All.SendAsync("PresenceChanged", OnlineUsers);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Un cliente envía un mensaje; se guarda y se reparte a todos al instante.</summary>
    public async Task SendMessage(string text)
    {
        var sentAt = DateTimeOffset.Now.ToString("o");
        db.Messages.Add(new Message { User = User, Text = text, SentAt = sentAt });
        await db.SaveChangesAsync(Context.ConnectionAborted);
        Console.WriteLine($"[msg] {User}: {text}");
        await Clients.All.SendAsync("ReceiveMessage", User, text, sentAt);
    }

    /// <summary>Notifica "está escribiendo…" a los demás.</summary>
    public Task SetTyping(bool isTyping) =>
        Clients.Others.SendAsync("UserTyping", User, isTyping);

    /// <summary>Una reacción (emoji) efímera; se reparte a TODOS para animarla en pantalla.
    /// No se guarda en la base: es puro espectáculo en tiempo real.</summary>
    public Task SendReaction(string emoji)
    {
        // Cinturón de seguridad: una reacción es un emoji corto, no un ensayo.
        if (string.IsNullOrEmpty(emoji)) return Task.CompletedTask;
        if (emoji.Length > 8) emoji = emoji[..8];
        return Clients.All.SendAsync("ReceiveReaction", User, emoji);
    }

    // ── Señalización de videollamada 1:1 ──
    // El hub NO transporta el vídeo: solo pone de acuerdo a dos navegadores (WebRTC hace el resto, P2P).
    public Task CallUser(string targetUser)    => Clients.User(targetUser).SendAsync("IncomingCall", User);
    public Task AcceptCall(string targetUser)  => Clients.User(targetUser).SendAsync("CallAccepted", User);
    public Task DeclineCall(string targetUser) => Clients.User(targetUser).SendAsync("CallDeclined", User);
    public Task HangUp(string targetUser)      => Clients.User(targetUser).SendAsync("CallEnded", User);

    /// <summary>Reenvía un "sobre" opaco (oferta/respuesta SDP o candidato ICE) al otro navegador.</summary>
    public Task SendSignal(string targetUser, string signal) =>
        Clients.User(targetUser).SendAsync("ReceiveSignal", User, signal);
}

/// <summary>Identifica al usuario de SignalR por el <c>?user=</c> para que <c>Clients.User(nombre)</c> funcione.</summary>
sealed class NameUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.GetHttpContext()?.Request.Query["user"].ToString();
}
