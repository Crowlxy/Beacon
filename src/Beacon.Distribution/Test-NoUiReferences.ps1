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
$files = Get-ChildItem (Join-Path $repositoryRoot 'src\Beacon.Contracts'), (Join-Path $repositoryRoot 'src\Beacon.Core'), (Join-Path $repositoryRoot 'src\Beacon.Platform.Windows') -Recurse -File |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' }
$matches = $files | Select-String -SimpleMatch -Pattern $patterns
if ($matches) {
    $matches | ForEach-Object { Write-Error "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
    throw 'UI framework reference check failed.'
}

Write-Host 'Contracts/Core/Platform.Windows UI framework reference check passed.'

# 禁止ブランド語チェック（CLAUDE.md不変制約。語自体をこのスクリプトへ書かないため連結で構築）
$bannedWord = 'Spot' + 'light'
$textExtensions = '.cs', '.xaml', '.csproj', '.props', '.targets', '.ps1', '.psm1', '.json', '.resw', '.xml', '.manifest', '.sln', '.yml'
$allCodeFiles = Get-ChildItem (Join-Path $repositoryRoot 'src'), (Join-Path $repositoryRoot 'tests') -Recurse -File |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' }
$bannedNameHits = $allCodeFiles | Where-Object { $_.Name -match $bannedWord }
$bannedContentHits = $allCodeFiles |
    Where-Object { $textExtensions -contains $_.Extension } |
    Select-String -SimpleMatch -Pattern $bannedWord
if ($bannedNameHits -or $bannedContentHits) {
    $bannedNameHits | ForEach-Object { Write-Error "Banned brand word in file name: $($_.FullName)" }
    $bannedContentHits | ForEach-Object { Write-Error "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
    throw 'Banned brand word check failed.'
}

Write-Host 'Banned brand word check passed.'
