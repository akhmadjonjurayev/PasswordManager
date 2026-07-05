# ===================================================================
#  SecureVault - Publisher & Installer Builder
#  Ishlatish : .\publish.ps1
#  Natija    : publish\SecureVault_Setup.exe
# ===================================================================

$ErrorActionPreference = "Stop"

$Root        = $PSScriptRoot
$PublishDir  = Join-Path $Root "publish"
$AppFilesDir = Join-Path $PublishDir "_app_files"
$IssFile     = Join-Path $PublishDir "installer.iss"
$OutputExe   = Join-Path $PublishDir "SecureVault_Setup.exe"

Write-Host ""
Write-Host "  ================================================" -ForegroundColor Cyan
Write-Host "           SecureVault Publisher                  " -ForegroundColor Cyan
Write-Host "  ================================================" -ForegroundColor Cyan
Write-Host ""

# --- 1. dotnet publish (self-contained, win-x64) ----------------------------
Write-Host "[1/3] dotnet publish..." -ForegroundColor Yellow

if (Test-Path $AppFilesDir) {
    Remove-Item $AppFilesDir -Recurse -Force
}

dotnet publish "$Root\PasswordManager.csproj" `
    --framework net10.0-windows10.0.19041.0 `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $AppFilesDir `
    --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "  XATO: dotnet publish muvaffaqiyatsiz tugadi." -ForegroundColor Red
    exit 1
}

Write-Host "  OK -> $AppFilesDir" -ForegroundColor Green
Write-Host ""

# --- 2. Inno Setup topish --------------------------------------------------
Write-Host "[2/3] Inno Setup qidirilmoqda..." -ForegroundColor Yellow

$CandidatePaths = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 5\ISCC.exe"
)

$IsccPath = $null
foreach ($p in $CandidatePaths) {
    if (Test-Path $p) {
        $IsccPath = $p
        break
    }
}

if (-not $IsccPath) {
    Write-Host ""
    Write-Host "  ESLATMA: Inno Setup o'rnatilmagan." -ForegroundColor Yellow
    Write-Host "  Installer yaratish uchun quyidagi manzildan" -ForegroundColor Yellow
    Write-Host "  yuklab o'rnating: https://jrsoftware.org/isdl.php" -ForegroundColor Cyan
    Write-Host "  So'ng bu skriptni qayta ishga tushiring." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Ilova fayllari saqlanib qoldi:" -ForegroundColor Gray
    Write-Host "  $AppFilesDir" -ForegroundColor White
    Write-Host ""
    exit 0
}

Write-Host "  OK -> $IsccPath" -ForegroundColor Green
Write-Host ""

# --- 3. Installer yaratish -------------------------------------------------
Write-Host "[3/3] Installer yaratilmoqda..." -ForegroundColor Yellow

& $IsccPath "/DAppSourceDir=$AppFilesDir" $IssFile

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "  XATO: Installer yaratilmadi (Inno Setup xatosi)." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "  ================================================" -ForegroundColor Green
Write-Host "              MUVAFFAQIYATLI TUGADI!              " -ForegroundColor Green
Write-Host "  ================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Installer: $OutputExe" -ForegroundColor Cyan
Write-Host ""
