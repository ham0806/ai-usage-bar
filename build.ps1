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

$outDir = Join-Path $PSScriptRoot "dist\AI Usage Bar"
if (Test-Path $outDir) {
    Remove-Item $outDir -Recurse -Force
}

$dotnet = Get-Dotnet
$publishArgs = @(
    "publish", "src\AiUsageBar\AiUsageBar.csproj",
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-o", $outDir,
    "/p:PublishSingleFile=true",
    "/p:IncludeNativeLibrariesForSelfExtract=true",
    "/p:EnableCompressionInSingleFile=true",
    "/p:DebugType=None",
    "/p:DebugSymbols=false"
)
if ($Version) {
    $publishArgs += "/p:Version=$Version"
}

& $dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Get-ChildItem $outDir -Filter *.pdb -ErrorAction SilentlyContinue | Remove-Item -Force

$exe = Join-Path $outDir "AI Usage Bar.exe"
if (-not (Test-Path $exe)) {
    Write-Error "exe was not written: $exe"
}

Write-Host "Built: $exe"
Get-ChildItem $outDir | ForEach-Object { Write-Host ("  {0}" -f $_.Name) }
