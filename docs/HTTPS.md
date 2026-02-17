# HTTPS Setup Guide for Vigil

This guide explains how to secure your Vigil with HTTPS for production use and remote access.

---

## Why HTTPS Matters

- **Encryption** — Protects sensitive data (API keys, credentials, authentication tokens) in transit
- **Authentication** — Proves your server's identity to clients, preventing man-in-the-middle attacks
- **Security Headers** — HTTPS enables security features like HSTS, CSP, and secure cookies
- **Remote Access** — Essential when accessing Vigil over the internet
- **Compliance** — Many organizations require HTTPS for any web application handling credentials or sensitive data

**Without HTTPS:** Credentials and API keys are sent in plain text over the network. Anyone on the network path can intercept and use them.

**With HTTPS:** All traffic is encrypted and authenticated. Only you and your server can read the data.

---

## Option 1: Caddy (Recommended)

Caddy is the simplest and most automated solution. It automatically provisions and renews SSL certificates from Let's Encrypt at zero cost.

### Install Caddy

**On Windows:**

Download from [caddyserver.com](https://caddyserver.com/download):
```powershell
# Or use Chocolatey
choco install caddy
```

**On Linux (Ubuntu/Debian):**

```bash
sudo apt-get install caddy
```

**On macOS:**

```bash
brew install caddy
```

### Configure Caddyfile

Create a `Caddyfile` in your project root or `/etc/caddy/Caddyfile`:

```caddyfile
atlas.yourdomain.com {
    reverse_proxy localhost:5263 {
        header_upstream X-Real-IP {http.request.remote}
        header_upstream X-Forwarded-For {http.request.remote}
        header_upstream X-Forwarded-Proto {http.request.scheme}
        header_upstream X-Forwarded-Host {http.request.host}
    }
}
```

**Replace `atlas.yourdomain.com` with your actual domain.**

### Run Caddy

```bash
caddy run
```

Or for systemd (Linux):

```bash
sudo systemctl enable caddy
sudo systemctl start caddy
```

**What Caddy Does:**
1. Automatically obtains an SSL certificate from Let's Encrypt for your domain
2. Proxies requests to `localhost:5263` (Atlas Web app)
3. Listens on ports 80 and 443
4. Automatically renews certificates before expiry (every 60 days)

### Firewall Configuration

Allow ports 80 and 443:

**Windows Firewall:**
```powershell
New-NetFirewallRule -DisplayName "HTTP" -Direction Inbound -LocalPort 80 -Protocol TCP -Action Allow
New-NetFirewallRule -DisplayName "HTTPS" -Direction Inbound -LocalPort 443 -Protocol TCP -Action Allow
```

**Linux (UFW):**
```bash
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```

### Update Application Settings

After Caddy is running, update `appsettings.json`:

```json
{
  "Auth": {
    "CookieSecure": true,
    "CookieSameSite": "Strict"
  },
  "Api": {
    "BaseUrl": "https://atlas.yourdomain.com"
  }
}
```

Restart the application:
```bash
cd src/Atlas.Web
dotnet run
```

---

## Option 2: nginx + Let's Encrypt (certbot)

Use nginx as a reverse proxy with automatic certificate management via certbot.

### Install nginx

**On Windows:**

Download from [nginx.org](http://nginx.org/en/download.html) or use WSL2.

**On Linux (Ubuntu/Debian):**

```bash
sudo apt-get update
sudo apt-get install nginx certbot python3-certbot-nginx
```

**On macOS:**

```bash
brew install nginx
brew install certbot
```

### Install SSL Certificate

Use certbot to obtain a free certificate from Let's Encrypt:

```bash
sudo certbot certonly --standalone -d atlas.yourdomain.com
```

This creates certificates in `/etc/letsencrypt/live/atlas.yourdomain.com/`.

**For automatic renewal:**

```bash
sudo systemctl enable certbot.timer
sudo systemctl start certbot.timer
```

### Configure nginx

Create or edit `/etc/nginx/sites-available/atlas`:

```nginx
# Redirect HTTP to HTTPS
server {
    listen 80;
    listen [::]:80;
    server_name atlas.yourdomain.com;
    return 301 https://$server_name$request_uri;
}

# HTTPS configuration
server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name atlas.yourdomain.com;

    # SSL certificate paths
    ssl_certificate /etc/letsencrypt/live/atlas.yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/atlas.yourdomain.com/privkey.pem;

    # SSL security settings
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 10m;

    # HSTS (optional but recommended)
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;

    # Gzip compression (optional)
    gzip on;
    gzip_types text/plain text/css application/json application/javascript;
    gzip_min_length 1000;

    # Proxy configuration
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
        proxy_set_header X-Forwarded-Host $host;

        # Timeout settings
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }
}
```

Enable the site:

```bash
sudo ln -s /etc/nginx/sites-available/atlas /etc/nginx/sites-enabled/
sudo nginx -t  # Test configuration
sudo systemctl restart nginx
```

### Update Application Settings

```json
{
  "Auth": {
    "CookieSecure": true,
    "CookieSameSite": "Strict"
  },
  "Api": {
    "BaseUrl": "https://atlas.yourdomain.com"
  }
}
```

---

## Option 3: Kestrel HTTPS (Self-Signed or Custom Certificate)

Use Kestrel (ASP.NET Core's built-in web server) with HTTPS directly. Suitable for internal/development use or with custom certificates.

### Generate Self-Signed Certificate

For testing only (browsers will show certificate warnings):

```bash
dotnet dev-certs https --clean
dotnet dev-certs https -ep %APPDATA%\ASP.NET\Https\atlas-cert.pfx -p YourPassword123
dotnet dev-certs https --trust
```

Or using OpenSSL:

```bash
openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.pem -days 365 -nodes -subj "/CN=atlas.yourdomain.com"
# Combine into PKCS12 format for .NET
openssl pkcs12 -export -out certificate.pfx -inkey key.pem -in cert.pem
```

### Configure appsettings.json

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5262"
      },
      "Https": {
        "Url": "https://localhost:5263",
        "Certificate": {
          "Path": "certificate.pfx",
          "Password": "YourPassword123"
        }
      }
    }
  },
  "Auth": {
    "CookieSecure": true,
    "CookieSameSite": "Strict"
  },
  "Api": {
    "BaseUrl": "https://localhost:5263"
  }
}
```

### Run with HTTPS

```bash
cd src/Atlas.Web
dotnet run
```

**Note:** Self-signed certificates will cause browser warnings. Use only for development/testing or internal access.

### Production Self-Signed Setup

For production with a self-signed cert, you must:
1. Import the certificate into client machines' certificate stores
2. Use a reverse proxy (Caddy/nginx) instead of Kestrel HTTPS directly
3. Consider using Let's Encrypt instead (it's free and won't trigger warnings)

---

## Firewall Considerations

Ensure your firewall allows traffic on port 443 (HTTPS) and 80 (HTTP for Let's Encrypt validation).

### Windows Defender Firewall

```powershell
# Allow HTTPS
New-NetFirewallRule -DisplayName "HTTPS" -Direction Inbound -LocalPort 443 -Protocol TCP -Action Allow

