# Install the MCP for Unity Python server (from the local checkout) into a
# self-contained virtual environment inside this workspace.
#
# Why a venv instead of `uvx --from <Server>`: the agent file sandbox breaks
# setuptools' build step (a Python process cannot write into a temp directory it
# just created in the same process), so the wheel is built once here, outside the
# sandbox, and DSH then launches the console script directly with no uv at runtime.
#
# Re-run this script after updating the checkout to refresh the environment.
#
#   powershell -File scripts/install-server.ps1
#   powershell -File scripts/install-server.ps1 -Python C:\Python312\python.exe
#
# -Root defaults to the repository this script lives in, and -Python defaults to
# whatever `uv` picks, so no machine-specific paths are baked in.

[CmdletBinding()]
param(
    [string]$Root,
    [string]$Python
)

$ErrorActionPreference = 'Continue'

# $PSScriptRoot is empty inside param() defaults on Windows PowerShell 5.1.
if (-not $Root) { $Root = Split-Path -Parent $PSScriptRoot }

# uv writes progress to stderr; PowerShell would otherwise surface it as an error
# record and (under 'Stop') abort the script. Run native tools explicitly instead.
function Invoke-Native {
    param([string]$Exe, [string[]]$Arguments)
    $output = & $Exe @Arguments 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "$Exe exited with code $LASTEXITCODE`n$output"
    }
    return $output
}

$Server = Join-Path $Root 'unity-mcp\Server'
$Venv = Join-Path $Root '.venv'

if (-not (Test-Path (Join-Path $Server 'pyproject.toml'))) {
    throw "Server checkout not found at $Server"
}

$env:UV_CACHE_DIR = Join-Path $Root '.tools\uv-cache'
$env:UV_TOOL_DIR = Join-Path $Root '.tools\uv-tools'

$venvArgs = @('venv', $Venv, '--allow-existing')
if ($Python) { $venvArgs += @('--python', $Python) }

if ($Python) { Write-Host "==> Creating virtual environment at $Venv (base: $Python)" }
else { Write-Host "==> Creating virtual environment at $Venv (base: whatever uv picks)" }
Invoke-Native 'uv' $venvArgs | Write-Host

$venvPython = Join-Path $Venv 'Scripts\python.exe'
if (-not (Test-Path $venvPython)) { $venvPython = Join-Path $Venv 'bin/python' }

Write-Host "==> Installing mcpforunityserver from $Server"
Invoke-Native 'uv' @('pip', 'install', '--python', $venvPython, $Server) | Write-Host

Write-Host "==> Verifying console script"
Invoke-Native (Join-Path $Venv 'Scripts\mcp-for-unity.exe') @('--help') | Write-Host
