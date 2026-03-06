# pack.ps1 — ZenithFiler リリース ZIP 作成スクリプト
# Usage: .\scripts\pack.ps1
# 出力: releases\ZenithFiler_v{version}.zip

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

# プロジェクトルートを取得
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path "$projectRoot\ZenithFiler.csproj")) {
    $projectRoot = Split-Path -Parent $PSScriptRoot
}
if (-not (Test-Path "$projectRoot\ZenithFiler.csproj")) {
    $projectRoot = $PSScriptRoot | Split-Path
}

Push-Location $projectRoot
try {
    # バージョンを csproj から抽出
    [xml]$csproj = Get-Content "ZenithFiler.csproj"
    $version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if (-not $version) {
        Write-Error "Version not found in ZenithFiler.csproj"
        exit 1
    }
    Write-Host "Building ZenithFiler v$version ($Configuration, $Runtime)..." -ForegroundColor Cyan

    # publish
    $publishDir = "publish"
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    dotnet publish -c $Configuration -r $Runtime --self-contained -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed"
        exit 1
    }

    # ZIP 作成
    $releasesDir = "releases"
    if (-not (Test-Path $releasesDir)) { New-Item -ItemType Directory -Path $releasesDir | Out-Null }
    $zipName = "ZenithFiler_v$version.zip"
    $zipPath = Join-Path $releasesDir $zipName
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

    Write-Host "Creating $zipName..." -ForegroundColor Cyan
    Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force

    $size = (Get-Item $zipPath).Length / 1MB
    Write-Host ("Done: $zipPath ({0:N1} MB)" -f $size) -ForegroundColor Green
    Write-Host "Upload this file to GitHub Releases." -ForegroundColor Yellow
}
finally {
    Pop-Location
}
