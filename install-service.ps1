# Steam Free Games Claimer Service Installer
# Führe dieses Skript als Administrator aus

param(
    [string]$ServiceName = "Steam Free Games Claimer",
    [string]$DisplayName = "Steam Free Games Claimer",
    [string]$Description = "Automatischer Claimer für kostenlose Steam-Spiele",
    [string]$BinPath = ""
)

# Prüfe Administrator-Rechte
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "Dieses Skript muss als Administrator ausgeführt werden!"
    exit 1
}

# Bestimme den Pfad zur ausführbaren Datei
if ([string]::IsNullOrEmpty($BinPath)) {
    $currentDir = Get-Location
    $BinPath = Join-Path $currentDir "AutoClaimFreeSteamGames.exe"
    
    # Prüfe ob die Datei existiert
    if (-not (Test-Path $BinPath)) {
        Write-Error "AutoClaimFreeSteamGames.exe nicht gefunden in: $currentDir"
        Write-Host "Stelle sicher, dass das Projekt kompiliert wurde: dotnet publish --configuration Release"
        exit 1
    }
}

Write-Host "Installiere Steam Free Games Claimer Service..." -ForegroundColor Green
Write-Host "Service Name: $ServiceName" -ForegroundColor Yellow
Write-Host "Display Name: $DisplayName" -ForegroundColor Yellow
Write-Host "BinPath: $BinPath" -ForegroundColor Yellow

# Prüfe ob der Service bereits existiert
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Service existiert bereits. Stoppe und entferne ihn..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete $ServiceName
    Start-Sleep -Seconds 2
}

# Erstelle den Service
Write-Host "Erstelle Service..." -ForegroundColor Green
$result = sc.exe create $ServiceName binPath= "`"$BinPath`"" DisplayName= "$DisplayName" start= auto

if ($LASTEXITCODE -eq 0) {
    Write-Host "Service erfolgreich erstellt!" -ForegroundColor Green
    
    # Setze Beschreibung
    sc.exe description $ServiceName "$Description"
    
    # Starte den Service
    Write-Host "Starte Service..." -ForegroundColor Green
    Start-Service -Name $ServiceName
    
    if ($?) {
        Write-Host "Service erfolgreich gestartet!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Service-Status:" -ForegroundColor Cyan
        Get-Service -Name $ServiceName | Format-Table -AutoSize
        
        Write-Host ""
        Write-Host "Nützliche Befehle:" -ForegroundColor Cyan
        Write-Host "  Service stoppen: Stop-Service '$ServiceName'" -ForegroundColor White
        Write-Host "  Service starten: Start-Service '$ServiceName'" -ForegroundColor White
        Write-Host "  Service entfernen: sc.exe delete '$ServiceName'" -ForegroundColor White
        Write-Host "  Service-Status prüfen: Get-Service '$ServiceName'" -ForegroundColor White
    } else {
        Write-Error "Fehler beim Starten des Services!"
        Write-Host "Prüfe die Windows Event Logs für weitere Details." -ForegroundColor Yellow
    }
} else {
    Write-Error "Fehler beim Erstellen des Services!"
    Write-Host "Exit Code: $LASTEXITCODE" -ForegroundColor Red
}

Write-Host ""
Write-Host "Installation abgeschlossen!" -ForegroundColor Green 