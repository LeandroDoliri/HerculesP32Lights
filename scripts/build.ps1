$proj = Split-Path -Parent $PSScriptRoot

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

# Detiene las apps temporalmente para que los .exe no queden bloqueados
$trayWasRunning = $false
foreach ($name in @("HerculesTray", "HerculesToggle")) {
    $running = Get-Process -Name $name -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "Deteniendo $name temporalmente para compilar..." -ForegroundColor Yellow
        if ($name -eq "HerculesTray") { $trayWasRunning = $true }
        Stop-Process -Name $name -Force -ErrorAction SilentlyContinue
    }
}
Start-Sleep -Milliseconds 600

# Los iconos van embebidos en los .exe: no hace falta copiarlos junto a ellos
$icon = "$assets\app.ico"
$resources = @(
    "/resource:$assets\icon_on.ico,HerculesP32.icon_on.ico",
    "/resource:$assets\icon_off.ico,HerculesP32.icon_off.ico"
)

Write-Host "[1/2] Compilando HerculesTray.exe..." -ForegroundColor Yellow
& $csc /nologo /target:winexe /win32icon:"$icon" @resources /out:"$proj\HerculesTray.exe" "$src\HerculesTray.cs" "$src\HerculesMidi.cs" "$src\HerculesShared.cs" "$src\HerculesUi.cs"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Fallo la compilacion de HerculesTray.exe"
    exit $LASTEXITCODE
}

Write-Host "[2/2] Compilando HerculesToggle.exe..." -ForegroundColor Yellow
& $csc /nologo /target:winexe /win32icon:"$icon" /out:"$proj\HerculesToggle.exe" "$src\HerculesToggle.cs" "$src\HerculesMidi.cs" "$src\HerculesShared.cs" "$src\HerculesUi.cs"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Fallo la compilacion de HerculesToggle.exe"
    exit $LASTEXITCODE
}

if ($trayWasRunning) {
    Write-Host "Reiniciando HerculesTray.exe..." -ForegroundColor Cyan
    Start-Process -FilePath "$proj\HerculesTray.exe"
}

Write-Host "Compilacion completada exitosamente!" -ForegroundColor Green
Write-Host "Ejecutables listos en: $proj" -ForegroundColor Green
