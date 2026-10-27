# NuGet Application Instructions for GetHub

## Overview

GetHub is a Windows Forms desktop application targeting .NET Framework 4.8. As noted in the README, this project has **no external NuGet packages** — all dependencies are standard .NET Framework assemblies referenced directly in the project file.

This document outlines how to build, package, and distribute GetHub. While it's not a library intended for consumption via `nuget install`, it can be packaged as a NuGet package if needed for internal distribution.

## Prerequisites

| Requirement | Purpose |
|-------------|---------|
| Windows 10 or 11 | Target platform |
| .NET Framework 4.8 Developer Pack | Required to compile from source |
| PowerShell 5.1 | Required to run build/packaging scripts |
| Visual Studio 2022 (Optional) | For editing/debugging via `GetHub.sln` |

## Building the Application

### Using PowerShell (Recommended)

```powershell
# From the repository root
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release
```

The compiled executable will be output to `build\GetHub.exe`.

### Using Visual Studio

1. Open `GetHub.sln` in Visual Studio 2022
2. Select `Release | Any CPU`
3. Build the solution (`Ctrl+Shift+B`) or run (`F5`)

> Note: Close any running instances of GetHub before building, as Windows may lock the output file.

## Packaging Options

### Option 1: Custom Installer (Recommended for End Users)

This repository includes a custom installer implementation. This is the recommended way to distribute GetHub.

```powershell
# From the repository root
powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1 -Configuration Release
```

This will:
1. Build the application via `build.ps1`
2. Compile the uninstaller (`Uninstall.exe`)
3. Package all files into a compressed payload
4. Produce a single-file installer at `dist\GetHubSetup.exe`

For silent installation, see the [README](README.md#option-1--install-the-ready-made-setup-recommended) for available switches.

### Option 2: Creating a NuGet Package

Since GetHub is a `WinExe` (desktop application), it is not meant to be consumed as a class library. However, you can package the build output as a NuGet package for internal distribution using a `.nuspec` file.

#### Create a .nuspec file

Create `GetHub.nuspec` in the repository root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
  <metadata>
    <id>GetHub</id>
    <version>1.0.0</version>
    <authors>Abdelwahab-Ayman</authors>
    <owners>Abdelwahab-Ayman</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Offline Git repository browser for Windows.</description>
    <tags>git github windows winforms offline</tags>
  </metadata>
  <files>
    <file src="build\**\*.*" target="tools" />
  </files>
</package>
```

#### Pack the package

Using the NuGet CLI:

```powershell
nuget pack GetHub.nuspec
```

This will produce `GetHub.1.0.0.nupkg`. The application files will be placed in the `tools/` directory of the package.

#### Installing from a NuGet package

- **Local feed**: `nuget install GetHub -Source C:\path\to\feed`
- **NuGet Server**: Push with `nuget push GetHub.1.0.0.nupkg -Source <feed-url> -ApiKey <key>`

After installation, run `tools\GetHub.exe` from the packages directory.

> Note: This distribution method does not create Start Menu shortcuts, file associations, or handle uninstallation like the custom installer does. For end-user deployment, Option 1 is strongly preferred.

## Project Structure Reference

| Path | Purpose |
|------|---------|
| `src\GetHub\` | Application source code (WinForms, C# 5, .NET Framework 4.8) |
| `build.ps1` | Builds the application |
| `package.ps1` | Builds the custom installer |
| `installer\` | Setup and uninstaller source code |
| `build\` | Compiled application output |
| `dist\` | Compiled installer output |

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Missing .NET Framework 4.8 reference assemblies | Install the .NET Framework 4.8 Developer Pack from Microsoft. |
| File-in-use errors during build | Close all running instances of GetHub before rebuilding. |
| PowerShell script execution blocked | Run with `-ExecutionPolicy Bypass` as shown in the examples above. |
| NuGet pack fails | Ensure `nuget.exe` is installed and in your PATH, or use `dotnet` CLI where applicable. |

## Additional Information

For more details about usage, configuration, and data storage locations, refer to the [README](README.md).
