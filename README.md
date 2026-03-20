# 🗄️ Database Starter

> Portable MySQL, MariaDB & PostgreSQL Instanzen auf Windows verwalten — ohne klassische Installation.

![Version](https://img.shields.io/badge/Version-0.0.1-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D6)
![License](https://img.shields.io/badge/License-MIT-green)

---

## 📋 Überblick

**Database Starter** ist eine WPF-Desktop-Anwendung, mit der portable Datenbank-Server direkt auf dem lokalen Rechner installiert, gestartet, gestoppt und deinstalliert werden können — ganz ohne Administratorrechte und ohne Windows-Dienste.

Die Anwendung verwaltet eigenständige, portable Instanzen der folgenden Datenbank-Engines:

| Engine       | Unterstützte Versionen        | Standard-Port |
|--------------|-------------------------------|---------------|
| 🐬 MySQL     | 8.4.7, 8.0.44                | 3306          |
| 🦭 MariaDB   | 11.8.6, 11.4.10, 10.11.16   | 3307          |
| 🐘 PostgreSQL | 18.3, 17.9, 16.13, 16.3, 15.17 | 5432          |

---

## ✨ Features

- **Ein-Klick-Installation** — Lädt die offizielle portable Distribution herunter und entpackt sie automatisch
- **Versionsauswahl** — Vor der Installation kann eine spezifische Version je Engine ausgewählt werden
- **Starten / Stoppen** — Datenbank-Prozesse werden direkt als lokale Prozesse gestartet und gestoppt
- **Deinstallation** — Entfernt alle Dateien einer Instanz sauber vom System
- **Fortschrittsanzeige** — Download-Fortschritt in Prozent mit Statusanzeige während der Installation
- **Live-Status** — Farbige Status-Indikatoren (🔴 Nicht installiert / 🟡 Wird installiert / 🟢 Läuft / 🔵 Gestoppt)
- **Log-Ausgabe** — Integriertes, aufklappbares Log je Datenbank-Instanz
- **Einstellungen** — Konfiguration (Pfade, Ports, Versionen) wird automatisch in `settings.json` persistiert
- **Graceful Shutdown** — Beim Beenden der Anwendung werden alle laufenden Server sauber heruntergefahren

---

## 🖼️ Screenshots

*Die Anwendung nutzt ein modernes Dark-Theme im Catppuccin-Stil mit Cards für jede Datenbank-Engine.*

---

## 🛠️ Voraussetzungen

- **Windows 10/11** (x64)
- [**.NET 10.0 Runtime**](https://dotnet.microsoft.com/download/dotnet/10.0) (oder SDK zum Selbstbauen)

> ℹ️ Es werden keine Administratorrechte benötigt. Alle Datenbank-Dateien werden unter `%LOCALAPPDATA%\DatabaseStarter` abgelegt.

---

## 🚀 Installation & Start

### Option 1: Release herunterladen

1. Lade das neueste Release von der [Releases-Seite](../../releases) herunter
2. Entpacke das Archiv in ein beliebiges Verzeichnis
3. Starte `DatabaseStarter.exe`

### Option 2: Selbst bauen

```bash
# Repository klonen
git clone https://github.com/<user>/DatabaseStarter.git
cd DatabaseStarter

# Bauen
dotnet build -c Release

# Starten
dotnet run --project DatabaseStarter
```

---

## 📖 Verwendung

1. **Version wählen** — Wähle im Dropdown die gewünschte Datenbank-Version aus
2. **Installieren** — Klicke auf `📥 Installieren`, um die portable Distribution herunterzuladen und einzurichten
3. **Starten** — Nach der Installation kann der Server mit `▶ Starten` gestartet werden
4. **Verbinden** — Verbinde dich mit deinem bevorzugten Client auf `localhost` und dem angezeigten Port
5. **Stoppen** — Beende den Server mit `⏹ Stoppen`
6. **Deinstallieren** — Entferne die Instanz mit `🗑 Deinstallieren` (nur möglich, wenn der Server gestoppt ist)

---

## 📁 Projektstruktur

```
DatabaseStarter/
├── DatabaseStarter.sln              # Solution-Datei
└── DatabaseStarter/                 # WPF-Hauptprojekt
    ├── App.xaml(.cs)                # Application Entry Point
    ├── MainWindow.xaml(.cs)         # Hauptfenster (UI)
    ├── Converters/
    │   └── Converters.cs            # WPF Value Converters (Status → Farbe, etc.)
    ├── Models/
    │   ├── AppSettings.cs           # Persistierte Anwendungseinstellungen
    │   ├── DatabaseDefaults.cs      # Standard-Versionen, Ports & Download-URLs
    │   ├── DatabaseEngine.cs        # Enum: MySQL, MariaDB, PostgreSQL
    │   ├── DatabaseInstanceInfo.cs  # Instanz-Konfiguration (Pfad, Port, Version, …)
    │   ├── DatabaseStatus.cs        # Enum: NotInstalled, Installing, Installed, Running
    │   └── DatabaseVersionInfo.cs   # Versionsinformationen inkl. Download-URL
    ├── Services/
    │   ├── IDatabaseEngineService.cs    # Interface für Engine-spezifische Operationen
    │   ├── MySqlEngineService.cs        # MySQL-Implementierung
    │   ├── MariaDbEngineService.cs      # MariaDB-Implementierung
    │   ├── PostgreSqlEngineService.cs   # PostgreSQL-Implementierung
    │   ├── DownloadService.cs           # HTTP-Download mit Fortschrittsanzeige
    │   ├── ProcessService.cs            # Prozess-Management (Start/Stop)
    │   └── SettingsService.cs           # JSON-basierte Einstellungsverwaltung
    └── ViewModels/
        ├── MainViewModel.cs         # Hauptfenster ViewModel
        ├── DatabaseViewModel.cs     # ViewModel je Datenbank-Card
        ├── ViewModelBase.cs         # INotifyPropertyChanged-Basisklasse
        └── RelayCommand.cs          # ICommand-Implementierung
```

---

## ⚙️ Konfiguration

Die Einstellungen werden automatisch in `settings.json` neben der Anwendung gespeichert:

```json
{
  "BasePath": "C:\\Users\\<User>\\AppData\\Local\\DatabaseStarter",
  "Instances": [
    {
      "Engine": 0,
      "Version": "8.4.7",
      "InstallPath": "…\\mysql",
      "DataDir": "…\\mysql\\data",
      "Port": 3306,
      "IsInitialized": false
    }
  ]
}
```

| Feld           | Beschreibung                                      |
|----------------|---------------------------------------------------|
| `BasePath`     | Basisverzeichnis für alle Datenbank-Installationen |
| `Engine`       | 0 = MySQL, 1 = MariaDB, 2 = PostgreSQL            |
| `Version`      | Gewählte Version der Engine                        |
| `InstallPath`  | Installationsverzeichnis der portablen Binaries    |
| `DataDir`      | Datenverzeichnis der Datenbank                     |
| `Port`         | TCP-Port, auf dem der Server lauscht               |

---

## 🏗️ Technologie-Stack

- **UI-Framework:** WPF (.NET 10.0)
- **Architektur:** MVVM (Model-View-ViewModel)
- **Sprache:** C# 13
- **Persistierung:** System.Text.Json
- **Design:** Catppuccin Mocha Dark Theme

---

## 🤝 Mitwirken

Beiträge sind willkommen! So geht's:

1. Forke das Repository
2. Erstelle einen Feature-Branch (`git checkout -b feature/mein-feature`)
3. Committe deine Änderungen (`git commit -m 'Feature: Beschreibung'`)
4. Pushe den Branch (`git push origin feature/mein-feature`)
5. Erstelle einen Pull Request

---

## 📄 Lizenz

Dieses Projekt steht unter der [MIT-Lizenz](LICENSE).

---

## ⚠️ Hinweise

- Die heruntergeladenen Datenbank-Binaries unterliegen den jeweiligen Lizenzen der Hersteller (Oracle/MySQL, MariaDB Foundation, PostgreSQL Global Development Group).
- Dieses Tool ist für **Entwicklungs- und Testzwecke** gedacht — nicht für den Produktiveinsatz.
- Die Datenbank-Prozesse laufen als lokale Benutzerprozesse, nicht als Windows-Dienste.

