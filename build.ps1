# AI Usage Bar の Windows exe を dist\AI Usage Bar\ に作る
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
    Write-Error ".NET 8 SDK が見つかりません。mise install dotnet@8 を実行してください。"
}

$dotnet = Get-Dotnet
& $dotnet publish src\AiUsageBar\AiUsageBar.csproj -c Release -r win-x64 --self-contained true -o "dist\AI Usage Bar"
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$exe = Join-Path $PSScriptRoot "dist\AI Usage Bar\AI Usage Bar.exe"
if (-not (Test-Path $exe)) {
    Write-Error "exe が出力されませんでした: $exe"
}

Write-Host "完成: $exe"
Write-Host "このフォルダごとコピーして使ってください。exe 単体では動きません。"