# Allow HTTP (for certbot validation)
New-NetFirewallRule -DisplayName "HTTP" -Direction Inbound -LocalPort 80 -Protocol TCP -Action Allow
```

### Cloud Firewalls (AWS, Azure, GCP)

Ensure security groups / firewall rules allow:
- **Inbound:** Port 443 (TCP)
- **Inbound:** Port 80 (TCP) — for certificate validation
- **Outbound:** Port 443 (TCP) — for updates and API calls

### Network Segmentation

For maximum security:
1. Block port 5263 at the firewall (application runs internally only)
2. Only expose port 443 via reverse proxy (Caddy/nginx)
3. Restrict port 5263 access to localhost/127.0.0.1 if possible

---

## Testing HTTPS

### Test Certificate Installation

Using curl:

```bash
# Caddy / nginx (Let's Encrypt)
curl -v https://atlas.yourdomain.com

# Self-signed (will show certificate warning)
curl -k https://localhost:5263
# or skip verification:
curl --insecure https://localhost:5263
```

### Test in Browser

1. Open `https://atlas.yourdomain.com` (or your domain)
2. Check the padlock icon (🔒 for secure, ⚠️ for warnings)
3. Click the padlock → "Certificate is valid" should appear
4. Verify the domain name matches your URL

### Test WebSocket Connections

