# Run the test suite locally (the same command as the CI "test" job).
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

function Get-Dotnet {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }
    $mise = Join-Path $env:LOCALAPPDATA "mise\dotnet-root\dotnet.exe"
    if (Test-Path $mise) {
        return $mise
    }
    Write-Error ".NET 8 SDK was not found. Run: mise install dotnet@8"
}

$dotnet = Get-Dotnet
& $dotnet test AiUsageBar.sln -c $Configuration --nologo
exit $LASTEXITCODE
