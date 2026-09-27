$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'PetUsageOverlay\PetUsageOverlay.csproj'
$exe = Join-Path $PSScriptRoot 'PetUsageOverlay\bin\Release\net8.0-windows\PetUsageOverlay.exe'

if (-not (Test-Path -LiteralPath $exe)) {
    dotnet build $project -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw '宠物用量程序编译失败。' }
}

Start-Process -FilePath $exe
