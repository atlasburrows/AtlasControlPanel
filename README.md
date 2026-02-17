# Vigil

**Self-hosted control panel for OpenClaw AI agents. Activity logging, task management, credential approval, and real-time monitoring — enforced at infrastructure level.**

_Vigil is developed by [Zenido Labs](https://zenidolabs.com)._

---

## Features

- **Activity Logging** — Automatic logging of all agent actions (Development, BugFix, System, Communication, Research, Security)
- **Task Board** — Create, track, and manage agent tasks with status workflows (Backlog, InProgress, Review, Done)
- **Credential Vault** — Centralized credential management with approval workflow and time-limited access
- **Real-Time Monitoring** — Live dashboard of agent activity, system health, and performance metrics
- **Cost Tracking** — Monitor API usage and associated costs across all integrated services
- **Chat Integration** — Direct integration with Telegram, Discord, and other chat platforms for alerts and reporting
- **Mobile-Responsive Design** — Full-featured dashboard accessible from any device

---

## Prerequisites

- **.NET 10 SDK** — [Download](https://dotnet.microsoft.com/download)
- **SQL Server 2019+** or **SQLite** (coming soon)
  - For SQL Server: Express Edition or higher
  - Connection string: `Server=localhost;Database=Vigil;Integrated Security=true;`
- **OpenClaw** — [GitHub](https://github.com/OpenClaw/OpenClaw)
- **Node.js 18+** (optional, for plugin development)

---

## Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/your-org/AtlasControlPanel.git
cd AtlasControlPanel
```

### 2. Initialize the Database

Run the SQL setup script against your SQL Server instance:

```bash
sqlcmd -S localhost -E -i setup.sql
```

Or if using a named instance:

```bash
sqlcmd -S .\SQLEXPRESS -E -i setup.sql
```

### 3. Configure Application Settings

Edit `src/Atlas.Web/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=Vigil;Integrated Security=true;"
  },
  "Auth": {
    "ApiKey": "your-secure-api-key-here",
    "CookieName: ".Vigil.Auth",
    "CookieSecure": false,
    "CookieSameSite": "Lax"
  },
  "Api": {
    "BaseUrl": "http://localhost:5263",
    "Timeout": 30
  },
  "OpenClaw": {
    "PluginPath": "../../../extension"
  }
}
```

### 4. Run the Application

```bash
cd src/Atlas.Web
dotnet run
```

The application will start on `http://localhost:5263` (or the port configured in launchSettings.json).

### 5. Access the Dashboard

Open your browser and navigate to:

```
http://localhost:5263
```

### 6. Install the OpenClaw Plugin

Two options:

**Option A: Direct Copy**
```bash
# Copy the extension folder to your OpenClaw extensions directory
cp -r extension ~/.openclaw/extensions/atlas
```

**Option B: NPM Install (if published)**
```bash
npm install @openclaw/atlas-plugin
```

### 7. Configure OpenClaw Integration

Add the plugin configuration to your `openclaw.json`:

```json
{
  "plugins": {
    "atlas": {
      "enabled": true,
      "url": "http://localhost:5263",
      "apiKey": "your-secure-api-key-here",
      "autoLog": true,
      "autoReportStatus": true
    }
  }
}
```

---

## Configuration

### appsettings.json Sections

#### ConnectionStrings

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=Vigil;Integrated Security=true;"
}
```

- **Server**: SQL Server instance (default: `localhost`)
- **Database**: Database name (default: `AtlasControlPanel`)
- **Integrated Security**: Use Windows authentication (set to `false` to use SQL authentication with UserId/Password)

#### Auth

```json
"Auth": {
  "ApiKey": "your-api-key-minimum-32-chars",
  "CookieName: ".Vigil.Auth",
  "CookieSecure": false,
  "CookieSameSite": "Lax"
}
```

- **ApiKey**: Secret key for API authentication (used by OpenClaw plugin and external callers)
- **CookieSecure**: Set to `true` when using HTTPS (see HTTPS section)
- **CookieSameSite**: Cookie SameSite policy (`Strict`, `Lax`, or `None`)

#### Api

```json
"Api": {
  "BaseUrl": "http://localhost:5263",
  "Timeout": 30
}
```

- **BaseUrl**: Public URL of the application (used for links in notifications)
- **Timeout**: API timeout in seconds

#### OpenClaw

```json
"OpenClaw": {
  "PluginPath": "../../../extension"
}
```

- **PluginPath**: Relative path to the OpenClaw plugin extension

---

## Plugin Configuration

### openclaw.json Integration

Add Vigil to your OpenClaw configuration:

```json
{
  "plugins": {
    "atlas": {
      "enabled": true,
      "url": "http://localhost:5263",
      "apiKey": "your-secure-api-key-from-appsettings",
      "autoLog": true,
      "autoReportStatus": true,
      "categories": {
        "developmentColor": "#4CAF50",
        "bugFixColor": "#FF5722",
        "systemColor": "#2196F3",
        "communicationColor": "#FF9800",
        "researchColor": "#9C27B0",
        "securityColor": "#F44336"
      }
    }
  }
}
```

**Configuration Options:**

- **url**: Base URL of Vigil (must match `Api.BaseUrl` in appsettings.json)
- **apiKey**: Secret key for plugin authentication (must match `Auth.ApiKey`)
- **autoLog**: Automatically log agent activities
- **autoReportStatus**: Automatically report agent status changes
- **categories**: Color mapping for activity categories (optional)

---

## HTTPS / Reverse Proxy

For production deployments or remote access, always use HTTPS. Vigil does not enforce HTTPS at the application level; instead, use a reverse proxy.

### Option 1: Caddy (Recommended)

Caddy automatically manages HTTPS certificates via Let's Encrypt.

**Caddyfile example** (for `atlas.yourdomain.com`):

```caddyfile
atlas.yourdomain.com {
  reverse_proxy localhost:5263
}
```

Run Caddy:
```bash
caddy run
```

Caddy will automatically provision an SSL certificate and serve HTTPS on port 443.

### Option 2: nginx with Let's Encrypt

**nginx configuration** (`/etc/nginx/sites-available/atlas`):

```nginx
server {
    listen 80;
    server_name atlas.yourdomain.com;
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name atlas.yourdomain.com;

    ssl_certificate /etc/letsencrypt/live/atlas.yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/atlas.yourdomain.com/privkey.pem;

    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;

    location / {
        proxy_pass http://localhost:5263;
        proxy_http_version 1.1;
        
        # WebSocket support
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        
        # Standard proxy headers
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Obtain a certificate with certbot:
```bash
certbot certonly --standalone -d atlas.yourdomain.com
```

### Update Auth Cookie Settings for HTTPS

When enabling HTTPS, update `appsettings.json`:

```json
"Auth": {
  "CookieSecure": true,
  "CookieSameSite": "Strict"
}
```

- **CookieSecure**: Forces cookies to be sent only over HTTPS
- **CookieSameSite**: Strict prevents cookies from being sent in cross-site requests

---

## Security

### API Key Management

- Generate a strong API key (minimum 32 characters, mix of upper/lowercase, numbers, symbols)
- Store securely in environment variables or secrets management system
- Rotate keys periodically (at least annually)
- Revoke old keys immediately when compromised

### Cookie Authentication

- Vigil uses secure HTTP-only cookies for session management
- Cookies are set with SameSite protection to prevent CSRF attacks
- Enable `CookieSecure: true` when using HTTPS (see HTTPS section)

### Credential Vault

- All stored credentials are encrypted at rest using AES-256
- Credential access requires API key authentication
- Approval workflow prevents unauthorized access to sensitive credentials
- Time-limited access ensures credentials expire automatically

### Network Security

- Restrict access to Vigil using firewall rules or authentication
- Use HTTPS for all external access
- Consider IP whitelisting for API endpoints
- Keep OpenClaw and .NET SDK updated for security patches

---

## Architecture Overview

Vigil consists of:

1. **Web Application** (`src/Atlas.Web/`) — ASP.NET Core 10 web UI and REST API
2. **Database** — SQL Server schema with stored procedures for activity logging, task management, and credential tracking
3. **OpenClaw Plugin** (`extension/`) — JavaScript/TypeScript plugin that integrates with OpenClaw agents
4. **Documentation** (`docs/`) — Setup and configuration guides

**Data Flow:**
- OpenClaw agents → Plugin → REST API → SQL Server
- Web Dashboard ← SQL Server
- Real-time updates via WebSocket or polling

---

## Project Structure

```
AtlasControlPanel/
├── src/
│   └── Atlas.Web/
│       ├── Controllers/          # REST API endpoints
│       ├── Services/             # Business logic
│       ├── Pages/                # Razor pages (UI)
│       ├── appsettings.json      # Configuration
│       ├── Program.cs            # Application entry point
│       └── Atlas.Web.csproj      # Project file
├── extension/                    # OpenClaw plugin
│   ├── src/
│   │   └── index.ts              # Plugin entry point
│   ├── package.json
│   └── tsconfig.json
├── docs/                         # Documentation
│   └── HTTPS.md                  # HTTPS setup guide
├── setup.sql                     # Database schema
├── README.md                     # This file
└── LICENSE                       # MIT license
```

---

## Troubleshooting

### Database Connection Error

**Error:** "Cannot open database 'AtlasControlPanel'..."

**Solution:** Ensure SQL Server is running and the connection string in `appsettings.json` is correct. Run `setup.sql` to create the database.

### Plugin Not Connecting

**Error:** Plugin shows "Connection failed" in dashboard

**Solution:** 
- Verify `url` and `apiKey` in `openclaw.json` match your Atlas installation
- Check firewall allows connections to port 5263
- Review application logs: `dotnet run` will show errors in console

### HTTPS Certificate Issues

**Error:** "ERR_CERT_INVALID" or "NET::ERR_CERT_AUTHORITY_INVALID"

**Solution:** See [HTTPS.md](docs/HTTPS.md) for certificate setup with Let's Encrypt or self-signed certs.

---

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## License

Vigil is licensed under the **MIT License**. See [LICENSE](LICENSE) file for details.

---

## Support & Community

- **Documentation:** [docs/](docs/)
- **OpenClaw Docs:** [OpenClaw GitHub](https://github.com/OpenClaw/OpenClaw)
- **Discord Community:** [Join our Discord server](https://discord.gg/openclaw)
- **Issues:** [GitHub Issues](https://github.com/your-org/AtlasControlPanel/issues)

---

**Last Updated:** February 2026
