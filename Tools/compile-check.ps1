# Unity 를 열지 않고 런타임 스크립트를 몇 초 만에 컴파일 검사한다 (Roslyn + Unity 6 관리 DLL + Library 의 패키지 DLL).
# 처음 한 번은 Unity 가 프로젝트를 가져와 Library\ScriptAssemblies 가 있어야 한다.
# 사용: powershell -ExecutionPolicy Bypass -File Tools\compile-check.ps1 [-Editor]
param([switch]$Editor)
$ErrorActionPreference = 'Stop'
$u22 = "C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Data"
$u6 = "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data"
$dotnet = Join-Path $u22 "NetCoreRuntime\dotnet.exe"
$csc = Join-Path $u22 "DotNetSdkRoslyn\csc.dll"
$root = Resolve-Path (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "..")
$out = Join-Path $env:TEMP "bluff-compile-check"
New-Item -ItemType Directory -Force $out | Out-Null

$refs = @()
$refs += Join-Path $u6 "NetStandard\ref\2.1.0\netstandard.dll"
$refs += Get-ChildItem (Join-Path $u6 "Managed\UnityEngine") -Filter "UnityEngine*.dll" | ForEach-Object { $_.FullName }
$refs += Get-ChildItem (Join-Path $u6 "Managed\UnityEngine") -Filter "Unity.*.dll" | ForEach-Object { $_.FullName }
if ($Editor) { $refs += Get-ChildItem (Join-Path $u6 "Managed\UnityEngine") -Filter "UnityEditor*.dll" | ForEach-Object { $_.FullName } }
$sa = Join-Path $root "Library\ScriptAssemblies"
foreach ($n in 'Unity.RenderPipelines.Core.Runtime','Unity.RenderPipelines.Universal.Runtime','Unity.InputSystem','Unity.RenderPipelines.Core.ShaderLibrary','Unity.RenderPipelines.GPUDriven.Runtime','Unity.RenderPipelines.Universal.Config.Runtime','Unity.Mathematics','Unity.Collections','Unity.Burst','Unity.RenderPipeline.Universal.ShaderLibrary') {
    $f = Join-Path $sa "$n.dll"; if (Test-Path $f) { $refs += $f }
}
if ($Editor) { foreach ($n in 'Unity.RenderPipelines.Universal.Editor','Unity.RenderPipelines.Core.Editor') { $f = Join-Path $sa "$n.dll"; if (Test-Path $f) { $refs += $f } } }

$src = @(Get-ChildItem (Join-Path $root "Assets\BluffAuction\Scripts\Core") -Recurse -Filter *.cs | ForEach-Object { $_.FullName })
# Steamworks.NET (STEAMWORKS_NET 쪽 코드까지 검사)
$sw = Join-Path $root "Packages\com.rlabrecque.steamworks.net\Runtime"
if (Test-Path $sw) { $src += Get-ChildItem $sw -Recurse -Filter *.cs | ForEach-Object { $_.FullName } }
$src += Get-ChildItem (Join-Path $root "Assets\BluffAuction\Scripts\Runtime") -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
if ($Editor) { $src += Get-ChildItem (Join-Path $root "Assets\BluffAuction\Scripts\Editor") -Recurse -Filter *.cs | ForEach-Object { $_.FullName } }

$rsp = Join-Path $out "check.rsp"
$lines = @('/nologo', '/target:library', '/nostdlib', '/langversion:9', '/warn:1', '/nowarn:0618,0649,0414,0169', '/unsafe', "/out:`"$(Join-Path $out 'check.dll')`"")
if (Test-Path $sw) { $lines += '/define:STEAMWORKS_NET;STEAMWORKS_WIN;STEAMWORKS_X64;UNITY_STANDALONE_WIN;UNITY_STANDALONE;UNITY_64' }
$lines += $refs | ForEach-Object { "/r:`"$_`"" }
$lines += $src | ForEach-Object { "`"$_`"" }
[IO.File]::WriteAllLines($rsp, $lines)
& $dotnet $csc "@$rsp" | Where-Object { $_ -notmatch 'CS2023' } | ForEach-Object { $_ -replace [regex]::Escape("$root\"), '' }
if ($LASTEXITCODE -eq 0) { Write-Host "OK ($($src.Count) files)" }
exit $LASTEXITCODE
