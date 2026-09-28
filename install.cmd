@echo off
setlocal
echo Development package installer. Releases are installed with install.ps1.
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -PackageRoot "%~dp0." -DevelopmentPackage %*
exit /b %errorlevel%
