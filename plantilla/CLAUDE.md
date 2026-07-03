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
