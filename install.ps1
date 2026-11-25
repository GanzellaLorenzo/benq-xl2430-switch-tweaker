#requires -Version 5.0
$ErrorActionPreference = 'Stop'

# Installs the built keeper to %LOCALAPPDATA%\XLKeeper and registers it to
# start at logon, in the user's interactive session.
#
# The /it flag is not optional: DDC/CI only works from the session that owns
# the display. A task running as SYSTEM in session 0 cannot reach the monitor.

$exe = Join-Path $PSScriptRoot 'XLKeeper.exe'
if (-not (Test-Path $exe)) {
    throw 'XLKeeper.exe not found. Run .\build.ps1 first.'
}

$dir = Join-Path $env:LOCALAPPDATA 'XLKeeper'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
Copy-Item $exe (Join-Path $dir 'XLKeeper.exe') -Force

schtasks /create /tn XLKeeper /tr "`"$dir\XLKeeper.exe`"" /sc onlogon /it /f | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'failed to register the scheduled task' }

schtasks /run /tn XLKeeper | Out-Null

Write-Host "Installed to $dir" -ForegroundColor Green
Write-Host 'Registered to start at logon (interactive session).'
Write-Host "Log: $env:TEMP\xlkeeper.log"
