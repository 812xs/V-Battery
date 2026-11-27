<#
 V-Battery yayınlama betiği: self-contained, tek dosyalı win-x64 EXE üretir.
 Kullanım:  ./scripts/publish.ps1 [-Version 0.1.0] [-Configuration Release]
 Çıktı:     artifacts/publish/V-Battery.exe
#>
param(
    [string]$Version = "0.1.0",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "artifacts/publish"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet publish (Join-Path $root "src/VBattery.App/VBattery.App.csproj") `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:Version=$Version `
    -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish başarısız oldu." }

Write-Host "Hazır: $out\V-Battery.exe"
