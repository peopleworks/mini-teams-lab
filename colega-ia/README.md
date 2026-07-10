# 🧠 Nova — el colega con IA (Sesión 03)

Nova es una **compañera de equipo con IA** que vive en tu Mini-Teams. **No modifica la app**: se conecta al hub
**como un cliente más** (igual que el simulador), solo que su cerebro es una llamada a la **API de Claude**.
Es la lección del curso hecha realidad: *el contrato es el lenguaje común.*

## Qué hace

- Se conecta a `/hub/chat?user=Nova` y **escucha** el canal.
- Cuando alguien la **menciona** ("Nova, ..."), le pregunta a **Claude** (API de Mensajes, con `HttpClient`, **sin SDK**) y responde en el chat.
- Muestra **"Nova está escribiendo…"** mientras piensa (usa `SetTyping` del contrato) y tiene anti-spam.
- Puede **buscar en el historial real** con *tool use*: lee la misma base SQLite de la app en solo lectura (`buscar_historial`).

## Requisitos

- **SDK de .NET 10**
- El **Mini-Teams corriendo** (la app de `../solucion/`), por ejemplo en `http://127.0.0.1:5210`.
- Una **API key de Claude de plataforma** (`console.anthropic.com`) — **distinta** de tu suscripción de Claude Code, funciona con **créditos**.

> Con el modelo `claude-haiku-4-5` cada respuesta cuesta céntimos (rápido, ideal para probar).
> Para una respuesta "de lujo" puedes subir a `claude-opus-4-8`.

## Cómo correrla

```powershell
# 1) Pon tu API key en ESTA terminal (es por-terminal)
$env:ANTHROPIC_API_KEY = "sk-ant-..."

# 2) Corre Nova apuntando a tu app
cd colega-ia
dotnet run -- --api http://127.0.0.1:5210
```

Luego, desde el navegador del Mini-Teams, escribe: **"Nova, preséntate al equipo en una frase"**.

### Argumentos

| Argumento | Por defecto | Para qué |
|---|---|---|
| `--api <url>` | `http://127.0.0.1:5210` | Dónde está el hub del Mini-Teams |
| `--model <id>` | `claude-haiku-4-5-20251001` | Modelo de Claude (o variable `CLAUDE_MODEL`) |
| `--key <key>` | *(usa `ANTHROPIC_API_KEY`)* | Pasar la key por argumento en vez de variable de entorno |
| `--db <ruta>` | `..\app-referencia\miniteams.db` | La base SQLite que consulta la herramienta `buscar_historial` |

## Cómo está hecha

| Archivo | Qué es |
|---|---|
| `Program.cs` | El cliente SignalR: escucha el canal, decide cuándo responder, dispara "escribiendo…", publica la respuesta y expone la herramienta `buscar_historial`. |
| `ClaudeClient.cs` | La llamada cruda a `https://api.anthropic.com/v1/messages` con el **lazo de tool use** (si Claude pide una herramienta, la ejecuta y vuelve a llamar). |
| `ColegaIA.csproj` | Proyecto de consola .NET 10 (referencia el cliente de SignalR y `Microsoft.Data.Sqlite`). |

> ¿Quieres construirla tú mismo desde cero? Los prompts están en los **Pasos 7–9** del [README principal](../README.md).
> Y el **contrato es sagrado**: Nova encaja porque respeta los 5 nombres.

---

*Build with Claude Code · PeopleWorks · Nova es otro cliente del contrato.*
