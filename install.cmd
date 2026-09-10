@echo off
setlocal
echo Development package installer. Customer releases use the signed install.ps1 entry.
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -PackageRoot "%~dp0." -DevelopmentPackage %*
exit /b %errorlevel%
