[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$installerDir = Join-Path $root "installer"
$buildDir = Join-Path $root "build"
$distDir = Join-Path $root "dist"
$objDir = Join-Path $installerDir "obj"
$stageDir = Join-Path $objDir "stage"
$payloadPath = Join-Path $objDir "payload.zip"
$setupExe = Join-Path $distDir "GetHubSetup.exe"
$iconPath = Join-Path $root "src\GetHub\Resources\GetHub.ico"
$logoPath = Join-Path $root "src\GetHub\Resources\GetHub.png"
$manifestPath = Join-Path $installerDir "Installer.manifest"

if (-not (Test-Path -LiteralPath $installerDir)) {
    throw "Installer folder not found: $installerDir"
}

Write-Host "Building the application first..." -ForegroundColor Cyan
& (Join-Path $root "build.ps1") -Configuration $Configuration

$appExe = Join-Path $buildDir "GetHub.exe"
$appConfig = Join-Path $buildDir "GetHub.exe.config"
if (-not (Test-Path -LiteralPath $appExe)) {
    throw "Application build not found: $appExe"
}

if (-not (Test-Path -LiteralPath $appConfig)) {
    throw "Application configuration not found: $appConfig"
}

$cscCandidates = @(
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)

$csc = $cscCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $csc) {
    throw "Could not find the .NET Framework C# compiler (csc.exe)."
}

$referenceCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} "Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8"),
    (Join-Path $env:ProgramFiles "Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8")
)

$referenceDir = $referenceCandidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
if (-not $referenceDir) {
    throw "Could not find the .NET Framework 4.8 reference assemblies."
}

$referenceNames = @(
    "mscorlib.dll",
    "System.dll",
    "System.Core.dll",
    "System.Drawing.dll",
    "System.IO.Compression.dll",
    "System.Windows.Forms.dll"
)

$references = @()
foreach ($name in $referenceNames) {
    $path = Join-Path $referenceDir $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing reference assembly: $path"
    }

    $references += "/reference:$path"
}

$compilerArgs = @(
    "/nologo",
    "/noconfig",
    "/nostdlib+",
    "/target:winexe",
    "/platform:anycpu",
    "/langversion:5",
    "/optimize+",
    "/debug-",
    "/win32icon:$iconPath",
    "/win32manifest:$manifestPath"
)

# The uninstaller is a separate, very small executable. Setup carries a copy and
# writes it next to the app, where Settings > Apps expects to find it.
$uninstallerSources = @(
    "InstallerInfo.cs",
    "InstallManifest.cs",
    "InstallerTheme.cs",
    "Shortcut.cs",
    "RunningApp.cs",
    "UninstallEngine.cs",
    "UninstallForm.cs",
    "UninstallerProgram.cs",
    "UninstallerAssemblyInfo.cs"
) | ForEach-Object { Join-Path $installerDir $_ }

$setupSources = @(
    "InstallerInfo.cs",
    "InstallManifest.cs",
    "InstallerTheme.cs",
    "Shortcut.cs",
    "RunningApp.cs",
    "InstallEngine.cs",
    "CheckBadge.cs",
    "SetupForm.cs",
    "SetupProgram.cs",
    "SetupAssemblyInfo.cs"
) | ForEach-Object { Join-Path $installerDir $_ }

foreach ($source in ($uninstallerSources + $setupSources)) {
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Installer source not found: $source"
    }
}

if (Test-Path -LiteralPath $objDir) {
    Remove-Item -LiteralPath $objDir -Recurse -Force
}

New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
New-Item -ItemType Directory -Path $distDir -Force | Out-Null

Copy-Item -LiteralPath $appExe -Destination (Join-Path $stageDir "GetHub.exe") -Force
Copy-Item -LiteralPath $appConfig -Destination (Join-Path $stageDir "GetHub.exe.config") -Force

$uninstallerExe = Join-Path $stageDir "Uninstall.exe"
Write-Host "Compiling the uninstaller..." -ForegroundColor Cyan
& $csc @compilerArgs "/out:$uninstallerExe" $references $uninstallerSources
if ($LASTEXITCODE -ne 0) {
    throw "Uninstaller build failed with exit code $LASTEXITCODE."
}

# One compressed archive holds every installed file. The setup executable embeds
# this archive, which is what makes the download a single file.
Write-Host "Compressing the install payload..." -ForegroundColor Cyan
Add-Type -AssemblyName System.IO.Compression

if (Test-Path -LiteralPath $payloadPath) {
    Remove-Item -LiteralPath $payloadPath -Force
}

$payloadStream = [System.IO.File]::Create($payloadPath)
try {
    $archive = New-Object System.IO.Compression.ZipArchive(
        $payloadStream,
        [System.IO.Compression.ZipArchiveMode]::Create,
        $true)
    try {
        foreach ($file in (Get-ChildItem -LiteralPath $stageDir -File | Sort-Object Name)) {
            $entry = $archive.CreateEntry(
                $file.Name,
                [System.IO.Compression.CompressionLevel]::Optimal)
            $entryStream = $entry.Open()
            try {
                $sourceStream = [System.IO.File]::OpenRead($file.FullName)
                try {
                    $sourceStream.CopyTo($entryStream)
                }
                finally {
                    $sourceStream.Dispose()
                }
            }
            finally {
                $entryStream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    $payloadStream.Dispose()
}

if (Test-Path -LiteralPath $setupExe) {
    Remove-Item -LiteralPath $setupExe -Force
}

Write-Host "Compiling GetHub Setup..." -ForegroundColor Cyan
& $csc @compilerArgs `
    "/out:$setupExe" `
    "/resource:$payloadPath,GetHub.Installer.payload.zip" `
    "/resource:$logoPath,GetHub.Installer.Logo.png" `
    $references `
    $setupSources
if ($LASTEXITCODE -ne 0) {
    throw "Setup build failed with exit code $LASTEXITCODE."
}

$setup = Get-Item -LiteralPath $setupExe
$payload = Get-Item -LiteralPath $payloadPath
$app = Get-Item -LiteralPath $appExe
$hash = (Get-FileHash -LiteralPath $setupExe -Algorithm SHA256).Hash

Write-Host ""
Write-Host "Package: $($setup.FullName)" -ForegroundColor Green
Write-Host ("  setup size : {0:N0} KB" -f ($setup.Length / 1KB))
Write-Host ("  payload    : {0:N0} KB (GetHub.exe {1:N0} KB + uninstaller)" -f ($payload.Length / 1KB), ($app.Length / 1KB))
Write-Host "  version    : 1.0.0"
Write-Host "  sha256     : $hash"
