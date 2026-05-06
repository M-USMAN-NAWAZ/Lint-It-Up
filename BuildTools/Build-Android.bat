@echo off
setlocal
powershell -ExecutionPolicy Bypass -File "%~dp0Build-Android.ps1"
pause
