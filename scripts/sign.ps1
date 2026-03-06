# ============================================================
# ZenithFiler コード署名スクリプト（自己署名証明書）
# ============================================================
# 使用方法:
#   1. 初回のみ: .\scripts\sign.ps1 -CreateCert
#   2. 署名:     .\scripts\sign.ps1 -BinDir .\bin\Release\net8.0-windows\win-x64\publish
# ============================================================

param(
    [switch]$CreateCert,
    [string]$BinDir = "",
    [string]$CertSubject = "CN=K.AKASAKA, O=ZenithFiler, L=Tokyo, C=JP",
    [string]$PfxPath = "$PSScriptRoot\..\certs\ZenithFiler.pfx",
    [string]$Thumbprint = ""
)

$ErrorActionPreference = "Stop"

# ── 証明書の作成（初回のみ） ──
if ($CreateCert) {
    $certDir = Split-Path $PfxPath -Parent
    if (-not (Test-Path $certDir)) {
        New-Item -ItemType Directory -Path $certDir -Force | Out-Null
    }

    Write-Host "[1/3] 自己署名コード署名証明書を作成中..." -ForegroundColor Cyan
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $CertSubject `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -NotAfter (Get-Date).AddYears(5) `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256

    Write-Host "[2/3] PFX にエクスポート中..." -ForegroundColor Cyan
    $password = Read-Host -AsSecureString -Prompt "PFX パスワードを入力してください"
    Export-PfxCertificate -Cert $cert -FilePath $PfxPath -Password $password | Out-Null

    Write-Host "[3/3] 完了" -ForegroundColor Green
    Write-Host "  Thumbprint : $($cert.Thumbprint)"
    Write-Host "  PFX        : $PfxPath"
    Write-Host ""
    Write-Host "署名時に使用: .\scripts\sign.ps1 -BinDir <出力パス> -Thumbprint $($cert.Thumbprint)" -ForegroundColor Yellow
    exit 0
}

# ── 署名 ──
if (-not $BinDir) {
    Write-Error "署名対象のディレクトリを -BinDir で指定してください。"
    exit 1
}

if (-not (Test-Path $BinDir)) {
    Write-Error "指定されたディレクトリが見つかりません: $BinDir"
    exit 1
}

# Thumbprint が指定されていない場合、PFX から取得を試みる
if (-not $Thumbprint) {
    if (Test-Path $PfxPath) {
        $password = Read-Host -AsSecureString -Prompt "PFX パスワードを入力してください"
        $pfxCert = Import-PfxCertificate -FilePath $PfxPath -CertStoreLocation "Cert:\CurrentUser\My" -Password $password
        $Thumbprint = $pfxCert.Thumbprint
        Write-Host "PFX から Thumbprint を取得: $Thumbprint" -ForegroundColor Cyan
    } else {
        Write-Error "Thumbprint を指定するか、PFX ファイルを配置してください: $PfxPath"
        exit 1
    }
}

$cert = Get-ChildItem "Cert:\CurrentUser\My\$Thumbprint" -ErrorAction SilentlyContinue
if (-not $cert) {
    Write-Error "証明書が見つかりません (Thumbprint: $Thumbprint)"
    exit 1
}

# 署名対象: EXE + アプリの DLL
$targets = Get-ChildItem $BinDir -Include "ZenithFiler.exe","ZenithFiler.dll" -Recurse

Write-Host "署名対象: $($targets.Count) ファイル" -ForegroundColor Cyan
foreach ($file in $targets) {
    Write-Host "  署名中: $($file.Name)..." -NoNewline
    Set-AuthenticodeSignature -FilePath $file.FullName -Certificate $cert -TimestampServer "http://timestamp.digicert.com" -HashAlgorithm SHA256 | Out-Null
    Write-Host " OK" -ForegroundColor Green
}

# ── 署名検証 ──
Write-Host ""
Write-Host "署名検証:" -ForegroundColor Cyan
foreach ($file in $targets) {
    $sig = Get-AuthenticodeSignature $file.FullName
    $status = $sig.Status
    $color = if ($status -eq "Valid") { "Green" } else { "Red" }
    Write-Host "  $($file.Name): $status" -ForegroundColor $color
}

Write-Host ""
Write-Host "署名完了" -ForegroundColor Green
