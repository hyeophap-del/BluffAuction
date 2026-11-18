# 규칙 엔진 + AI 를 Unity 에 포함된 Roslyn 으로 컴파일해 시뮬레이션을 돌린다.
# 사용: powershell -ExecutionPolicy Bypass -File Tools\Sim\run-sim.ps1 -- --games 1000 --players 4
param(
    [string]$UnityData = "C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Data",
    [Parameter(ValueFromRemainingArguments = $true)] [string[]]$SimArgs
)
$ErrorActionPreference = 'Stop'
$dotnet = Join-Path $UnityData "NetCoreRuntime\dotnet.exe"
$csc = Join-Path $UnityData "DotNetSdkRoslyn\csc.dll"
$refDir = Join-Path $UnityData "MonoBleedingEdge\lib\mono\4.7.1-api"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$core = Resolve-Path (Join-Path $root "..\..\Assets\BluffAuction\Scripts\Core")
$out = Join-Path $root "bin"
New-Item -ItemType Directory -Force $out | Out-Null

$sources = @(Get-ChildItem $core -Recurse -Filter *.cs | ForEach-Object { $_.FullName })
$sources += (Join-Path $root "SimMain.cs")

& $dotnet $csc /nologo /noconfig /nostdlib /utf8output /target:exe /langversion:9 /optimize+ /warn:4 /nowarn:1591 `
    /out:"$out\Sim.exe" `
    /reference:"$refDir\mscorlib.dll" /reference:"$refDir\System.dll" /reference:"$refDir\System.Core.dll" `
    $sources
if ($LASTEXITCODE -ne 0) { Write-Host "compile failed"; exit $LASTEXITCODE }
& "$out\Sim.exe" @SimArgs
exit $LASTEXITCODE
