param([switch]$SkipInstall, [switch]$PreserveAutostart)

$ErrorActionPreference = 'Stop'
$pluginRoot = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'plugins\pet-usage'
$pluginSource = Join-Path $PSScriptRoot 'plugin'
$skillRoot = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex\skills\.system\plugin-creator'
foreach ($relativePath in @(
    '.codex-plugin\plugin.json',
    'skills\pet-usage\SKILL.md',
    'scripts\install-autostart.ps1',
    'scripts\uninstall-autostart.ps1',
    'scripts\set-mode.ps1',
    'scripts\start.ps1'
)) {
    $sourceFile = Join-Path $pluginSource $relativePath
    if (-not (Test-Path -LiteralPath $sourceFile)) { throw "找不到插件源文件：$sourceFile" }
    $destination = Join-Path $pluginRoot $relativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $sourceFile -Destination $destination -Force
}

$overlayProject = Join-Path $PSScriptRoot 'PetUsageOverlay\PetUsageOverlay.csproj'
$watcherProject = Join-Path $PSScriptRoot 'PetUsageWatcher\PetUsageWatcher.csproj'
$output = Join-Path $pluginRoot 'bin'
$taskName = 'Codex Pet Usage Watcher'
dotnet build $overlayProject -c Release --nologo -v:q
if ($LASTEXITCODE -ne 0) { throw '宠物用量浮层编译失败。' }
dotnet build $watcherProject -c Release --nologo -v:q
if ($LASTEXITCODE -ne 0) { throw '宠物用量监听程序编译失败。' }

$task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($task) { Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue }
$running = @(Get-CimInstance Win32_Process | Where-Object {
    $_.Name -in @('PetUsageOverlay.exe', 'PetUsageWatcher.exe') -and
    $_.ExecutablePath -and
    [string]::Equals((Split-Path -Parent $_.ExecutablePath), $output,
        [StringComparison]::OrdinalIgnoreCase)
})
foreach ($process in $running) {
    Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
}
if ($running.Count -gt 0) {
    Wait-Process -Id $running.ProcessId -Timeout 10 -ErrorAction SilentlyContinue
}

dotnet publish $overlayProject -c Release -r win-x64 --self-contained false -o $output --nologo -v:q -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw '宠物用量浮层发布失败。' }
dotnet publish $watcherProject -c Release -r win-x64 --self-contained false -o $output --nologo -v:q -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw '宠物用量监听程序发布失败。' }

$oldSymbols = Join-Path $output 'PetUsageOverlay.pdb'
if (Test-Path -LiteralPath $oldSymbols) { Remove-Item -LiteralPath $oldSymbols }

foreach ($unusedAsset in @('assets\orbit.png', 'assets\below.png')) {
    $path = Join-Path $pluginRoot $unusedAsset
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
}

if (-not $PreserveAutostart) {
    & (Join-Path $pluginRoot 'scripts\install-autostart.ps1')
} elseif (-not $task) {
    throw "找不到现有的 $taskName 计划任务。"
}
Start-ScheduledTask -TaskName $taskName

python (Join-Path $skillRoot 'scripts\validate_plugin.py') $pluginRoot
if ($LASTEXITCODE -ne 0) { throw '插件校验失败。' }
if ($SkipInstall) { return }

python (Join-Path $skillRoot 'scripts\update_plugin_cachebuster.py') $pluginRoot
if ($LASTEXITCODE -ne 0) { throw '插件版本更新失败。' }
$marketplace = python (Join-Path $skillRoot 'scripts\read_marketplace_name.py')
if ($LASTEXITCODE -ne 0) { throw '个人插件目录读取失败。' }
codex plugin add "pet-usage@$marketplace"
if ($LASTEXITCODE -ne 0) { throw '插件安装失败。' }
