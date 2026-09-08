# Build the self-contained Windows exe into dist\AI Usage Bar\
param(
    [string]$Version
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
$publishArgs = @(
    "publish", "src\AiUsageBar\AiUsageBar.csproj",
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-o", "dist\AI Usage Bar"
)
if ($Version) {
    $publishArgs += "/p:Version=$Version"
}

& $dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$exe = Join-Path $PSScriptRoot "dist\AI Usage Bar\AI Usage Bar.exe"
if (-not (Test-Path $exe)) {
    Write-Error "exe was not written: $exe"
}

Write-Host "Built: $exe"
Write-Host "Copy the whole folder. The exe does not run by itself."
