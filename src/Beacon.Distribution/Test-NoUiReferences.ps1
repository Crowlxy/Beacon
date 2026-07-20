[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$patterns = @(
    'using System.Windows',
    'Microsoft.UI.Xaml',
    'Windows.UI.Xaml',
    'UseWPF',
    'UseWindowsForms',
    'FrameworkReference'
)
$files = Get-ChildItem (Join-Path $repositoryRoot 'src\Beacon.Contracts'), (Join-Path $repositoryRoot 'src\Beacon.Core') -Recurse -File |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' }
$matches = $files | Select-String -SimpleMatch -Pattern $patterns
if ($matches) {
    $matches | ForEach-Object { Write-Error "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
    throw 'UI framework reference check failed.'
}

Write-Host 'Contracts/Core UI framework reference check passed.'
