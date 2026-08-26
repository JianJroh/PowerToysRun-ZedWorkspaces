# Zed Recent Projects — PowerToys Run plugin

A [PowerToys Run](https://learn.microsoft.com/windows/powertoys/run) plugin that lists the projects you recently opened in the [Zed editor](https://zed.dev) and opens them back in Zed.

## Features

- Lists recent **local** projects (folders) opened in Zed, newest first.
- Fuzzy matching against the project name **and** its full path (multiple words are ANDed).
- Each result shows the absolute path plus a relative last-opened time (e.g. "3 hours ago").
- `Enter` opens the project in Zed in a new window.
- Right-click context menu: **Open in Zed**, **Reveal in File Explorer**, **Copy path**.
- Reads Zed's SQLite database read-only — Zed never needs to be running and nothing is written.
- Configurable via the PowerToys settings page: max results, open behavior, relative time.

## Requirements

- Windows 10/11, x64 or ARM64
- [PowerToys](https://learn.microsoft.com/windows/powertoys/install) (PowerToys Run enabled)
- [Zed](https://zed.dev/download) installed (the plugin launches the `zed` CLI; standard install locations are probed if it is not on PATH)

## Install

1. Download the latest `ZedRecentProjects-<arch>.zip` from the [Releases](../../releases) page (pick `win-x64` or `win-arm64` matching your CPU).
2. Extract the folder so you get a `ZedRecentProjects\` folder containing `ZedRecentProjects.dll` and `plugin.json`.
3. Copy that folder into your PowerToys Run plugins directory:

   ```
   %LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\
   ```

   So the final layout is `...\Plugins\ZedRecentProjects\plugin.json`.
4. Restart PowerToys (fully quit from the tray icon, then start it again).

## Usage

Open PowerToys Run (`Alt+Space`) and start typing a project name or path — for example `vibe` or `zed`. Projects that match are shown; press `Enter` to open the selected project in Zed.

The plugin is **global**: results appear for any query. Typing `zed` first scopes the search to this plugin only.

## Settings

In *PowerToys Settings → PowerToys Run → Plugins → Zed Recent Projects*:

| Setting | Default | Description |
|---|---|---|
| Maximum number of recent projects | 20 | How many recent projects are listed. |
| Always open in a new window | Yes | Open with `zed --new <path>`. When disabled, Zed's default behavior decides. |
| Show relative last-opened time | Yes | Append e.g. "3 hours ago" to each result. |

## How it reads the recent projects

Zed keeps its workspace history in a local SQLite database:

```
%LOCALAPPDATA%\Zed\db\<channel>\db.sqlite
```

The plugin queries the `workspaces` table (`paths`, `timestamp`, `remote_connection_id`), keeps only entries that are **local directories**, deduplicates, and sorts by last-opened time. The database is opened read-only with `Mode=ReadOnly` and a shared cache, so it never interferes with a running Zed.

> The schema has changed across Zed versions and may change again. If a future Zed release breaks the plugin, open an [issue](../../issues).

## Build from source

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```powershell
# x64
dotnet build src\ZedRecentProjects\ZedRecentProjects.csproj -c Release -p:Platform=x64 -r win-x64

# ARM64
dotnet build src\ZedRecentProjects\ZedRecentProjects.csproj -c Release -p:Platform=ARM64 -r win-arm64
```

The plugin folder is produced under `src\ZedRecentProjects\bin\<Platform>\Release\net9.0-windows10.0.26100.0\win-<arch>\`.

## Development

### Repository layout

```
├── src/ZedRecentProjects/   the plugin itself
│   ├── Main.cs              entry point; implements IPlugin, IContextMenu, ISettingProvider
│   ├── ZedDatabase.cs       read-only SQLite access to Zed's workspace history
│   ├── ZedLauncher.cs       starts the `zed` CLI, reveals folders in File Explorer
│   ├── ZedProject.cs        result record (root path(s), last-opened time)
│   ├── TextMatcher.cs       fuzzy multi-term name/path matcher
│   ├── RelativeTime.cs      "3 hours ago" style formatting
│   ├── plugin.json          PowerToys plugin manifest (ID, ActionKeyword, ExecuteFileName)
│   └── Images/              plugin icon (icon.png)
└── tools/
    ├── plugin-smoke/        console harness to exercise Query/context menus outside PowerToys
    ├── zed-db-dump/         prints the workspaces table schema + sample rows
    └── build-deploy.ps1     one-command build + deploy: builds, copies/junctions to the PowerToys plugins folder, optional restart
```

### Architecture

- The plugin is a third-party PowerToys Run plugin (same hosting model as Wox): `Main` implements `Wox.Plugin.IPlugin` (`Init`/`Query`), `IContextMenu` (`LoadContextMenus`), and `ISettingProvider` (`AdditionalOptions`, `UpdateSettings`).
- `plugin.json` drives discovery: `ExecuteFileName` must be `ZedRecentProjects.dll`, and the `ID` must match `Main.PluginID`. `ActionKeyword: "zed"` plus `IsGlobal: true` means results appear for *any* query, which is why `Main.Query` also handles a free-text/empty search.
- Each `Query` re-reads the settings (`ReadMaxResults`, `ReadOptionBool`) and reloads Zed's database (`LoadRecentProjects(maxResults * 4)`), so settings changes take effect immediately without a PowerToys restart.
- Native SQLite caveat: SQLitePCLRaw resolves `e_sqlite3.dll` by name from the host's process search paths, which do not include the plugin folder. `ZedDatabase.EnsureSqliteReady` therefore pre-loads it from the plugin's own directory.
- The database is opened with `Mode=ReadOnly`. Read problems deliberately degrade to an empty result list, so an unexpected Zed schema change surfaces as "no results" rather than an error dialog.

### Iterating on a change

1. Build a Debug x64 copy:

   ```powershell
   dotnet build src\ZedRecentProjects\ZedRecentProjects.csproj -c Debug -p:Platform=x64 -r win-x64
   ```

2. Copy the whole output folder to the PowerToys plugin directory:

   ```
   src\ZedRecentProjects\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\
       →  %LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\ZedRecentProjects\
   ```

   The output folder already contains `ZedRecentProjects.dll`, `plugin.json`, `Images\`, and all dependencies.

3. Fully restart PowerToys (quit from the tray icon, then start it again).
4. Open PowerToys Run (`Alt+Space`) and type `zed ...` to verify.

If you are only touching `TextMatcher`, `ZedDatabase`, or `RelativeTime` logic, `tools/plugin-smoke` (see [Debugging](#debugging)) lets you verify much faster without restarting PowerToys.

### One-command build & deploy

`tools/build-deploy.ps1` wraps build + deploy (+ optional restart) into a single command:

```powershell
# build Debug x64, copy to the plugins folder, remind to restart
powershell tools\build-deploy.ps1

# rebuild and let the script restart PowerToys for you
powershell tools\build-deploy.ps1 -RestartPowerToys

# use a directory junction instead of copying: future builds auto-deploy
powershell tools\build-deploy.ps1 -Link
```

- `-Configuration Debug|Release`, `-Platform x64|ARM64` (defaults: `Debug`, `x64`).
- `-NoBuild` — deploy whatever is already built.
- `-Link` — replaces the plugins folder with a directory junction pointing at the build output folder, so every future `dotnet build` is already "deployed". Re-run to re-point it.
- `-RestartPowerToys` — quits PowerToys before copying (releases locked DLLs) and relaunches it afterwards.

> PowerShell 7 (`pwsh`) is recommended; the junction code also works in the built-in Windows PowerShell.

### Adding a setting

1. Add an entry to the `OptionDefinitions` array in `Main.cs`: a `Key`, the control type (`Numberbox` or `Checkbox`), a display label/description, and defaults (`NumberValue`, `NumberBoxMin`/`NumberBoxMax`, or `Value`).
2. Read it in `Query` with e.g. `ReadOptionBool("my_key", true)` (extend `ReadOption`/`ReadMaxResults` if you need a new option type).
3. PowerToys renders the option automatically via `ISettingProvider` — no `plugin.json` change is needed.
4. Note: PowerToys persists whatever the user saved; changing a default in code only affects options that have not been saved yet.

### Changing the icon

The plugin uses two icons, one per surface:

- `src\ZedRecentProjects\Images\icon.png` — colored icon shown in the PowerToys Run result list (referenced by `Main.cs`).
- `src\ZedRecentProjects\Images\plain-icon.png` — monochrome icon shown in the PowerToys Settings plugin list (referenced by `plugin.json`). Keep the shape as a black silhouette on a transparent background; PowerToys renders it as a single-color silhouette per theme (`BitmapIcon`).

Replace a file directly; keep the name and `.png` format so no code changes are needed.

## Debugging

### plugin-smoke

`tools/plugin-smoke` runs the plugin inside a minimal console host, so you can exercise `Query`, the text matcher, and DB parsing without touching PowerToys:

```powershell
dotnet run --project tools\plugin-smoke\plugin-smoke.csproj -p:Platform=x64 -r win-x64
```

Prerequisites: the .NET 9 SDK, PowerToys installed (the harness resolves PowerToys' helper assemblies from a running `PowerToys` process or the standard install directories), and a real Zed database under `%LOCALAPPDATA%\Zed\db\`.

It prints:

- the result of `ZedDatabase.LoadRecentProjects(20)` (with relative times)
- `TextMatcher.TryScore` checks for a few sample queries
- full `Query` runs through a stub host (`StubApi` implements `IPublicAPI`) for `"vibe"`, `""`, and `"zed vo"`, plus the context-menu titles on the first result

Use it while iterating on search/DB logic — it is much faster than rebuild + restart PowerToys.

### zed-db-dump

```powershell
dotnet run --project tools\zed-db-dump\zed-db-dump.csproj
```

Opens Zed's database and prints the `workspaces` table schema (`PRAGMA table_info`), the row count, and a few sample rows (BLOB columns shown as hex + UTF-8 text).

Use it when a Zed update seems to have broken the plugin (the plugin returns no results): dump the schema first and compare it against the SQL in `ZedDatabase.QueryProjects` (`SELECT paths, timestamp FROM workspaces ...`), then adjust the query if a column changed.

> Note: this tool hardcodes the `0-stable` channel path (`%LOCALAPPDATA%\Zed\db\0-stable\db.sqlite`), while the plugin also accepts other channels (e.g. `0-preview`). If Zed is on a non-stable channel, point the tool at your channel or open the DB with a SQLite browser.

### Manual end-to-end test

After installing per [Install](#install), verify:

- typing a partial project name or path returns matching projects
- `Enter` opens the folder in Zed
- the right-click context menu offers **Open in Zed**, **Reveal in File Explorer**, and **Copy path**
- the [Settings](#settings) (max results, new window, relative time) take effect without restarting PowerToys
- both global mode (any query) and scoped mode (typing `zed` first) work

### Troubleshooting

| Symptom | Cause & fix |
|---|---|
| No results | The database is missing or the schema changed. Run `zed-db-dump` (above) to inspect. The plugin intentionally swallows DB errors and returns an empty list, so this is the expected failure mode — there is no error dialog. |
| "Could not launch Zed" toast | The `zed` CLI was not found. The plugin probes `zed` on PATH, then `%LOCALAPPDATA%\Programs\Zed\Zed.exe`, `%LOCALAPPDATA%\Programs\Zed\zed.exe`, `%LOCALAPPDATA%\Zed\zed.exe`, and `%APPDATA%\Zed\zed.exe`. Ensure Zed is installed, or add its folder to PATH. |
| Plugin does not load in PowerToys | Confirm the plugin folder contains both `ZedRecentProjects.dll` and `plugin.json`, then fully restart PowerToys (tray → Quit → start again). |
| Settings seem ignored | PowerToys caches saved plugin settings. If you changed a default in `OptionDefinitions`, reset the saved value on the plugin's PowerToys settings page. |

## License

[MIT](LICENSE)
