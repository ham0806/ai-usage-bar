# AI Usage Bar を起動する
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$exe = Join-Path $PSScriptRoot "dist\AI Usage Bar\AI Usage Bar.exe"
if (Test-Path $exe) {
    Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe)
    return
}

function Get-Dotnet {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }
    $mise = Join-Path $env:LOCALAPPDATA "mise\dotnet-root\dotnet.exe"
    if (Test-Path $mise) {
        return $mise
    }
    Write-Error ".NET 8 SDK が見つかりません。先に mise install dotnet@8 を実行するか、.\build.ps1 で exe を作ってください。"
}

$dotnet = Get-Dotnet
& $dotnet run --project src\AiUsageBar\AiUsageBar.csproj -c Release
