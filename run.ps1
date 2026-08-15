# AI Usage Bar を起動する
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$pythonw = Join-Path $PSScriptRoot ".venv\Scripts\pythonw.exe"
$python = Join-Path $PSScriptRoot ".venv\Scripts\python.exe"
if (Test-Path $pythonw) {
    Start-Process -FilePath $pythonw -ArgumentList "-m", "ai_usage_bar" -WorkingDirectory $PSScriptRoot
} elseif (Test-Path $python) {
    Start-Process -FilePath $python -ArgumentList "-m", "ai_usage_bar" -WorkingDirectory $PSScriptRoot
} else {
    Write-Error "先に .venv を作成し、pip install -e . を実行してください。"
}
