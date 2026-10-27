[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$projectDir = Join-Path $root "src\GetHub"
$outputDir = Join-Path $root "build"

if (-not (Test-Path -LiteralPath $projectDir)) {
    throw "Project folder not found: $projectDir"
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
    "System.IO.Compression.FileSystem.dll",
    "System.Net.Http.dll",
    "System.Web.Extensions.dll",
    "System.Windows.Forms.dll",
    "System.Xml.dll"
)

$references = @()
foreach ($name in $referenceNames) {
    $path = Join-Path $referenceDir $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing reference assembly: $path"
    }

    $references += "/reference:$path"
}

$sources = @(Get-ChildItem -LiteralPath $projectDir -Recurse -Filter "*.cs" | Sort-Object FullName | ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) {
    throw "No C# source files found in $projectDir"
}

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$outputExe = Join-Path $outputDir "GetHub.exe"

$compilerArgs = @(
    "/nologo",
    "/noconfig",
    "/nostdlib+",
    "/target:winexe",
    "/platform:anycpu",
    "/langversion:5",
    "/optimize+",
    "/out:$outputExe"
)

if ($Configuration -eq "Debug") {
    $compilerArgs += "/define:DEBUG;TRACE"
    $compilerArgs += "/debug+"
    $compilerArgs += "/optimize-"
}
else {
    $compilerArgs += "/define:TRACE"
    $compilerArgs += "/debug-"
}

$iconPath = Join-Path $projectDir "Resources\GetHub.ico"
$logoPath = Join-Path $projectDir "Resources\GetHub.png"
if (Test-Path -LiteralPath $iconPath) {
    $compilerArgs += "/win32icon:$iconPath"
    $compilerArgs += "/resource:$iconPath,GetHub.Resources.GetHub.ico"
}

if (Test-Path -LiteralPath $logoPath) {
    $compilerArgs += "/resource:$logoPath,GetHub.Resources.GetHub.png"
}

$compilerArgs += $references
$compilerArgs += $sources

Write-Host "Building GetHub ($Configuration)..." -ForegroundColor Cyan
& $csc @compilerArgs
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

Copy-Item -LiteralPath (Join-Path $projectDir "App.config") -Destination (Join-Path $outputDir "GetHub.exe.config") -Force

Write-Host "Build succeeded: $outputExe" -ForegroundColor Green
