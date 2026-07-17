[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$portableRoot = Join-Path $artifactsRoot 'portable'
$stage = Join-Path $portableRoot 'Beacon'
$hostStage = Join-Path $artifactsRoot 'plugin-host'
$zipPath = Join-Path $artifactsRoot 'Beacon-Portable-x64.zip'

function Assert-RepositoryChild([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $prefix = $repositoryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the repository: $fullPath"
    }
}

foreach ($path in @($portableRoot, $hostStage, $zipPath)) {
    Assert-RepositoryChild $path
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

$winuiArguments = @(
    'publish', (Join-Path $repositoryRoot 'src\Beacon.WinUI\Beacon.WinUI.csproj'),
    '-c', $Configuration, '-r', $Runtime, '--self-contained', 'true',
    '-p:WindowsAppSDKSelfContained=true', '-p:DebugType=None', '-o', $stage
)
& dotnet @winuiArguments
if ($LASTEXITCODE -ne 0) { throw 'Beacon.WinUI publish failed.' }

$hostArguments = @(
    'publish', (Join-Path $repositoryRoot 'src\Beacon.PluginHost\Beacon.PluginHost.csproj'),
    '-c', $Configuration, '-r', $Runtime, '--self-contained', 'true',
    '-p:DebugType=None', '-o', $hostStage
)
& dotnet @hostArguments
if ($LASTEXITCODE -ne 0) { throw 'Beacon.PluginHost publish failed.' }

Get-ChildItem -LiteralPath $hostStage -Force | Copy-Item -Destination $stage -Recurse -Force
foreach ($name in @('System.Windows.dll', 'WindowsBase.dll')) {
    Remove-Item -LiteralPath (Join-Path $stage $name) -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path (Join-Path $stage 'Plugins') -Force | Out-Null
foreach ($name in @('Settings', 'History', 'Plugins', 'Cache', 'Logs', 'Clipboard', 'State')) {
    New-Item -ItemType Directory -Path (Join-Path $stage "Data\$name") -Force | Out-Null
}

New-Item -ItemType File -Path (Join-Path $stage 'portable.flag') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $stage
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'attribution.md') -Destination $stage

Compress-Archive -LiteralPath $stage -DestinationPath $zipPath -CompressionLevel Optimal
Get-ChildItem -LiteralPath $stage -Recurse -File -Filter '*.dll' |
    ForEach-Object { $_.FullName.Substring($stage.Length + 1) } |
    Sort-Object |
    Set-Content -LiteralPath (Join-Path $artifactsRoot 'portable-dlls.txt') -Encoding utf8

Write-Host "Portable ZIP: $zipPath"
