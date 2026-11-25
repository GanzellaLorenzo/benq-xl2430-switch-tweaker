#requires -Version 5.0
$ErrorActionPreference = 'Stop'

# Builds XLKeeper.exe with the C# compiler that ships with .NET Framework 4.x.
# No SDK, no MSBuild, nothing to install.

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw 'csc.exe not found. .NET Framework 4.x is required (it is built into Windows 10/11).'
}

Push-Location $PSScriptRoot
try {
    & $csc /nologo /target:winexe /optimize+ /out:XLKeeper.exe src\XLKeeper.cs
    if ($LASTEXITCODE -ne 0) { throw 'compilation failed' }
    Write-Host 'XLKeeper.exe built.' -ForegroundColor Green
}
finally {
    Pop-Location
}
