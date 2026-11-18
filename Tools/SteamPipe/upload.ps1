# Steam 에 빌드를 올린다 (SteamPipe). 앱을 등록하고 Steamworks SDK 의 steamcmd 를 받은 뒤에 쓴다.
# 사용: powershell -ExecutionPolicy Bypass -File Tools\SteamPipe\upload.ps1 -AppId 1234560 -DepotId 1234561 -SteamCmd C:\steamworks\tools\ContentBuilder\builder\steamcmd.exe -User <Steamworks 계정>
#  - 먼저 Tools\build.ps1 로 빌드하고, Platform/Steam.cs 의 AppId 도 같은 번호로 바꿔 두어야 한다.
#  - 비밀번호·Steam Guard 코드는 steamcmd 가 직접 묻는다 (이 스크립트는 저장하지 않는다).
#  - 올린 뒤 Steamworks 웹 → SteamPipe → Builds 에서 확인하고 기본 브랜치에 건다.
param(
    [Parameter(Mandatory = $true)][string]$AppId,
    [Parameter(Mandatory = $true)][string]$DepotId,
    [Parameter(Mandatory = $true)][string]$SteamCmd,
    [Parameter(Mandatory = $true)][string]$User
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "..\..")
$steamCs = Get-Content -Raw -Encoding UTF8 (Join-Path $root "Assets\BluffAuction\Scripts\Runtime\Platform\Steam.cs")
$codeId = [regex]::Match($steamCs, 'AppId = (\d+)').Groups[1].Value
if ($codeId -ne $AppId) { throw "Steam.cs AppId is $codeId, not $AppId - change it and rebuild first" }
$setup = Get-Content -Raw -Encoding UTF8 (Join-Path $root "Assets\BluffAuction\Scripts\Editor\ProjectSetup.cs")
$ver = [regex]::Match($setup, 'Version = "([^"]+)"').Groups[1].Value
$content = Join-Path $root "Builds\Windows"
if (-not (Test-Path (Join-Path $content "BluffAuction.exe"))) { throw "no build at $content" }
$work = Join-Path $root "Builds\SteamPipe"
New-Item -ItemType Directory -Force (Join-Path $work "output") | Out-Null
$vdf = Get-Content -Raw (Join-Path $PSScriptRoot "app_build.vdf.template")
$vdf = $vdf.Replace("__APPID__", $AppId).Replace("__DEPOTID__", $DepotId).Replace("__VERSION__", $ver)
$vdf = $vdf.Replace("__CONTENT__", $content + "\").Replace("__OUTPUT__", (Join-Path $work "output") + "\")
$script = Join-Path $work "app_build_$AppId.vdf"
[IO.File]::WriteAllText($script, $vdf)
Write-Host "Uploading Bluff Auction $ver (app $AppId, depot $DepotId)"
& $SteamCmd +login $User +run_app_build $script +quit
exit $LASTEXITCODE
