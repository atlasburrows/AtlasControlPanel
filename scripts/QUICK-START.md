# OpenClaw Scripts - Quick Start Guide

## Choose Your Platform

### 🪟 Windows (PowerShell)

**First time setup:**
```powershell
cd "C:\Users\mikal\source\repos\AtlasControlPanel\scripts"
.\install-openclaw.ps1
```

**Just patch an existing installation:**
```powershell
.\patch-openclaw.ps1
```

**See help:**
```powershell
Get-Help .\install-openclaw.ps1 -Detailed
Get-Help .\patch-openclaw.ps1 -Detailed
```

---

### 🐧 Linux/macOS (Bash)

**First time setup:**
```bash
cd ~/path/to/AtlasControlPanel/scripts
chmod +x *.sh
./install-openclaw.sh
```

**Just patch an existing installation:**
```bash
./patch-openclaw.sh
```

**See help:**
```bash
./install-openclaw.sh --help
./patch-openclaw.sh --help
```

---

## What Each Script Does

| Script | Purpose | Platform |
|--------|---------|----------|
| `install-openclaw.ps1` | Install OpenClaw + apply patch | Windows |
| `patch-openclaw.ps1` | Patch existing OpenClaw | Windows |
| `install-openclaw.sh` | Install OpenClaw + apply patch | Linux/macOS |
| `patch-openclaw.sh` | Patch existing OpenClaw | Linux/macOS |

---

## Expected Output

```
✓ Node.js found: v22.18.0
✓ Version requirement met (18+)
ℹ npm found: 10.5.0
ℹ Found OpenClaw at: /path/to/openclaw

Processing: gateway-server.cjs ... ✓ patched
Processing: gateway-server.mjs ... ✓ patched
Processing: agent-session.cjs ... ✓ patched
Processing: agent-session.mjs ... ✓ patched

✓ Patch process completed successfully!
✓ Files patched: 4
✓ Total processed: 4/4
```

---

## Troubleshooting

### "Node.js not installed"
→ Install from https://nodejs.org/ (require 18+)

### "npm not found"
→ Comes with Node.js. Reinstall if needed.

### "OpenClaw not found"
→ Run `.\install-openclaw.ps1` or `./install-openclaw.sh` first

### PowerShell execution error
→ Run as Administrator or use: `powershell -ExecutionPolicy Bypass -File .\script.ps1`

### Bash execution error
→ Make executable: `chmod +x *.sh`

---

## Verify It Worked

```bash
# Check OpenClaw version
openclaw --version

# Check gateway status
openclaw gateway status

# View installed location
npm list -g openclaw
```

---

## Need More Help?

- Full documentation: `README-OPENCLAW-SCRIPTS.md`
- Deployment details: `DEPLOYMENT.md`
- OpenClaw docs: https://openclaw.dev

---

**Safe to run multiple times** ✓ Idempotent design  
**Auto-backup** ✓ Creates `.bak` files before patching  
**Verified** ✓ Checks that patches applied correctly
