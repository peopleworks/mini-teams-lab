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
| | **✦ Sesión 03 — dale un cerebro y un remate visual** | |
| 7 | Nova entra y escucha | Una IA aparece 🟢 en tu canal |
| 8 | El cerebro: primera llamada a Claude | Nova piensa y responde de verdad |
| 9 | Herramientas: Nova busca en el historial | IA **+ tus datos reales** |
| 10 | 🎆 Reacciones que llueven **+ videollamada 1:1** | La sala aplaude; y vídeo dentro de la app |

> 💡 ¿Prefieres verlo como **línea de tiempo con todos los prompts y botón de copiar**?
> Está la [**receta visual**](https://peopleworks.github.io/build-with-claude-code/receta.html) — los 14 prompts, en orden.

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

# ✦ Sesión 03 — Del chat al colega con IA

En la Sesión 02 dijimos: *"un bot de consola, tu navegador y una app móvil son todos clientes iguales — el contrato es el lenguaje común."* Ahora lo cobramos: le damos al equipo **una compañera con IA de verdad, Nova**, que **piensa** cada respuesta con Claude. Y lo mejor: **no tocamos el Mini-Teams** — Nova es **otro cliente más**, igual que el simulador, solo que su cerebro es una llamada a la API de Claude.

> 🔑 **Necesitas una API key de Claude** (de la plataforma, `console.anthropic.com`) en la variable de entorno `ANTHROPIC_API_KEY`.
> Es **distinta** de tu suscripción de Claude Code y funciona con **créditos**. Con el modelo `claude-haiku-4-5` cada respuesta cuesta céntimos.
> El código de Nova ya está en la carpeta [`colega-ia/`](colega-ia/) — ábrela para verlo o construye el tuyo con estos prompts.

## ⏱️ PASO 7 — Nova entra y escucha

Nova es un proyecto de consola aparte que se conecta al hub **como un cliente más**. Primero, que entre y oiga (todavía sin cerebro):

```
Crea un proyecto de consola .NET 10 llamado ColegaIA (carpeta colega-ia/) que se conecte
al hub del Mini-Teams como un cliente más, usando Microsoft.AspNetCore.SignalR.Client 10.0.0.
NO referencia la app: solo habla el contrato por su nombre (como el simulador).
- Identidad: se conecta a /hub/chat?user=Nova.
- Acepta el argumento --api <url> (por defecto http://127.0.0.1:5210).
- Escucha ReceiveMessage(user, text, sentAt) y, por ahora, imprime en consola "user: text".
- Ignora sus propios mensajes (cuando user == "Nova").
- Guarda en memoria las últimas 30 líneas del canal (una cola thread-safe) — las usaremos como contexto luego.
Al arrancar, imprime "Nova en línea" y quédate escuchando hasta Ctrl+C.
```

Con tu app corriendo, en otra terminal: `cd colega-ia && dotnet run -- --api http://127.0.0.1:5210`.
Escribe algo desde el navegador → aparece en la consola de Nova. **Ya está adentro, porque habla el contrato.**

## ⏱️ PASO 8 — 🧠 El cerebro: primera llamada a Claude

Cuando alguien nombre a Nova, en vez de una frase fija le preguntamos a **Claude** — con la API cruda, sin SDK, para ver la forma real (un `system`, unos `messages`, un JSON):

```
Agrega el "cerebro" de Nova con la API de Mensajes de Claude, con HttpClient (SIN SDK):
- Endpoint: POST https://api.anthropic.com/v1/messages
- Headers: x-api-key (leído de la variable de entorno ANTHROPIC_API_KEY, o de --key),
  y anthropic-version: 2023-06-01.
- Modelo: claude-haiku-4-5-20251001 (o --model / variable CLAUDE_MODEL). max_tokens 400.
- Dispara SOLO cuando el mensaje mencione "Nova" (o "@nova"), y nunca a sus propios mensajes.
- System prompt: "Eres Nova, una compañera de equipo con IA en un chat estilo Teams de PeopleWorks.
  Respondes en español, cálida y BREVE (1-3 frases, como un chat)."
- En el mensaje de usuario, incluye las últimas líneas del canal (contexto) + lo que escribió la persona.
- Toma el texto de la respuesta de Claude y publícalo con SendMessage.
- Si la API falla, que Nova diga algo amable en el canal en vez de caerse.
```

Y dale **modales humanos** para que no se note tan máquina:

```
Dale a Nova modales humanos:
1) Antes de pensar, llama SetTyping(true); cuando publique la respuesta, SetTyping(false).
   Así el "Nova está escribiendo…" del Mini-Teams se enciende de verdad mientras Claude piensa.
2) Anti-spam: que no responda más de una vez cada ~2 segundos, y que NO procese dos respuestas
   a la vez (si ya está pensando, ignora el nuevo disparo).
```

Pon la key (`$env:ANTHROPIC_API_KEY = "sk-ant-..."`), reinicia Nova, y desde el navegador escribe **"Nova, preséntate al equipo en una frase"**. Responde de verdad. 🎉

> **La clave nunca va en el código** — va en una variable de entorno. Semilla de buena práctica.

## ⏱️ PASO 9 — 🛠️ Herramientas: Nova consulta el historial real

Hasta ahora Nova solo sabe lo que oyó desde que entró. Le damos **manos**: que busque en la misma base SQLite de la app. Esto es **IA + tus datos**.

```
Dale a Nova una herramienta con "tool use" de Claude:
- Declara en el request un tool "buscar_historial" con input { termino: string, limite?: int }.
- Cuando la respuesta de Claude tenga stop_reason == "tool_use", ejecuta la herramienta y
  devuélvele el resultado como un bloque tool_result, y vuelve a llamar a la API (lazo) hasta
  que conteste con texto. Pon un tope de 4 vueltas por seguridad.
- La herramienta lee la MISMA base SQLite de la app en solo lectura (Microsoft.Data.Sqlite,
  Data Source=<db>;Mode=ReadOnly): SELECT User, Text FROM Messages WHERE Text LIKE '%termino%'
  ORDER BY Id DESC LIMIT <limite (máx 20)>. Devuelve las líneas encontradas.
- Acepta --db <ruta> (por defecto ..\app-referencia\miniteams.db). Si la base no existe,
  que la herramienta degrade con gracia (que Nova diga que no puede ver el historial ahora).
```

Pregunta **"Nova, ¿qué se dijo sobre el deploy?"** → verás que **usa la herramienta**, lee SQLite y responde citando el mensaje real. No se lo inventó: **lo buscó en tus datos.**

> **El lazo** `pregunta → Claude pide una herramienta → tu código la ejecuta → Claude ve el resultado → responde`
> es, a mano, exactamente lo que hace un servidor **MCP** por dentro.

## ⏱️ PASO 10 — 🎆 El remate visual: reacciones + videollamada

Dos funciones puro Mini-Teams (no usan la API). Cada una demuestra lo mismo: **una función nueva es una palabra nueva en el contrato — la agregas una vez y TODOS los clientes la reciben.**

**👏 Reacciones que llueven** — servidor y cliente:

```
Agrega al ChatHub un método SendReaction(string emoji) que reparta a TODOS (Clients.All)
un evento ReceiveReaction(user, emoji). NO lo guardes en la base: es efímero. Limita el emoji
a pocos caracteres por seguridad.
```

```
Añade una barra de botones de emoji (👏 ❤️ 🎉 🔥 😂). Al tocar uno, invoca SendReaction(emoji).
Escucha ReceiveReaction y haz "flotar" ese emoji subiendo por la pantalla (posición y deriva al
azar; se borra al terminar la animación). Añade un "aplausómetro" en la cabecera que suba con cada
reacción y baje solo; cuando llegue al tope, dispara una lluvia de confeti.
```

**📹 Videollamada 1:1** — el mismo hub ahora te conecta una llamada (el vídeo va **directo entre navegadores**; el hub solo los presenta):

```
Agrega señalización de videollamada 1:1 al hub. Registra un IUserIdProvider que use el ?user=
como identidad (para poder dirigir eventos con Clients.User(nombre)). Métodos del cliente al
servidor: CallUser/AcceptCall/DeclineCall/HangUp(targetUser) y SendSignal(targetUser, signal).
Reenvían al destinatario: IncomingCall/CallAccepted/CallDeclined/CallEnded(fromUser) y
ReceiveSignal(fromUser, signal). El hub NO transporta vídeo: solo reenvía "sobres".
```

```
Botón 📞 junto a cada usuario en línea. Al llamar, usa WebRTC (RTCPeerConnection con el STUN
de Google) para vídeo+audio P2P; intercambia la oferta/respuesta SDP y los candidatos ICE por
SendSignal/ReceiveSignal del hub. Muestra un panel con el vídeo remoto grande, tu vídeo en
pequeño (PiP) y botones de micro, cámara y colgar, más un aviso de llamada entrante con
Aceptar/Rechazar.
```

> El vídeo exige **contexto seguro**: funciona en `127.0.0.1` y en `https://`. Entre redes distintas, producción puede requerir un servidor **TURN** (relay); para tu demo local no hace falta.

La carpeta [`solucion/`](solucion/) ya trae **todo esto integrado** (reacciones + videollamada), por si te quieres comparar.

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
