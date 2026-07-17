[CmdletBinding()]
param(
    [string]$ZipPath,
    [switch]$KeepRunning,
    [switch]$UseActivationPipe
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $ZipPath) {
    $ZipPath = Join-Path $repositoryRoot 'artifacts\Beacon-Portable-x64.zip'
}

$smokeA = Join-Path $repositoryRoot 'artifacts\smoke-a'
$smokeB = Join-Path $repositoryRoot 'artifacts\smoke-b'
$repositoryPrefix = $repositoryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
foreach ($path in @($smokeA, $smokeB)) {
    $fullPath = [IO.Path]::GetFullPath($path)
    if (-not $fullPath.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the repository: $fullPath"
    }
    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
}

Expand-Archive -LiteralPath $ZipPath -DestinationPath $smokeA

if (-not $UseActivationPipe -and -not ('BeaconR1Hotkey' -as [type])) {
    Add-Type @"
using System.Runtime.InteropServices;
public static class BeaconR1Hotkey
{
    [DllImport("user32.dll")]
    public static extern void keybd_event(byte key, byte scan, uint flags, System.UIntPtr extra);
}
"@
}

function Invoke-BeaconHotkey {
    [BeaconR1Hotkey]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
    [BeaconR1Hotkey]::keybd_event(0x10, 0, 0, [UIntPtr]::Zero)
    [BeaconR1Hotkey]::keybd_event(0x20, 0, 0, [UIntPtr]::Zero)
    [BeaconR1Hotkey]::keybd_event(0x20, 0, 2, [UIntPtr]::Zero)
    [BeaconR1Hotkey]::keybd_event(0x10, 0, 2, [UIntPtr]::Zero)
    [BeaconR1Hotkey]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
}

function Test-Beacon([string]$Root) {
    $executable = Join-Path $Root 'Beacon\Beacon.Next.exe'
    $log = Join-Path $Root 'Beacon\Data\Logs\beacon.log'
    $process = Start-Process -FilePath $executable -PassThru
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        while ([DateTime]::UtcNow -lt $deadline) {
            $content = if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Raw } else { '' }
            if ($content -match 'Hotkey and tray registered') { break }
            Start-Sleep -Milliseconds 200
        }
        if ($process.HasExited) { throw "Beacon.Next exited with code $($process.ExitCode)." }
        if ($content -notmatch 'Hotkey and tray registered') { throw 'Hotkey registration was not observed.' }

        if ($UseActivationPipe) {
            $activationProcess = Start-Process -FilePath $executable -PassThru
            if (-not $activationProcess.WaitForExit(15000)) {
                Stop-Process -Id $activationProcess.Id -Force
                throw 'Second Beacon.Next instance did not exit within 15 seconds.'
            }
            if ($activationProcess.ExitCode -ne 0) {
                throw "Second Beacon.Next instance exited with code $($activationProcess.ExitCode)."
            }
        }
        else {
            Invoke-BeaconHotkey
        }

        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        while ([DateTime]::UtcNow -lt $deadline) {
            $content = if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Raw } else { '' }
            if ($content -match 'Hotkey or activation pipe' -and $content -match 'RPC cancellation confirmed') {
                break
            }
            Start-Sleep -Milliseconds 200
        }

        $content = Get-Content -LiteralPath $log -Raw
        if ($content -match 'ERROR|Exception') { throw "Error marker found in $log." }
        if ($content -notmatch 'Hotkey or activation pipe') { throw 'Window activation was not observed.' }
        if ($content -notmatch 'RPC cancellation confirmed') { throw 'RPC cancellation was not observed.' }
        return $process
    }
    catch {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        throw
    }
}

$firstProcess = Test-Beacon $smokeA
Stop-Process -Id $firstProcess.Id
$firstProcess.WaitForExit()

New-Item -ItemType Directory -Path $smokeB | Out-Null
Move-Item -LiteralPath (Join-Path $smokeA 'Beacon') -Destination (Join-Path $smokeB 'Beacon')
$secondProcess = Test-Beacon $smokeB
if (-not $KeepRunning) {
    Stop-Process -Id $secondProcess.Id
    $secondProcess.WaitForExit()
}

Write-Host 'Portable launch, hotkey, RPC cancellation, and folder-move restart passed.'
