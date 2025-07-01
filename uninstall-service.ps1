# Steam Free Games Claimer Service Deinstaller
# Führe dieses Skript als Administrator aus

param(
    [string]$ServiceName = "Steam Free Games Claimer"
)

# Prüfe Administrator-Rechte
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "Dieses Skript muss als Administrator ausgeführt werden!"
    exit 1
}

Write-Host "Entferne Steam Free Games Claimer Service..." -ForegroundColor Green
Write-Host "Service Name: $ServiceName" -ForegroundColor Yellow

# Prüfe ob der Service existiert
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existingService) {
    Write-Host "Service '$ServiceName' wurde nicht gefunden." -ForegroundColor Yellow
    exit 0
}

# Zeige aktuellen Status
Write-Host "Aktueller Service-Status:" -ForegroundColor Cyan
Get-Service -Name $ServiceName | Format-Table -AutoSize

# Stoppe den Service
Write-Host "Stoppe Service..." -ForegroundColor Green
Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3

# Entferne den Service
Write-Host "Entferne Service..." -ForegroundColor Green
$result = sc.exe delete $ServiceName

if ($LASTEXITCODE -eq 0) {
    Write-Host "Service erfolgreich entfernt!" -ForegroundColor Green
} else {
    Write-Error "Fehler beim Entfernen des Services!"
    Write-Host "Exit Code: $LASTEXITCODE" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Deinstallation abgeschlossen!" -ForegroundColor Green 