Some features (real-time monitoring) use WebSockets over HTTPS (WSS):

```bash
# Use websocat or similar tools
websocat wss://atlas.yourdomain.com/api/monitoring
```

### Test OpenClaw Plugin Connection

After HTTPS is running, update `openclaw.json`:

```json
{
  "plugins": {
    "atlas": {
      "url": "https://atlas.yourdomain.com"
    }
  }
}
```

Restart OpenClaw and check logs for connection success.

### Using SSL Labs (External Test)

Visit [ssllabs.com/ssltest](https://www.ssllabs.com/ssltest/) and enter your domain.

**Expected Grade:** A or A+ (for production setups)

---

## Certificate Renewal

### With Caddy

Caddy handles renewal automatically. No action needed.

Check renewal status:
```bash
caddy reload
```

### With nginx + certbot

Check renewal is scheduled:

```bash
sudo certbot renew --dry-run
```

Manual renewal:
```bash
sudo certbot renew
sudo systemctl reload nginx
```

### With Kestrel (Self-Signed)

Manually renew before expiry. Set a calendar reminder.

---

## Troubleshooting HTTPS

### "ERR_CERT_AUTHORITY_INVALID"

**Cause:** Certificate is self-signed or not trusted.

**Solution:**
- Use Let's Encrypt instead (Caddy or certbot)
- Or import self-signed cert into client's certificate store
- Or use `--insecure` flag in curl (development only)

### "The certificate is not yet valid" or "Certificate expired"

**Cause:** System clock is incorrect or certificate expired.

**Solution:**
- Sync system clock: `ntpdate -s time.nist.gov` (Linux) or settings (Windows)
- Renew certificate: `sudo certbot renew --force-renewal`

### Connection Refused on Port 443

**Cause:** Firewall is blocking port 443 or reverse proxy not running.

**Solution:**
- Check firewall rules (see Firewall Considerations section)
- Verify Caddy/nginx is running: `caddy -v` or `sudo systemctl status nginx`
- Check port is listening: `netstat -an | grep 443`

### "Mixed Content" or "ERR_INSECURE_RESPONSE"

**Cause:** Page is HTTPS but some resources are HTTP.

**Solution:**
- Ensure all resource URLs in the application use HTTPS
- Update `Api.BaseUrl` in `appsettings.json` to HTTPS URL
- Ensure reverse proxy forwards proto headers correctly

### Plugin Cannot Connect

**Cause:** Plugin still connecting to HTTP after HTTPS setup.

**Solution:**
- Update `openclaw.json` plugin URL to `https://...`
- Verify API key matches `Auth.ApiKey` in `appsettings.json`
- Check browser console for CORS or certificate errors
- Verify reverse proxy is running and healthy

---

## Best Practices

1. **Always Use HTTPS in Production** — No exceptions for credential management applications
2. **Use Let's Encrypt (Free)** — Caddy or certbot automate everything
3. **Enable HSTS** — Tells browsers to always use HTTPS (included in nginx config above)
4. **Restrict Admin Access** — Use VPN or IP whitelisting for dashboard access
5. **Monitor Certificate Expiry** — Set calendar reminders or enable alerts
6. **Update Regularly** — Keep Caddy, nginx, and .NET SDK patched
7. **Test After Setup** — Use SSL Labs or curl to verify configuration

---

## Further Reading

- [Let's Encrypt Documentation](https://letsencrypt.org/docs/)
- [Caddy Documentation](https://caddyserver.com/docs/)
- [nginx Documentation](https://nginx.org/en/docs/)
- [OWASP TLS Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Transport_Layer_Protection_Cheat_Sheet.html)
- [Mozilla SSL Configuration Generator](https://ssl-config.mozilla.org/)

---

**Last Updated:** February 2026
