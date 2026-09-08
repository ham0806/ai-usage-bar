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
    Write-Error ".NET 8 SDK was not found. Run mise install dotnet@8, or build the exe with .\build.ps1."
}

$dotnet = Get-Dotnet
& $dotnet run --project src\AiUsageBar\AiUsageBar.csproj -c Release
