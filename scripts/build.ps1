$proj = Split-Path -Parent $PSScriptRoot
if (-not $proj) { $proj = "C:\Users\leand\HerculesP32Lights" }

$src = "$proj\src"
$assets = "$proj\assets"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $csc)) {
    Write-Error "No se encontro csc.exe en $csc"
    exit 1
}

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host " Compilando Hercules P32 Lights Controller   " -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# Check if HerculesTray is running and stop it temporarily to avoid file lock
$wasRunning = $false
$runningProc = Get-Process -Name "HerculesTray" -ErrorAction SilentlyContinue
if ($runningProc) {
    Write-Host "Deteniendo HerculesTray temporalmente para compilar..." -ForegroundColor Yellow
    $wasRunning = $true
    Stop-Process -Name "HerculesTray" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 600
}

# Ensure icon is present
$icon = "$proj\app.ico"
if (-not (Test-Path $icon)) { $icon = "$assets\app.ico" }

Write-Host "[1/2] Compilando HerculesTray.exe..." -ForegroundColor Yellow
& $csc /target:winexe /win32icon:"$icon" /out:"$proj\HerculesTray.exe" "$src\HerculesTray.cs" "$src\HerculesMidi.cs"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Fallo la compilacion de HerculesTray.exe"
    exit $LASTEXITCODE
}

Write-Host "[2/2] Compilando HerculesToggle.exe..." -ForegroundColor Yellow
& $csc /target:winexe /win32icon:"$icon" /out:"$proj\HerculesToggle.exe" "$src\HerculesToggle.cs" "$src\HerculesMidi.cs"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Fallo la compilacion de HerculesToggle.exe"
    exit $LASTEXITCODE
}

if ($wasRunning) {
    Write-Host "Reiniciando HerculesTray.exe..." -ForegroundColor Cyan
    Start-Process -FilePath "$proj\HerculesTray.exe"
}

Write-Host "Compilacion completada exitosamente!" -ForegroundColor Green
Write-Host "Ejecutables listos en: $proj" -ForegroundColor Green
