# 💬 Mini-Teams Lab — construye tu propio chat en tiempo real con Claude Code

> Del curso **Build with Claude Code** de [PeopleWorks](https://peopleworks.github.io/build-with-claude-code/).
> En ~2 horas pasas de una carpeta vacía a un **chat estilo Microsoft Teams**: mensajes al instante,
> quién está en línea, "está escribiendo…", historial que sobrevive a la recarga y — el gran final —
> **un equipo entero de bots conversando** en tu canal.

**Stack:** ASP.NET Core (.NET 10) · SignalR (tiempo real) · cliente web HTML/CSS/JavaScript · EF Core + SQLite (cero configuración).

---

## 🎯 La idea de este lab

No te vamos a dar la app hecha para que la copies. Te damos **los prompts** y **el contrato**, y **Claude Code
construye el código contigo**. Así aprendes lo que de verdad importa: cómo dirigir a la IA con buenas instrucciones.

- La carpeta [`solucion/`](solucion/) es una **red de seguridad**: si te atoras, compara. (Intenta no verla antes 😉.)
- La carpeta [`simulador/`](simulador/) es el **efecto WOW** del final — bots que llenan tu canal de vida.

---

## ✅ Requisitos

- [SDK de .NET 10](https://dotnet.microsoft.com/download) → verifica con `dotnet --version`
- [Claude Code](https://claude.com/claude-code)
- Un navegador
- Windows/Mac/Linux (los ejemplos usan PowerShell, pero es igual en bash)

---

## ⚠️ Lo único que NO se toca: el contrato del Hub

Todo el mini-Teams gira alrededor de **5 nombres**. Si los respetas, el cliente web y el simulador de bots
encajan sin cambiar nada. Por eso lo escribimos en el `CLAUDE.md` **antes** de programar: para que Claude no invente otros.

| Dirección | Método | Firma |
|---|---|---|
| Cliente → Servidor | `SendMessage` | `(string text)` |
| Cliente → Servidor | `SetTyping` | `(bool isTyping)` |
| Servidor → Cliente | `ReceiveMessage` | `(string user, string text, string sentAt)` |
| Servidor → Cliente | `UserTyping` | `(string user, bool isTyping)` |
| Servidor → Cliente | `PresenceChanged` | `(string[] online)` |

- **Ruta del hub:** `/hub/chat`
- **Identidad:** el usuario llega en la query string → `/hub/chat?user=Ana`

---

## 🗺️ El plan (7 pasos)

| # | Paso | Resultado visible |
|---|------|-------------------|
| 0 | Proyecto + memoria (`CLAUDE.md`) | Claude sabe qué construimos |
| 1 | Backend en tiempo real (SignalR) | El hub vive |
| 2 | Cliente web estilo Teams | Dos ventanas chatean |
| 3 | Presencia + "escribiendo…" | Puntos 🟢 y el "…escribiendo" |
| 4 | Historial con SQLite | Recargas y los mensajes siguen |
| 5 | 🤯 Un equipo con guion (bots) | El canal cobra vida solo |
| 6 | Publicar (opcional) | Todos entran por una URL |

---

## ⏱️ PASO 0 — Proyecto + memoria

```powershell
mkdir MiniTeams
cd MiniTeams
git init
claude
```

Dentro de Claude, dale memoria: copia el archivo [`plantilla/CLAUDE.md`](plantilla/CLAUDE.md) de este repo
y pégalo como `CLAUDE.md` en la raíz de tu proyecto (o dile a Claude: *"crea este CLAUDE.md"* y pega el bloque).

> **Por qué primero la memoria:** el `CLAUDE.md` con el contrato es tu seguro. Sin él, Claude podría llamar
> a un método `Broadcast` en vez de `ReceiveMessage`, y el simulador del Paso 5 no encajaría. **El contrato primero.**

---

## ⏱️ PASO 1 — Backend en tiempo real (SignalR)

Empieza en **Plan Mode** (pulsa `Shift+Tab`) y luego pega:

```
Crea un proyecto ASP.NET Core (dotnet new web) llamado MiniTeams con SignalR.
Implementa un ChatHub en /hub/chat siguiendo EXACTAMENTE el contrato del CLAUDE.md:
- La identidad del usuario llega en ?user=<nombre> (léela en OnConnectedAsync).
- Mantén en memoria un conjunto de usuarios en línea (thread-safe).
- SendMessage(string text): reenvía a todos ReceiveMessage(user, text, sentAt-ISO).
- SetTyping(bool): reenvía a los demás UserTyping(user, isTyping).
- Al conectar/desconectar, emite PresenceChanged(online[]) a todos.
Registra SignalR en Program.cs y mapea el hub en /hub/chat. Sirve archivos estáticos desde wwwroot.
```

Corre y verifica que arranca sin errores:

```powershell
dotnet run --launch-profile http
```

> **Qué acabas de lograr:** SignalR mantiene un canal abierto (WebSocket) con cada cliente.
> `Clients.All.ReceiveMessage(...)` empuja a todos sin que pregunten. Esa es la diferencia con una API REST: **el servidor habla primero.**

---

## ⏱️ PASO 2 — El cliente web estilo Teams

```
Crea el cliente web en wwwroot (index.html, styles.css, app.js) estilo Microsoft Teams:
- Una pantalla de "login" simple: pide un nombre y un botón Entrar.
- Layout: barra lateral con el canal "general" y la lista de usuarios en línea; a la derecha, el chat.
- app.js usa el cliente JS de SignalR (agrega <script> del CDN de @microsoft/signalr).
  - Conecta a /hub/chat?user=<nombre>.
  - Al enviar el formulario: llama SendMessage(text).
  - Escucha ReceiveMessage(user, text, sentAt) y pinta la burbuja (mis mensajes a la derecha).
  - Escucha PresenceChanged(online) y actualiza la lista de usuarios.
- Paleta estilo Teams (morados/índigo), burbujas redondeadas, tipografía limpia.
```

Prueba la magia: `dotnet run --launch-profile http`, abre **dos ventanas** (una normal y una de incógnito),
entra como **Ana** y como **Luis**, y escribe en una → aparece **al instante** en la otra.

---

## ⏱️ PASO 3 — Presencia + "está escribiendo…"

```
Agrega dos detalles en tiempo real:
1) Presencia: junto a cada usuario de la lista, un punto verde si está en línea. Usa PresenceChanged.
2) "Escribiendo…": cuando escribo en la caja, llama SetTyping(true) (y SetTyping(false) al parar
   ~1.5s después de la última tecla). Escucha UserTyping(user, isTyping) y muestra
   "Fulano está escribiendo…" debajo del chat. Que desaparezca solo.
```

> El "escribiendo…" con *debounce* de 1.5s es el mismo patrón que usan WhatsApp y Teams.

---

## ⏱️ PASO 4 — Historial con EF Core + SQLite

```
Agrega persistencia con EF Core + SQLite:
- Paquete: Microsoft.EntityFrameworkCore.Sqlite.
- Entidad Message (Id, User, Text, SentAt). DbContext con DbSet<Message>.
- Guarda cada mensaje en SendMessage antes de reenviarlo.
- Al conectar un cliente, envíale los últimos 50 mensajes (ReceiveMessage) como historial.
- Crea la base con EnsureCreated() al arrancar (sin migraciones, para simplificar).
```

Ahora recarga la página: los mensajes **siguen ahí**. SQLite es un solo archivo `.db`, sin servidor ni configuración.

---

## ⏱️ PASO 5 — 🤯 El WOW: un equipo entero con guion

Este repo ya trae el simulador listo. Con tu app corriendo (mira el puerto que imprime `dotnet run`), en **otra terminal**:

```powershell
cd simulador
dotnet run -- --api http://127.0.0.1:5210
```

> Cambia `5210` por el puerto real de tu app.

Verás a **Ana, Luis, María y un Bot IA** ponerse en línea, teclear y conversar solos. Y lo mejor:
si **tú** escribes "hola" desde el navegador, **un bot te responde por tu nombre**. 🤯

**La moraleja del día:** el simulador **no toca tu app** — solo habla el **mismo contrato** (`SendMessage`, `SetTyping`).
Por eso un bot de consola, tu navegador y una futura app móvil son todos clientes iguales. **El contrato es el lenguaje común.**

> Los personajes y frases se editan en [`simulador/guion.json`](simulador/guion.json) en 30 segundos.

---

## ⏱️ PASO 6 — Publicar (opcional)

¿Quieres que otros entren por una URL? En [`docs/PUBLICAR-IIS.md`](docs/PUBLICAR-IIS.md) están las instrucciones
para desplegar en IIS (con lo crítico para SignalR: **WebSockets habilitado**).

---

## 🆘 Si te atoras

1. Lee el error en rojo y **pégaselo a Claude** — casi siempre lo corrige.
2. Windows: si algo "no conecta", usa **`127.0.0.1`** en vez de `localhost` (IPv6).
3. Compara con la carpeta [`solucion/`](solucion/) (pero solo como último recurso 😉).

---

## 🧠 Lo que te llevas

Construir con IA no es "pídele que haga la app". Es **darle el contexto correcto** (`CLAUDE.md`),
**un contrato claro** y **prompts precisos**, y dejar que itere. Ese mismo método sirve para cualquier proyecto.

---

*Hecho con 💜 en el curso Build with Claude Code · [PeopleWorks](https://peopleworks.github.io/build-with-claude-code/)*
