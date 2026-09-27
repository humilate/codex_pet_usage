$ErrorActionPreference = 'Stop'
$overlay = Join-Path $PSScriptRoot '..\bin\PetUsageOverlay.exe'
if (-not (Test-Path -LiteralPath $overlay)) {
    throw '插件缺少 PetUsageOverlay.exe，请重新安装插件。'
}
Start-Process -FilePath $overlay
