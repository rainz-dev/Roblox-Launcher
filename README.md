# RobloxLauncher

A custom Roblox bootstrapper for Windows, in the style of Bloxstrap. It replaces Roblox's own launcher: it handles
`roblox-player:` links from the website, keeps Roblox up to date from Roblox's official CDN, and starts it.

> **Name is a placeholder.** It lives in `AppInfo.Name` (and `AssemblyName` in `src/RobloxLauncher/RobloxLauncher.csproj`).
> If you ever publish this, pick a name without "Roblox" in it, since that's Roblox's trademark.

It only talks to Roblox's own servers and never touches the running game: no injection, no memory edits,
nothing that changes gameplay.

## Status

| Step | What | State |
| --- | --- | --- |
| 1 | Core bootstrapper: link handling, download/update, launch | ✅ this PR |
| 2 | Settings GUI + performance tweaks | planned |
| 3 | FastFlag editor with presets | planned |
| 4 | Mods (content-folder swaps) | planned |
| 5 | Discord rich presence, server region | planned |
| 6 | Multiple Roblox windows | planned |
| 7 | Server history/browser, playtime, companion apps, notifications | planned |

## How it works

1. Asks `clientsettings.roblox.com/v2/client-version/WindowsPlayer` for the latest build id (e.g. `version-2c6fd5e09aaf4cfa`).
2. If that build isn't installed, downloads `{build}-rbxPkgManifest.txt` from `setup.rbxcdn.com`, which lists ~20 zip
   packages with their MD5 hashes.
3. Downloads the packages (4 at a time, falling back to mirror CDNs on failure) and checks every MD5.
   Packages are cached by hash, so an update only downloads what actually changed.
4. Unpacks each zip to its folder (`PackageMap.cs`) in a staging folder, then swaps it in, so a failed or cancelled
   update never leaves a broken install.
5. Starts `RobloxPlayerBeta.exe` with the link you clicked, or `--app` for the home screen.

If you're offline, it launches the build you already have.

Files live in `%LocalAppData%\RobloxLauncher\`:

```
Versions\version-xxxx\   the installed Roblox build
Downloads\               package cache (by MD5)
Logs\                    last 10 runs, check here first when something breaks
state.json               which build is installed
```

## Build and run

You need Windows, the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), and Visual Studio 2026 or Rider.

- **From the IDE:** open `RobloxLauncher.slnx`, set `RobloxLauncher` as the startup project, run.
- **From a terminal:**
  ```
  dotnet test
  dotnet run --project src/RobloxLauncher
  ```
- **Make the .exe:**
  ```
  dotnet publish src/RobloxLauncher -p:PublishProfile=win-x64
  ```
  This puts a ~230 KB `RobloxLauncher.exe` in `publish\`. Put it somewhere permanent (e.g. `%LocalAppData%\Programs\RobloxLauncher\`)
  and run it once. Links are registered to wherever the .exe is, and re-registered each time it runs.

### Commands

| Command | Does |
| --- | --- |
| `RobloxLauncher.exe` | Update if needed, open Roblox |
| `RobloxLauncher.exe roblox-player:...` | Update if needed, join that game (this is what the website calls) |
| `RobloxLauncher.exe --register` | Only take over `roblox-player:` / `roblox:` links |
| `RobloxLauncher.exe --unregister` | Give the links back to the official Roblox launcher |

## Project layout

```
src/RobloxLauncher.Core/   platform-neutral logic, fully unit tested
  Deployment/              version API, manifest parsing, CDN downloads, package map
  Install/                 installer, safe unzip, install state
  Launch/                  command-line parsing, starting Roblox
  Bootstrapper.cs          check → install → return folder to launch
src/RobloxLauncher/        WPF app: progress window, registry link handler, wiring
tests/                     xUnit tests against a fake CDN (no network)
```

## Known limits (step 1)

- Only the `production` (LIVE) channel. Roblox beta channels aren't supported yet.
- No settings window yet; that's step 2.
- If Roblox adds a new package to its builds, installs stop with a clear error until it's added to `PackageMap.cs`.
  The test `Every_package_in_real_manifest_is_mapped_or_ignored` checks this against a saved real manifest.
