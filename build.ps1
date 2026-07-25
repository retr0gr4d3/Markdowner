<#
.SYNOPSIS
    Build script for Windows (also works on PowerShell 7 for macOS and Linux).

.EXAMPLE
    .\build.ps1
    Restore, build, test, and package artifacts\Markdowner-<version>-<rid>.zip.

.EXAMPLE
    .\build.ps1 -NoPackage
    Just build and test - the fast inner loop.

.EXAMPLE
    .\build.ps1 -Runtime win-arm64 -NoTest
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $Runtime,
    [string] $Version,
    [switch] $NoPackage,
    [switch] $NoTest,
    # Packaging is the default; accepted so existing habits keep working.
    [switch] $Publish
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK is not on PATH - install .NET 10 from https://dotnet.microsoft.com/download'
}

# Work out a sensible default runtime identifier from the host.
if (-not $Runtime) {
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
    $rid = if ($arch -eq 'arm64') { 'arm64' } else { 'x64' }

    $Runtime =
        if ($IsLinux) { "linux-$rid" }
        elseif ($IsMacOS) { "osx-$rid" }
        else { "win-$rid" }
}

if (-not $Version) {
    $props = Get-Content (Join-Path $root 'Directory.Build.props') -Raw
    $Version = if ($props -match '<Version>(.*?)</Version>') { $Matches[1] } else { '1.0.0' }
}

Write-Host "==> Configuration : $Configuration"
Write-Host "==> Runtime       : $Runtime"
Write-Host "==> Version       : $Version"

$solution = Join-Path $root 'Markdowner.sln'

dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw 'restore failed' }

dotnet build $solution -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw 'build failed' }

if (-not $NoTest) {
    Write-Host '==> Running tests'
    dotnet test $solution -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw 'tests failed' }
}

if ($NoPackage) {
    Write-Host '==> Skipping packaging (-NoPackage)'
    return
}

$staging = Join-Path $root "artifacts/staging/$Runtime"
$package = Join-Path $root "artifacts/Markdowner-$Version-$Runtime.zip"

Write-Host '==> Publishing'
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging -Force | Out-Null

dotnet publish (Join-Path $root 'src/Markdowner/Markdowner.csproj') `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -p:Version=$Version `
    -o $staging
if ($LASTEXITCODE -ne 0) { throw 'publish failed' }

# Debug symbols are not part of a release drop.
Get-ChildItem $staging -Recurse -Filter *.pdb | Remove-Item -Force
Copy-Item (Join-Path $root 'README.md') $staging -ErrorAction SilentlyContinue

Write-Host "==> Packaging $package"
if (Test-Path $package) { Remove-Item $package -Force }
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $package -CompressionLevel Optimal

# The staging tree has served its purpose; leave only the archive behind.
Remove-Item (Join-Path $root 'artifacts/staging') -Recurse -Force

$size = '{0:N1} MB' -f ((Get-Item $package).Length / 1MB)
Write-Host ''
Write-Host "  Package: $package"
Write-Host "  Size:    $size"
Write-Host ''
