$proj = Split-Path -Parent $PSScriptRoot
if (-not $proj) { $proj = "C:\Users\leand\HerculesP32Lights" }

$WshShell = New-Object -comObject WScript.Shell
$DesktopPath = [Environment]::GetFolderPath("Desktop")
$StartMenuPath = [Environment]::GetFolderPath("Programs")
$TargetExe = "$proj\HerculesToggle.exe"
$IconPath = "$proj\app.ico"

Write-Host "Creando accesos directos..." -ForegroundColor Cyan

# 1. Acceso directo en Escritorio para el interruptor
$DesktopShortcut = $WshShell.CreateShortcut("$DesktopPath\Luces Hercules P32.lnk")
$DesktopShortcut.TargetPath = $TargetExe
$DesktopShortcut.WorkingDirectory = $proj
$DesktopShortcut.IconLocation = "$IconPath, 0"
$DesktopShortcut.Description = "Prender y apagar luces del controlador Hercules P32 DJ"
$DesktopShortcut.Save()

# 2. Acceso directo en Menú Inicio
$StartShortcut = $WshShell.CreateShortcut("$StartMenuPath\Luces Hercules P32.lnk")
$StartShortcut.TargetPath = $TargetExe
$StartShortcut.WorkingDirectory = $proj
$StartShortcut.IconLocation = "$IconPath, 0"
$StartShortcut.Description = "Prender y apagar luces del controlador Hercules P32 DJ"
$StartShortcut.Save()

# 3. Acceso directo a la carpeta del proyecto en el Escritorio (opcional para acceso facil)
$FolderShortcut = $WshShell.CreateShortcut("$DesktopPath\Carpeta Proyecto Hercules P32.lnk")
$FolderShortcut.TargetPath = $proj
$FolderShortcut.Description = "Carpeta con archivos del proyecto Hercules P32 Lights"
$FolderShortcut.Save()

Write-Host "Accesos directos creados correctamente en Escritorio y Menu Inicio!" -ForegroundColor Green
