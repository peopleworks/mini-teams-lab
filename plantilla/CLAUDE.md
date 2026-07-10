# MiniTeams — Guía para Claude

## QUÉ construimos
Un chat en tiempo real estilo Microsoft Teams, en un solo proyecto:
- Backend: ASP.NET Core (.NET) + SignalR.
- Cliente web: HTML + CSS + JavaScript (con el cliente JS de SignalR), servido desde wwwroot.
- Un solo canal compartido "general" para empezar.

## Contrato del Hub (NO cambiar los nombres)
- Ruta: /hub/chat   ·   Identidad por query string: /hub/chat?user=<nombre>
- Cliente → Servidor:
  - SendMessage(string text)
  - SetTyping(bool isTyping)
- Servidor → Cliente:
  - ReceiveMessage(string user, string text, string sentAt)   // sentAt en ISO 8601
  - UserTyping(string user, bool isTyping)
  - PresenceChanged(string[] online)

## Reglas
- C# moderno: file-scoped namespaces, async/await, pasar CancellationToken.
- El servidor mantiene en memoria la lista de usuarios en línea (por nombre).
- Nada de autenticación por ahora: el nombre viene en ?user=.
- Mantener el código simple y legible.

## Comandos
- Correr:   dotnet run
- Correr (HTTP, más simple para probar):  dotnet run --launch-profile http

## Gotcha (Windows)
- Si algo "no conecta", usa 127.0.0.1 en vez de localhost (IPv6).

## Nova = otro cliente del contrato (Sesión 03)
- Nova (carpeta colega-ia/) es una IA que participa en el chat. NO modifica la app:
  se conecta al hub como un cliente más (igual que el simulador), hablando estos mismos 5 nombres.
- Su cerebro es una llamada a la API de Claude (HttpClient, sin SDK); la key va en la variable
  de entorno ANTHROPIC_API_KEY, nunca en el código.
- Regla que se mantiene: agregar una función = agregar una palabra al contrato una sola vez,
  y todos los clientes (navegador, simulador, Nova) la reciben.
