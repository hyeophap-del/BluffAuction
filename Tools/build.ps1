# 배치 모드로 Windows 빌드 (Unity 를 열지 않고). 사용: powershell -ExecutionPolicy Bypass -File Tools\build.ps1 [-Prepare] [-Dev]
param(
    [switch]$Prepare,   # 빌드 없이 설정·자산·씬만 (컴파일 확인)
    [switch]$Dev
)
$ErrorActionPreference = 'Stop'
$unity = "C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Unity.exe"
$root = Resolve-Path (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "..")
$log = Join-Path $root "Logs\build.log"
New-Item -ItemType Directory -Force (Join-Path $root "Logs") | Out-Null
$method = if ($Prepare) { "BluffAuction.EditorTools.BuildScript.PrepareOnly" } else { "BluffAuction.EditorTools.BuildScript.BuildWindows" }
$args = @('-batchmode', '-quit', '-projectPath', "`"$root`"", '-executeMethod', $method, '-logFile', "`"$log`"")
if ($Dev) { $args += '-devbuild' }
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath $unity -ArgumentList $args -PassThru -Wait -NoNewWindow
Write-Host ("exit {0} in {1:F0}s" -f $p.ExitCode, $sw.Elapsed.TotalSeconds)
# 컴파일 오류와 빌드 결과만 추려 보여 준다
Select-String -Path $log -Pattern 'error CS|\[Build\]|Exception|Compilation failed|Scripts have compiler errors' | Select-Object -First 60 | ForEach-Object { $_.Line }
exit $p.ExitCode
