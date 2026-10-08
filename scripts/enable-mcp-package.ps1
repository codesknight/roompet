# (Re-)attach the MCP for Unity package to this project's manifest.
#
# The committed Packages/manifest.json deliberately does NOT reference the AI dev
# toolbox: it is a local development environment, not a game dependency, and a clone
# should open with zero package errors. This script puts the line back on a machine
# that does have the checkout, so the local setup can be recreated at any time —
# including after `git reset --hard`, which can clobber the skip-worktree'd working
# copy.
#
#   powershell -File scripts/enable-mcp-package.ps1
#   powershell -File scripts/enable-mcp-package.ps1 -PackagePath 'D:\somewhere\unity-mcp\MCPForUnity'
#   powershell -File scripts/enable-mcp-package.ps1 -Remove     # detach again

[CmdletBinding()]
param(
    [string]$PackagePath,
    [string]$Project,
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'

# NOTE: $PSScriptRoot is empty inside param() defaults on Windows PowerShell 5.1,
# so the paths are resolved here in the body instead.
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $Project) { $Project = Join-Path $repoRoot 'UnityMCPProject' }

# Default: the checkout that lives next to this repository, as docs/MCP_SETUP.md sets up.
if (-not $PackagePath) { $PackagePath = Join-Path $repoRoot 'unity-mcp\MCPForUnity' }

$manifestPath = Join-Path $Project 'Packages\manifest.json'
if (-not (Test-Path $manifestPath)) { throw "manifest not found: $manifestPath" }

$utf8 = New-Object System.Text.UTF8Encoding($false)
$text = [System.IO.File]::ReadAllText($manifestPath, [System.Text.Encoding]::UTF8)
$entry = '"com.coplaydev.unity-mcp": "file:' + ($PackagePath -replace '\\', '/') + '",'

if ($Remove) {
    $text = [regex]::Replace($text, '\s*"com\.coplaydev\.unity-mcp":\s*"file:[^"]*",', '')
    [System.IO.File]::WriteAllText($manifestPath, $text, $utf8)
    Write-Host "[RoomPet] Removed the MCP for Unity dependency from $manifestPath"
    return
}

if ($text -match 'com\.coplaydev\.unity-mcp') {
    Write-Host "[RoomPet] Already referenced in $manifestPath — nothing to do."
    return
}

if (-not (Test-Path (Join-Path $PackagePath 'package.json'))) {
    Write-Warning "No package.json at '$PackagePath'. The reference will be written anyway, but Unity will report a missing package until the checkout is there (see docs/MCP_SETUP.md)."
}

# Insert as the first dependency so the diff is at the top of the file.
$text = [regex]::Replace($text, '("dependencies"\s*:\s*\{\s*\n)', "`$1    $entry`n", 1)
[System.IO.File]::WriteAllText($manifestPath, $text, $utf8)
Write-Host "[RoomPet] Added the MCP for Unity dependency to $manifestPath"
Write-Host "[RoomPet]   $entry"
Write-Host "[RoomPet] Switch back to Unity and let it resolve the package."
