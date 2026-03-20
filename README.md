# 🗄️ Database Starter

> Manage portable MySQL, MariaDB & PostgreSQL instances on Windows — without traditional installation.

![Version](https://img.shields.io/badge/Version-0.0.1-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D6)
![License](https://img.shields.io/badge/License-MIT-green)

---

## 📋 Overview

**Database Starter** is a WPF desktop application that allows you to install, start, stop, and uninstall portable database servers directly on your local machine — without administrator privileges and without Windows services.

The application manages standalone, portable instances of the following database engines:

| Engine       | Supported Versions                  | Default Port |
|--------------|-------------------------------------|--------------|
| 🐬 MySQL     | 9.5.0, 8.4.7, 8.0.44              | 3306         |
| 🦭 MariaDB   | 12.2.2, 11.8.6, 11.4.10, 10.11.16 | 3307         |
| 🐘 PostgreSQL | 18.3, 17.9, 16.13, 16.3, 15.17    | 5432         |

---

## ✨ Features

- **One-Click Installation** — Downloads the official portable distribution and extracts it automatically
- **Version Selection** — A specific version per engine can be selected before installation
- **Start / Stop** — Database processes are started and stopped directly as local processes
- **Uninstallation** — Cleanly removes all files of an instance from the system
- **Progress Indicator** — Download progress in percent with status display during installation
- **Live Status** — Colored status indicators (🔴 Not installed / 🟡 Installing / 🟢 Running / 🔵 Stopped)
- **Log Output** — Integrated, expandable log per database instance
- **Settings** — Configuration (paths, ports, versions) is automatically persisted in `settings.json`
- **Graceful Shutdown** — When the application exits, all running servers are shut down gracefully
- **Localization** — Available in German and English (auto-detected from system language)

---

## 🖼️ Screenshots

*The application uses a modern dark theme in Catppuccin style with cards for each database engine.*

---

## 🛠️ Prerequisites

- **Windows 10/11** (x64)
- [**.NET 10.0 Runtime**](https://dotnet.microsoft.com/download/dotnet/10.0) (or SDK for building from source)

> ℹ️ No administrator privileges are required. All database files are stored under `%LOCALAPPDATA%\DatabaseStarter`.

---

## 🚀 Installation & Startup

### Option 1: Download Release

1. Download the latest release from the [Releases page](../../releases)
2. Extract the archive to any directory
3. Run `DatabaseStarter.exe`

### Option 2: Build from Source

```bash
# Clone repository
git clone https://github.com/<user>/DatabaseStarter.git
cd DatabaseStarter

# Build
dotnet build -c Release

# Run
dotnet run --project DatabaseStarter
```

---

## 📖 Usage

1. **Select Version** — Choose the desired database version from the dropdown
2. **Install** — Click `📥 Install` to download and set up the portable distribution
3. **Start** — After installation, the server can be started with `▶ Start`
4. **Connect** — Connect with your preferred client to `localhost` and the displayed port
5. **Stop** — Stop the server with `⏹ Stop`
6. **Uninstall** — Remove the instance with `🗑 Uninstall` (only possible when the server is stopped)

---

## 📁 Project Structure

```
DatabaseStarter/
├── DatabaseStarter.sln              # Solution file
└── DatabaseStarter/                 # WPF main project
    ├── App.xaml(.cs)                # Application Entry Point
    ├── MainWindow.xaml(.cs)         # Main window (UI)
    ├── Converters/
    │   └── Converters.cs            # WPF Value Converters (Status → Color, etc.)
    ├── Models/
    │   ├── AppSettings.cs           # Persisted application settings
    │   ├── DatabaseDefaults.cs      # Default versions, ports & download URLs
    │   ├── DatabaseEngine.cs        # Enum: MySQL, MariaDB, PostgreSQL
    │   ├── DatabaseInstanceInfo.cs  # Instance configuration (path, port, version, …)
    │   ├── DatabaseStatus.cs        # Enum: NotInstalled, Installing, Installed, Running
    │   └── DatabaseVersionInfo.cs   # Version information incl. download URL
    ├── Resources/
    │   ├── Strings.resx             # Localized strings (German, default)
    │   ├── Strings.en.resx          # Localized strings (English)
    │   └── Strings.Designer.cs      # Auto-generated resource accessor
    ├── Services/
    │   ├── IDatabaseEngineService.cs    # Interface for engine-specific operations
    │   ├── MySqlEngineService.cs        # MySQL implementation
    │   ├── MariaDbEngineService.cs      # MariaDB implementation
    │   ├── PostgreSqlEngineService.cs   # PostgreSQL implementation
    │   ├── DownloadService.cs           # HTTP download with progress reporting
    │   ├── ProcessService.cs            # Process management (start/stop)
    │   └── SettingsService.cs           # JSON-based settings management
    └── ViewModels/
        ├── MainViewModel.cs         # Main window ViewModel
        ├── DatabaseViewModel.cs     # ViewModel per database card
        ├── ViewModelBase.cs         # INotifyPropertyChanged base class
        └── RelayCommand.cs          # ICommand implementation
```

---

## ⚙️ Configuration

Settings are automatically saved in `settings.json` next to the application:

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

| Field          | Description                                      |
|----------------|--------------------------------------------------|
| `BasePath`     | Base directory for all database installations    |
| `Engine`       | 0 = MySQL, 1 = MariaDB, 2 = PostgreSQL          |
| `Version`      | Selected version of the engine                   |
| `InstallPath`  | Installation directory of the portable binaries  |
| `DataDir`      | Data directory of the database                   |
| `Port`         | TCP port the server listens on                   |

---

## 🏗️ Technology Stack

- **UI Framework:** WPF (.NET 10.0)
- **Architecture:** MVVM (Model-View-ViewModel)
- **Language:** C# 13
- **Persistence:** System.Text.Json
- **Localization:** .resx resource files (German & English)
- **Design:** Catppuccin Mocha Dark Theme

---

## 🤝 Contributing

Contributions are welcome! Here's how:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Commit your changes (`git commit -m 'Feature: Description'`)
4. Push the branch (`git push origin feature/my-feature`)
5. Create a Pull Request

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

## ⚠️ Notes

- The downloaded database binaries are subject to the respective licenses of the vendors (Oracle/MySQL, MariaDB Foundation, PostgreSQL Global Development Group).
- This tool is intended for **development and testing purposes** — not for production use.
- The database processes run as local user processes, not as Windows services.
