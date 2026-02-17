@echo off
set ASPNETCORE_ENVIRONMENT=Development
cd /d C:\Users\mikal\source\repos\AtlasControlPanel\src\Atlas.Web
start "" "bin\Debug\net10.0\Atlas.Web.exe" --urls "http://0.0.0.0:5263;https://0.0.0.0:443"
