<#
.SYNOPSIS
    Restores the repository to a fresh, just-cloned state.

.DESCRIPTION
    Removes every bin/ and obj/ directory, TestResults/, artifacts/, and *.user
    files. Source, the .git directory and IDE settings (.vs, .idea) are left alone.

.EXAMPLE
    .\clean.ps1

.EXAMPLE
    .\clean.ps1 -DryRun
    List what would be removed without deleting anything.
#>
[CmdletBinding()]
param(
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

# Refuse to run anywhere that isn't this project, so a stray invocation from
# elsewhere can never delete someone's unrelated directories.
if (-not (Test-Path (Join-Path $root 'Markdowner.sln'))) {
    throw "$root does not look like the Markdowner repository"
}

$targets = [System.Collections.Generic.List[string]]::new()

Get-ChildItem $root -Recurse -Directory -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '[\\/]\.git([\\/]|$)' } |
    Where-Object { $_.Name -in @('bin', 'obj', 'TestResults') } |
    ForEach-Object { $targets.Add($_.FullName) }

Get-ChildItem $root -Recurse -File -Force -Filter *.user -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '[\\/]\.git([\\/]|$)' } |
    ForEach-Object { $targets.Add($_.FullName) }

$artifacts = Join-Path $root 'artifacts'
if (Test-Path $artifacts) { $targets.Add($artifacts) }

$count = 0
foreach ($target in $targets) {
    # A parent may already have taken this path with it.
    if (-not (Test-Path $target)) { continue }

    $relative = $target.Substring($root.Length).TrimStart('\', '/')

    if ($DryRun) {
        Write-Host "  would remove $relative"
    }
    else {
        Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  removed $relative"
    }

    $count++
}

if ($count -eq 0) {
    Write-Host 'Already clean.'
}
elseif ($DryRun) {
    Write-Host "$count item(s) would be removed. Re-run without -DryRun to delete them."
}
else {
    Write-Host "Clean: removed $count item(s)."
}
