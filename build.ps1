<#
    build.ps1 - Build the Spell Sword mod and deploy it into Blade & Sorcery.

    Usage:
        ./build.ps1            # build (Release) and copy into the game's Mods folder
        ./build.ps1 -NoDeploy  # just build, don't copy
#>
param(
    [switch]$NoDeploy,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---- 1. Build the DLL ----------------------------------------------------
# Find MSBuild via Visual Studio (no separate .NET SDK required).
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) { throw "vswhere.exe not found - is Visual Studio installed?" }
$msbuild = & $vswhere -products * -latest -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild.exe not found via vswhere." }

Write-Host "Building SpellSword.csproj ($Configuration) with:" -ForegroundColor Cyan
Write-Host "  $msbuild"
& $msbuild "$root\SpellSword.csproj" /t:Build /p:Configuration=$Configuration /nologo /v:minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$dll = Join-Path $root "bin\$Configuration\SpellSword.dll"
if (-not (Test-Path $dll)) { throw "Expected output not found: $dll" }
Write-Host "Built: $dll" -ForegroundColor Green

if ($NoDeploy) { return }

# ---- 2. Deploy into the game --------------------------------------------
$modsRoot = "S:\game_installations\SteamLibrary\steamapps\common\Blade & Sorcery\BladeAndSorcery_Data\StreamingAssets\Mods"
$dest = Join-Path $modsRoot "SpellSword"

if (-not (Test-Path $modsRoot)) { throw "Mods folder not found: $modsRoot" }
if (-not (Test-Path $dest)) { New-Item -ItemType Directory -Path $dest | Out-Null }

Copy-Item $dll -Destination $dest -Force
Copy-Item (Join-Path $root "ModFiles\manifest.json") -Destination $dest -Force

Write-Host "Deployed to: $dest" -ForegroundColor Green
Write-Host "Restart Blade & Sorcery (or reload mods) to pick up changes." -ForegroundColor Yellow
