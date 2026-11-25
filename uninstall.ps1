#requires -Version 5.0
$ErrorActionPreference = 'SilentlyContinue'

# Removes the scheduled task, stops the process and deletes the install folder.

schtasks /delete /tn XLKeeper /f 2>$null | Out-Null
taskkill /f /im XLKeeper.exe 2>$null | Out-Null
Remove-Item (Join-Path $env:LOCALAPPDATA 'XLKeeper') -Recurse -Force 2>$null

Write-Host 'XLKeeper removed.'
