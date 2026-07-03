# 🚀 Publicar Mini-Teams en IIS → miniteams.peopleworksservices.com

> App: ASP.NET Core (.NET 10) + SignalR + SQLite. Lo crítico para SignalR es **WebSockets habilitado** en IIS.

---

## 1) En tu máquina — generar el paquete (YA HECHO)

```
cd C:\Proyecto\AI\webinar\mini-teams-lab\app-referencia
dotnet publish -c Release -o publish
```

> El resultado está en `app-referencia\publish\`. Ese es el que copias al servidor.
> (Si publicas el proyecto que construiste EN VIVO, es el mismo comando dentro de su carpeta.)

---

## 2) En el servidor IIS — requisitos de una sola vez

### a) ASP.NET Core Hosting Bundle (.NET 10)
Instálalo en el servidor y reinicia IIS:
```
iisreset
```
> El Hosting Bundle trae el módulo `AspNetCoreModuleV2` que arranca la app.

### b) ⚠️ WebSocket Protocol (CLAVE para SignalR)
Sin esto, el chat carga pero **no actualiza en vivo**. Actívalo:

- **Windows Server** (PowerShell como admin):
  ```
  Install-WindowsFeature Web-WebSockets
  ```
- **Windows normal:** Panel → Activar características de Windows → IIS →
  Servicios World Wide Web → Características de desarrollo de aplicaciones → **WebSocket Protocol**.

---

## 3) Crear el sitio en IIS

1. **IIS Manager → Sites → Add Website**
   - **Site name:** `miniteams`
   - **Physical path:** `C:\inetpub\miniteams`  (crea la carpeta)
   - **Binding:** tipo `https` (recomendado) o `http`
   - **Host name:** `miniteams.peopleworksservices.com`
2. **Application Pool** del sitio → **Basic Settings** →
   **.NET CLR version = "No Managed Code"** (Sin código administrado).
   > ASP.NET Core corre fuera del CLR de IIS; el pool solo hace de proxy.

---

## 4) Copiar la app

Copia **todo el contenido** de `app-referencia\publish\` dentro de `C:\inetpub\miniteams\`.
(Deben quedar ahí `MiniTeams.dll`, `web.config`, la carpeta `wwwroot`, las DLL de SQLite, etc.)

---

## 5) ⚠️ Permiso de escritura para SQLite

La app crea `miniteams.db` en la carpeta del sitio. El pool necesita poder escribir:

1. Carpeta `C:\inetpub\miniteams` → clic derecho → **Propiedades → Seguridad → Editar → Agregar**
2. Agrega el usuario: **`IIS AppPool\miniteams`** (el nombre de tu App Pool)
3. Dale permiso **Modificar**.
   > Sin esto verás error 500.30 al arrancar porque no puede crear el `.db`.

---

## 6) DNS y HTTPS

- **DNS:** apunta `miniteams.peopleworksservices.com` (registro A / CNAME) a la IP del servidor.
- **HTTPS (recomendado):** enlaza un certificado en el binding 443.
  SignalR sobre `wss://` (WebSocket seguro) es lo ideal. Si ya tienes wildcard
  `*.peopleworksservices.com`, úsalo; si no, win-acme (Let's Encrypt) en 2 min.

---

## 7) Verificar

1. Abre `https://miniteams.peopleworksservices.com` → debe cargar el chat.
2. Abre **dos ventanas** (normal + incógnito), entra como Ana y Luis, escribe en una.
   - **Aparece al instante en la otra** → ✅ WebSockets OK.
   - **Carga pero NO actualiza en vivo** → ❌ falta el paso 2b (WebSocket Protocol).
3. Recarga → los mensajes siguen ahí → ✅ SQLite escribiendo bien (paso 5).

---

## 🆘 Diagnóstico rápido

| Síntoma | Causa probable | Arreglo |
|---|---|---|
| **HTTP 500.19** | falta Hosting Bundle | instala Hosting Bundle + `iisreset` |
| **HTTP 500.30** (no arranca) | no puede crear `miniteams.db` | permiso Modificar al App Pool (paso 5) |
| Carga pero no chatea en vivo | WebSockets apagado | paso 2b |
| **404 en `/hub/chat`** | ruta o app mal copiada | revisa que `web.config` esté en la raíz del sitio |
| Página en blanco | app pool con CLR administrado | ponlo en "No Managed Code" (paso 3.2) |

> Logs de arranque: en `web.config` pon `stdoutLogEnabled="true"` temporalmente y crea la carpeta `logs\`; revisa `logs\stdout*.log`.

---

## 💡 Opcional — robustez para el aula (sin depender de internet)

El cliente carga SignalR desde el CDN de unpkg. Si un alumno está **sin internet**, esa línea falla.
Para blindarlo, descarga `signalr.min.js` una vez, guárdalo en `wwwroot\lib\signalr\` y cambia el
`<script>` de `index.html` para que apunte a `/lib/signalr/signalr.min.js` en vez del CDN.
(No es obligatorio si todos tendrán internet.)
