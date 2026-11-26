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
| 1 | Core bootstrapper: link handling, download/update, launch | ✅ done |
| 2 | Custom UI + performance tweaks | ✅ done |
| 3 | FastFlag editor with presets | ✅ this PR |
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

## How you use it

It replaces the official Roblox launcher, so it behaves like it:

- **Opening the launcher** (Start menu "RobloxLauncher", or the .exe) shows a small launch window, updates Roblox if
  needed and opens the Roblox app. **Clicking Play on roblox.com** does the same, then joins that game.
- **Settings** open from the gear on the launch window, the Start menu "RobloxLauncher Settings" shortcut, or
  `RobloxLauncher.exe --settings`. Changes save immediately and apply the next time Roblox launches.
  - *Performance:*
    - *High CPU priority*: sets Roblox's process priority to High.
    - *Use dedicated GPU*: the same per-app setting as Windows Settings › Display › Graphics (High performance).
    - *Disable fullscreen optimizations*: the same checkbox as the .exe's Properties › Compatibility tab.
  - *FastFlags:* presets (Competitive, Balanced, Showcase), one-click options (graphics API, render quality,
    texture quality, anti-aliasing, grass, distant detail), switches (exclusive fullscreen with Alt+Enter, disable
    DPI scaling, gray sky, still grass) and a raw JSON editor. Since Sept 2025 Roblox only honors
    [allowlisted flags](https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569);
    the editor warns about any others. The list lives in `FastFlags/FastFlagAllowlist.cs`, and the options and
    presets in `FastFlags/FastFlagCatalog.cs`. Flags are written to `ClientSettings\ClientAppSettings.json` in
    the Roblox install on every launch, so they survive updates.
  - *General:* re-register or give back the Roblox links, open the logs folder, reinstall Roblox.

The GPU and fullscreen switches are stored by Windows per .exe path, so they're moved to each new Roblox build
  and removed from old ones. Only entries under the launcher's own `Versions` folder are ever touched.
- **Settings:** re-register or give back the Roblox links, open the logs folder, reinstall Roblox.

The look (colours, corner radii, fonts, button styles) is all in `src/RobloxLauncher/Themes/Theme.xaml`.
Fonts are Inter and Space Grotesk, bundled under the SIL Open Font License (see `src/RobloxLauncher/Fonts/`).

Files live in `%LocalAppData%\RobloxLauncher\`:

```
Versions\version-xxxx\   the installed Roblox build
Downloads\               package cache (by MD5)
Logs\                    last 10 runs, check here first when something breaks
state.json               which build is installed
settings.json            your Performance switches
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
  This puts a ~1.4 MB `RobloxLauncher.exe` (mostly the bundled fonts) in `publish\`. Put it somewhere permanent (e.g. `%LocalAppData%\Programs\RobloxLauncher\`)
  and run it once. Links and Start menu shortcuts are pointed at wherever the .exe is, each time it runs.

### Commands

| Command | Does |
| --- | --- |
| `RobloxLauncher.exe` | Update if needed, open the Roblox app |
| `RobloxLauncher.exe --settings` | Open the settings window |
| `RobloxLauncher.exe roblox-player:...` | Update if needed, join that game (this is what the website calls) |
| `RobloxLauncher.exe --register` | Only take over `roblox-player:` / `roblox:` links and add Start menu shortcuts |
| `RobloxLauncher.exe --unregister` | Give the links back to the official Roblox launcher, remove the shortcuts |

## Project layout

```
src/RobloxLauncher.Core/   platform-neutral logic, fully unit tested
  Deployment/              version API, manifest parsing, CDN downloads, package map
  Install/                 installer, safe unzip, install state
  Launch/                  command-line parsing, starting Roblox
  Bootstrapper.cs          check → install → return folder to launch
  FastFlags/               allowlist, options/presets, ClientAppSettings.json read/write
  Settings/                settings.json load/save
  Tweaks/                  performance tweaks (registry logic behind an interface, so it's testable)
src/RobloxLauncher/        WPF app
  Themes/Theme.xaml        colours, fonts, button/switch/nav styles
  ProgressWindow.xaml      the launch window (update → start Roblox), with a settings gear
  SettingsWindow.xaml      Performance / General settings
  Shortcuts.cs             Start menu shortcuts
  LaunchFlow.cs            update → apply tweaks → start Roblox
  ProtocolRegistrar.cs     roblox-player: link handler
  RegistryValues.cs        real HKCU registry access for the tweaks
tests/                     xUnit tests against a fake CDN (no network)
```

## Known limits

- Only the `production` (LIVE) channel. Roblox beta channels aren't supported yet.
- Mods and Servers pages are placeholders until their steps.
- FastFlag values for quality levels come from community testing; Roblox doesn't document them.
- If Roblox adds a new package to its builds, installs stop with a clear error until it's added to `PackageMap.cs`.
  The test `Every_package_in_real_manifest_is_mapped_or_ignored` checks this against a saved real manifest.
