#requires -Version 5.0
<#
    Restaura o override de EDID do XL2430 sem precisar do CRU.

    Depois de formatar o Windows o override some (ele vive no registro) e o
    monitor volta a 143/144 Hz. Este script regrava o mesmo bloco EDID que o CRU
    gerou e forca o driver a reler.

    Rode como Administrador.
#>

$ErrorActionPreference = 'Stop'

# --- EDID: 1920x1080 @ 110 Hz, HT 2001 / VT 1502 (330,61 MHz) -------------
$base = "00FFFFFFFFFFFF0009D13F7F455400001D1D0104A5351E783E57E0A5544F9D2612505400000001010101010101010101010101010101258180517038A64130203500E00E11000018000000FC005A4F57494520584C204C43440A00000010000000000000000000000000000000000010000000000000000000000000000001D2"
$ext  = "02030000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000FB"
$name = "015A4F57494520584C204C4344"   # 0x01 + "ZOWIE XL LCD"
$ser  = "0039374B3034343133534C30"       # 0x00 + serial de fabrica
$rng  = "0000380090001E00A0014A"         # 11 bytes, limites de taxa
$extn = "01"

function ToBytes([string]$hex) {
    $b = New-Object byte[] ($hex.Length / 2)
    for ($i = 0; $i -lt $b.Length; $i++) {
        $b[$i] = [Convert]::ToByte($hex.Substring($i * 2, 2), 16)
    }
    return ,$b
}

# --- integridade antes de gravar ------------------------------------------
foreach ($pair in @(@('base', $base), @('ext', $ext))) {
    $bytes = ToBytes $pair[1]
    if ($bytes.Length -ne 128) { throw "$($pair[0]): esperado 128 bytes, veio $($bytes.Length)" }
    $sum = 0
    foreach ($x in $bytes) { $sum = ($sum + $x) % 256 }
    if ($sum -ne 0) { throw "$($pair[0]): checksum invalido ($sum)" }
}

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'rode como Administrador'
}

# --- instancias do monitor ------------------------------------------------
$devs = Get-PnpDevice -Class Monitor -ErrorAction SilentlyContinue |
        Where-Object { $_.InstanceId -like 'DISPLAY\BNQ*' }

if (-not $devs) { throw 'monitor BenQ (BNQ*) nao encontrado' }

foreach ($dev in $devs) {
    $regPath = 'HKLM:\SYSTEM\CurrentControlSet\Enum\' + $dev.InstanceId + '\Device Parameters'
    if (-not (Test-Path $regPath)) { continue }

    $key = Join-Path $regPath 'EDID_OVERRIDE'
    New-Item -Path $key -Force | Out-Null
    Set-ItemProperty -Path $key -Name '0'                 -Value (ToBytes $base) -Type Binary
    Set-ItemProperty -Path $key -Name '1'                 -Value (ToBytes $ext)  -Type Binary
    Set-ItemProperty -Path $key -Name 'CRU_Name'          -Value (ToBytes $name) -Type Binary
    Set-ItemProperty -Path $key -Name 'CRU_Serial_Number' -Value (ToBytes $ser)  -Type Binary
    Set-ItemProperty -Path $key -Name 'CRU_Range_Limits'  -Value (ToBytes $rng)  -Type Binary
    Set-ItemProperty -Path $key -Name 'CRU_Extensions'    -Value (ToBytes $extn) -Type Binary
    Write-Host ("override gravado: " + $dev.InstanceId)
}

# --- forca o driver a reler -----------------------------------------------
Write-Host 'reiniciando o dispositivo de monitor...'
$active = $devs | Where-Object { $_.Status -eq 'OK' }
if ($active) {
    $active | Disable-PnpDevice -Confirm:$false
    Start-Sleep -Seconds 3
    $active | Enable-PnpDevice -Confirm:$false
    Start-Sleep -Seconds 5
}

$mode = Get-CimInstance Win32_VideoController | Select-Object -First 1
Write-Host ("agora: {0}x{1} @ {2} Hz" -f $mode.CurrentHorizontalResolution, $mode.CurrentVerticalResolution, $mode.CurrentRefreshRate)

if ($mode.CurrentRefreshRate -ne 110) {
    Write-Host 'ainda nao esta em 110 Hz - reinicie o Windows e rode de novo a verificacao.' -ForegroundColor Yellow
} else {
    Write-Host 'ok: 110 Hz ativo.' -ForegroundColor Green
}
