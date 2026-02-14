New-NetFirewallRule -DisplayName "Atlas Control Panel Web" -Direction Inbound -Protocol TCP -LocalPort 5263 -Action Allow -Profile Private
