# 배포용 압축 파일을 만든다: 빌드 → Release\BluffAuction-<버전>-win64.zip (안에 BluffAuction-<버전> 폴더 하나)
# 사용: powershell -ExecutionPolicy Bypass -File Tools\package.ps1 [-NoBuild]
param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "..")
if (-not $NoBuild) {
    & powershell -ExecutionPolicy Bypass -File (Join-Path $root "Tools\build.ps1")
    if ($LASTEXITCODE -ne 0) { throw "build failed" }
}
$setup = Get-Content -Raw -Encoding UTF8 (Join-Path $root "Assets\BluffAuction\Scripts\Editor\ProjectSetup.cs")
$ver = [regex]::Match($setup, 'Version = "([^"]+)"').Groups[1].Value
$build = Join-Path $root "Builds\Windows"
if (-not (Test-Path (Join-Path $build "BluffAuction.exe"))) { throw "no build at $build" }
# 배포하면 안 되는 것이 섞이지 않았는지
$bad = Get-ChildItem $build -Directory | Where-Object { $_.Name -like "*DoNotShip*" }
if ($bad) { throw "DoNotShip folder in build: $($bad.Name)" }

$out = Join-Path $root "Release"
New-Item -ItemType Directory -Force $out | Out-Null
$name = "BluffAuction-$ver-win64"
$stage = Join-Path $env:TEMP "bluff-package\$name"
if (Test-Path (Split-Path $stage)) { Remove-Item -Recurse -Force (Split-Path $stage) }
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item -Recurse -Force (Join-Path $build "*") $stage
$zip = Join-Path $out "$name.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $true)
Remove-Item -Recurse -Force (Split-Path $stage)
$size = (Get-Item $zip).Length / 1MB
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
Write-Host ("{0}  {1:N1} MB  SHA256 {2}" -f $zip, $size, $hash)
