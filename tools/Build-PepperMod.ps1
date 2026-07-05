param(
    [string]$Configuration = "Release",
    [string]$VintageStory = $env:VINTAGE_STORY,
    [switch]$Install
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$modInfoPath = Join-Path $root "modinfo.json"
$modInfo = Get-Content -Raw $modInfoPath | ConvertFrom-Json
$packagePath = Join-Path $root ("{0}-{1}.zip" -f $modInfo.modid, $modInfo.version)

if (-not $VintageStory) {
    $commonPaths = @(
        "E:\Vintagestory",
        "$env:APPDATA\Vintagestory",
        "$env:LOCALAPPDATA\Programs\Vintagestory",
        "C:\Program Files\Vintagestory"
    )

    $VintageStory = $commonPaths | Where-Object {
        Test-Path (Join-Path $_ "VintagestoryAPI.dll")
    } | Select-Object -First 1
}

if (-not $VintageStory -or -not (Test-Path (Join-Path $VintageStory "VintagestoryAPI.dll"))) {
    throw "Could not find VintagestoryAPI.dll. Set VINTAGE_STORY or pass -VintageStory."
}

$dotnetCandidates = @(
    "$env:USERPROFILE\.dotnet10\dotnet.exe",
    "$env:USERPROFILE\.dotnet\dotnet.exe",
    "dotnet"
)

$dotnet = $dotnetCandidates | Where-Object {
    if ($_ -eq "dotnet") {
        Get-Command dotnet -ErrorAction SilentlyContinue
    } else {
        Test-Path $_
    }
} | Select-Object -First 1

if (-not $dotnet) {
    throw "Could not find dotnet. Install the .NET SDK first."
}

$project = Join-Path $root "src\PepperMod.csproj"
$env:VINTAGE_STORY = $VintageStory
& $dotnet build $project -c $Configuration

$dllPath = Join-Path $root "src\bin\$Configuration\net10.0\$($modInfo.modid).dll"
if (-not (Test-Path $dllPath)) {
    throw "Build succeeded but did not produce $dllPath."
}

if (Test-Path $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($packagePath, [System.IO.Compression.ZipArchiveMode]::Create)

try {
    $rootFiles = @("modinfo.json", "modicon.png")
    foreach ($file in $rootFiles) {
        $path = Join-Path $root $file
        if (Test-Path $path) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $path, $file) | Out-Null
        }
    }

    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $dllPath, "$($modInfo.modid).dll") | Out-Null

    $assetsRoot = Join-Path $root "assets"
    Get-ChildItem $assetsRoot -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($root.Length + 1).Replace("\", "/")
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $relative) | Out-Null
    }
} finally {
    $zip.Dispose()
}

if ($Install) {
    $modsDir = Join-Path $env:APPDATA "VintagestoryData\Mods"
    New-Item -ItemType Directory -Force -Path $modsDir | Out-Null
    Copy-Item -LiteralPath $packagePath -Destination (Join-Path $modsDir (Split-Path -Leaf $packagePath)) -Force
}

Write-Host "Built $packagePath"
if ($Install) {
    Write-Host "Installed to $modsDir"
}
