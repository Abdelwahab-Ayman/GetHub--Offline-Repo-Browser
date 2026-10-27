# GetHub – Offline Repo Browser

GetHub is a Windows desktop application for browsing Git repositories without an internet connection. Open a local clone or a downloaded repository, explore files and folders, read code and rendered READMEs, and review commit history, branches, tags and diffs. It can also download public or private repositories from GitHub and read their issues and project boards inside the app.

Everything stays local. Repositories, settings and the GitHub token are stored under `%LocalAppData%\GetHub`.

---

## Requirements

| Requirement | Needed for |
|---|---|
| Windows 10 or 11 | Running GetHub |
| .NET Framework 4.8 | Running GetHub (included with Windows 10/11) |
| PowerShell 5.1 | Running `build.ps1` and `package.ps1` |
| .NET Framework 4.8 Developer Pack | Building from source (provides the 4.8 reference assemblies) |
| Visual Studio 2022 | Optional, for editing and debugging |

There are no NuGet packages, no administrator rights and no third-party installer tools. Setup installs per user into `%LocalAppData%\Programs\GetHub`.

---

## Quick start

### Option 1 – install the ready-made setup (recommended)

1. Download or clone this repository.
2. Run **`dist\GetHubSetup.exe`**.
3. Choose the install folder and shortcuts, then select **Install**.

GetHub is installed for the current Windows account and appears in **Settings → Apps**. The setup is not code-signed, so Windows may show *Windows protected your PC* on first run: choose **More info → Run anyway**.

Silent install (useful for scripts):

```powershell
.\dist\GetHubSetup.exe /silent /dir="D:\Apps\GetHub" /nodesktop /nostartmenu /nolaunch
```

| Switch | Meaning |
|---|---|
| `/silent` | No window, no prompts |
| `/dir="C:\path"` | Install folder (default: `%LocalAppData%\Programs\GetHub`) |
| `/nodesktop` | Do not create a desktop shortcut |
| `/nostartmenu` | Do not add a Start-menu shortcut |
| `/nolaunch` | Do not start GetHub when setup finishes |
| `/launch` | Start GetHub when a silent install finishes |

To uninstall, use **Settings → Apps → GetHub**, or run `Uninstall.exe /quiet` from the install folder. Your settings, sign-in and downloaded repositories are kept unless you tick the delete option in the uninstaller. `/removedata` also removes them.

### Option 2 – build and run from source

```powershell
git clone https://github.com/Abdelwahab-Ayman/GetHub--Offline-Repo-Browser.git
cd GetHub--Offline-Repo-Browser
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\build\GetHub.exe
```

If PowerShell blocks the script, the command above bypasses the policy for that process only. A Debug build is also available:

```powershell
.\build.ps1 -Configuration Debug
```

`build.ps1` compiles the app with the .NET Framework C# compiler (`csc.exe`) and the v4.8 reference assemblies, then writes `build\GetHub.exe` and `build\GetHub.exe.config`. Stop any running GetHub before rebuilding, otherwise Windows keeps the old file locked.

You can also open **`GetHub.sln`** in Visual Studio 2022, select **Release – Any CPU**, and press **F5**. Visual Studio needs the .NET Framework 4.8 targeting pack as well.

### Option 3 – build the single-file installer

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1
```

This builds the app, compiles the uninstaller, compresses the payload and writes **`dist\GetHubSetup.exe`**, printing its size and SHA-256. No Inno Setup, NSIS or WiX installation is required.

---

## Using GetHub

### Open a local repository
Drag a repository folder onto the GetHub window. GetHub reads its `.git` history when one is present and shows the last downloaded issues/projects if they exist.

### Download a repository from GitHub
1. Select **Add repository**.
2. Paste a URL such as `https://github.com/dotnet/runtime`, `owner/repository`, or a `git@github.com:...` address.
3. Optionally choose a branch; leave it blank for the default branch.
4. Select **Download**.

Public repositories work while signed out. Private repositories need a signed-in account.

### Sign in to GitHub
GetHub uses a personal access token instead of an OAuth app.

1. On GitHub, open **Settings → Developer settings → Personal access tokens → Tokens (classic) → Generate new token**.
2. Give it the **`repo`** and **`read:project`** scopes.
3. In GetHub, open **Settings → Sign in** and paste the token.

The token is checked against `api.github.com` before it is saved, stored only in `%LocalAppData%\GetHub\settings.json`, and can be removed with **Sign out** in Settings. The `read:project` scope is what lets the Projects tab load project boards.

### Issues and projects
Choose a repository, then select the **Issues** or **Projects** tab. GetHub keeps the last downloaded copy on disk, so lists and issue comments stay readable offline. Links that cannot be shown in the app open in your normal browser.

### Themes
Switch between dark and light under **Settings → Theme**. The change applies immediately without restarting.

---

## Where your data lives

| Path | Contents |
|---|---|
| `%LocalAppData%\GetHub\settings.json` | Theme and GitHub sign-in |
| `%LocalAppData%\GetHub\repos\` | Downloaded repositories, metadata, issues and projects |
| `%LocalAppData%\GetHub\render\` | Temporary HTML shown in the embedded browser |
| `%LocalAppData%\GetHub\temp\` | Download and extraction scratch space |
| `%LocalAppData%\Programs\GetHub\` | Default install folder (setup edition) |

The app registers IE11 emulation for its embedded browser under `HKCU\Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION`. The uninstaller removes it.

---

## Project layout

| Path | Purpose |
|---|---|
| `src\GetHub\` | Application source (WinForms, C# 5, .NET Framework 4.8) |
| `installer\` | Setup and uninstaller source |
| `build.ps1` | Builds `build\GetHub.exe` |
| `package.ps1` | Builds `dist\GetHubSetup.exe` |
| `build\` | Generated application binaries |
| `dist\` | Generated setup executable |
| `GetHub.sln` | Visual Studio solution |
| `snapshot\` | Local test repository used while developing (not part of the app) |

The repository intentionally commits the current `build\` and `dist\` binaries so the app can be tried without a build step. Re-run `build.ps1` and `package.ps1` to refresh them after changing the source.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| `Could not find the .NET Framework 4.8 reference assemblies` | Install the .NET Framework 4.8 Developer Pack, or add the targeting pack through Visual Studio Installer. |
| Build fails with `CS0016` or a file-in-use error | Close all GetHub windows, then build again. |
| Script cannot be loaded because running scripts is disabled | Use `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1`. |
| SmartScreen warns about `GetHubSetup.exe` | The setup is unsigned; choose **More info → Run anyway**. |
| Issues or Projects show a sign-in page | Sign in with a token that has the `repo` and `read:project` scopes. |
| Issues show an old copy | GetHub displays the cached copy when GitHub cannot be reached; connect to the network and refresh the tab. |
| Setup cannot write to `C:\Program Files` | Install to a folder in your user profile, such as the default location. |

---

## License

See [LICENSE](LICENSE).
