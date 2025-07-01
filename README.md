# Steam Free Games Auto Claimer

Ein automatischer C# Windows Service, der kostenlose Steam-Spiele ("Free to Keep") automatisch auf deinem Account aktiviert.

## Features

- **Automatische Erkennung**: Überwacht SteamDB auf kostenlose Spiele
- **Intelligente Filterung**: Aktiviert nur "Free to Keep" Spiele (nicht Free Weekends)
- **Windows Service**: Läuft im Hintergrund ohne Benutzerinteraktion
- **Robuste Fehlerbehandlung**: Automatische Wiederherstellung bei Fehlern
- **Detailliertes Logging**: Vollständige Protokollierung aller Aktivitäten
- **Konfigurierbar**: Anpassbare Prüfintervalle und Einstellungen

## Voraussetzungen

- Windows 10/11
- .NET 8.0 Runtime
- Steam Account mit aktiviertem Steam Guard (empfohlen)
- Internetverbindung

## Installation

### 1. Projekt kompilieren

```bash
dotnet restore
dotnet build --configuration Release
dotnet publish --configuration Release --output ./publish
```

### 2. Konfiguration einrichten

Bearbeite die Datei `appsettings.json`:

```json
{
  "SteamSettings": {
    "Username": "DEIN_STEAM_USERNAME",
    "Password": "DEIN_STEAM_PASSWORD",
    "SteamGuardCode": "",
    "CheckIntervalMinutes": 30,
    "SteamDbUrl": "https://steamdb.info/upcoming/free/"
  }
}
```

**Wichtige Hinweise:**
- Ersetze `DEIN_STEAM_USERNAME` und `DEIN_STEAM_PASSWORD` mit deinen echten Steam-Credentials
- Wenn du Steam Guard aktiviert hast, musst du den Code manuell eingeben (siehe unten)
- Das Prüfintervall kann angepasst werden (Standard: 30 Minuten)

### 3. Als Windows Service installieren

```bash
# Als Administrator ausführen
sc create "Steam Free Games Claimer" binPath="C:\Pfad\zu\deinem\AutoClaimFreeSteamGames.exe"
sc description "Steam Free Games Claimer" "Automatischer Claimer für kostenlose Steam-Spiele"
sc start "Steam Free Games Claimer"
```

### 4. Als Konsolenanwendung testen

```bash
dotnet run -- --console
```

## Konfiguration

### Steam Guard Setup

Wenn du Steam Guard aktiviert hast:

1. Starte die Anwendung im Konsolenmodus: `dotnet run -- --console`
2. Gib den Steam Guard Code ein, wenn du dazu aufgefordert wirst
3. Der Code wird in der `appsettings.json` gespeichert

### Erweiterte Einstellungen

```json
{
  "SteamSettings": {
    "Username": "dein_username",
    "Password": "dein_password",
    "SteamGuardCode": "dein_guard_code",
    "CheckIntervalMinutes": 15,
    "SteamDbUrl": "https://steamdb.info/upcoming/free/",
    "MaxRetries": 3,
    "RetryDelaySeconds": 60
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

## Logging

Die Anwendung erstellt detaillierte Logs:

- **Konsolenausgabe**: Für sofortige Überwachung
- **Datei-Logs**: `logs/steam-claimer-YYYY-MM-DD.log`
- **Windows Event Log**: Für Service-Ereignisse

### Log-Level

- `Information`: Normale Aktivitäten
- `Warning`: Probleme die automatisch behoben werden
- `Error`: Kritische Fehler

## Troubleshooting

### Häufige Probleme

1. **Login-Fehler**
   - Prüfe deine Steam-Credentials
   - Stelle sicher, dass Steam Guard korrekt konfiguriert ist
   - Versuche manuellen Login auf steamcommunity.com

2. **Keine Spiele gefunden**
   - Prüfe deine Internetverbindung
   - SteamDB könnte temporär nicht erreichbar sein
   - Es sind möglicherweise wirklich keine kostenlosen Spiele verfügbar

3. **Service startet nicht**
   - Führe als Administrator aus
   - Prüfe die Windows Event Logs
   - Stelle sicher, dass .NET 8.0 installiert ist

### Debug-Modus

Für detaillierte Fehlerdiagnose:

```bash
dotnet run -- --console --environment Development
```

## Sicherheit

- **Credentials**: Werden nur lokal in `appsettings.json` gespeichert
- **Verschlüsselung**: Passwörter sollten in einer Produktionsumgebung verschlüsselt werden
- **Berechtigungen**: Der Service benötigt nur Internetzugang

## Performance

- **CPU**: Minimal (< 1% bei normaler Nutzung)
- **RAM**: ~50-100 MB
- **Netzwerk**: Nur bei Prüfungen aktiv
- **Speicher**: Logs werden automatisch rotiert

## Beitragen

Verbesserungsvorschläge und Bug-Reports sind willkommen!

## ⚠Haftungsausschluss

Diese Software ist für Bildungszwecke erstellt. Die Verwendung erfolgt auf eigene Gefahr. Beachte die Steam-Nutzungsbedingungen.

## 📄 Lizenz

MIT License - siehe LICENSE-Datei für Details.

## 🔗 Links

- [SteamDB Free Games](https://steamdb.info/upcoming/free/)
- [Steam Community](https://steamcommunity.com/)
- [.NET 8.0 Download](https://dotnet.microsoft.com/download/dotnet/8.0) 
