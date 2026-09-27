param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('orbit', 'below')]
    [string]$Mode
)

$ErrorActionPreference = 'Stop'
$overlay = Join-Path $PSScriptRoot '..\bin\PetUsageOverlay.exe'
if (-not (Test-Path -LiteralPath $overlay)) {
    throw '插件缺少 PetUsageOverlay.exe，请重新安装插件。'
}
$result = Start-Process -FilePath $overlay -ArgumentList @('--set-mode', $Mode) -WindowStyle Hidden -Wait -PassThru
if ($result.ExitCode -ne 0) { throw "切换显示模式失败，退出码：$($result.ExitCode)" }
& (Join-Path $PSScriptRoot 'start.ps1')
