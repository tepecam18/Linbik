# Linbik.CLI

Command-line tool for bootstrapping Linbik projects and managing service registrations.

## 📦 Installation

```bash
dotnet tool install --global Linbik.Cli
```

## 🚀 Commands

### `linbik init`

Interactive setup: provisions a service on the Linbik platform (OAuth claim flow), writes `appsettings.json`, and injects the required `AddLinbik...`/`UseLinbik...` calls into `Program.cs`. Prompts for service name, app URL, callback path, and auth provider (JWT or PASETO) if not auto-detected.

```bash
linbik init [--url <linbik-server-url>] [--name <app-name>]
```

| Option | Description |
|--------|-------------|
| `--url` | Linbik server URL (default: `https://api.linbik.com`) |
| `--name` | Application name (default: auto-detected from the `.csproj`/`package.json`/directory name) |

### `linbik status`

Shows local credentials (`.linbik/credentials.json`), the `appsettings.json` Linbik section, and live connectivity/claim status from the Linbik server. Takes no options.

```bash
linbik status
```

### `linbik export-config`

Writes the locally stored credentials (from a prior `linbik init`) into `appsettings.json`, without re-provisioning.

```bash
linbik export-config [--path <path-to-appsettings.json>]
```

| Option | Description |
|--------|-------------|
| `--path` | Path to `appsettings.json` (default: auto-detect in the current directory) |

### `linbik doctor`

Read-only health check — diagnoses `.linbik/credentials.json`, `appsettings.json`, and `Program.cs` without modifying anything. Reports missing `AddLinbik()`/`EnsureLinbik()`/auth-provider calls, config/`Program.cs` provider mismatches (e.g. `PasetoAuth` configured but `AddLinbikPasetoAuth()` missing), and `ServiceId` mismatches between credentials and `appsettings.json`. Takes no options.

```bash
linbik doctor
```

## 🔧 Requirements

- .NET 10.0 SDK or later
- A Linbik account at [linbik.com](https://linbik.com)

## 📖 Documentation

- [Full Documentation](https://github.com/tepecam18/Linbik)
- [Linbik.Core](../Linbik.Core/README.md)
- [Linbik.JwtAuthManager](../Linbik.JwtAuthManager/README.md)
- [Linbik.PasetoAuthManager](../Linbik.PasetoAuthManager/README.md)
- [Linbik.Server](../Linbik.Server/README.md)
- [Linbik.YARP](../Linbik.YARP/README.md)
- [Linbik.Slices](../Linbik.Slices/README.md)

## 📄 License

MIT License

**Contact**: info@linbik.com

---

**Version**: 1.2.0  
**Platform**: .NET 10.0 (net10.0)  
**Last Updated**: 9 Eylül 2026
