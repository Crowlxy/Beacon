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

function Write-Stage([string]$Message) {
    Write-Host ('[{0:HH:mm:ss.fff}] {1}' -f [DateTime]::UtcNow, $Message)
}

$smokeA = Join-Path $repositoryRoot 'artifacts\smoke-a'
$smokeB = Join-Path $repositoryRoot 'artifacts\smoke-b'
$logArchive = Join-Path $repositoryRoot 'artifacts\logs'
$repositoryPrefix = $repositoryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
foreach ($path in @($smokeA, $smokeB, $logArchive)) {
    $fullPath = [IO.Path]::GetFullPath($path)
    if (-not $fullPath.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the repository: $fullPath"
    }
    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
}

Write-Stage "Expanding $ZipPath to $smokeA"
Expand-Archive -LiteralPath $ZipPath -DestinationPath $smokeA
New-Item -ItemType Directory -Path $logArchive | Out-Null

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

function Test-Beacon([string]$Root, [string]$Phase) {
    $executable = Join-Path $Root 'Beacon\Beacon.Next.exe'
    $logDirectory = Join-Path $Root 'Beacon\Data\Logs'
    function Read-BeaconLog {
        $current = Get-ChildItem -LiteralPath $logDirectory -Filter 'beacon-????-??-??.log' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1
        if ($null -eq $current) { return '' }
        return Get-Content -LiteralPath $current.FullName -Raw
    }
    # 前フェーズから持ち越したログのマーカーに誤マッチすると、起動完了前に
    # activationインスタンスを起動してMutex獲得レースになる（LESSONS 2026-07-17）
    Get-ChildItem -LiteralPath $logDirectory -Filter '*.log' -ErrorAction SilentlyContinue |
        Remove-Item -Force
    Write-Stage "${Phase}: launching first instance"
    $process = Start-Process -FilePath $executable -PassThru
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        while ([DateTime]::UtcNow -lt $deadline) {
            $content = Read-BeaconLog
            if ($content -match 'Hotkey and tray registered') { break }
            Start-Sleep -Milliseconds 200
        }
        if ($process.HasExited) { throw "Beacon.Next exited with code $($process.ExitCode)." }
        if ($content -notmatch 'Hotkey and tray registered') { throw 'Hotkey registration was not observed.' }
        Write-Stage "${Phase}: hotkey registration observed"

        if ($UseActivationPipe) {
            $activationProcess = Start-Process -FilePath $executable -PassThru
            Write-Stage "${Phase}: activation instance launched (pid $($activationProcess.Id))"
            if (-not $activationProcess.WaitForExit(15000)) {
                Write-Stage "${Phase}: activation instance timeout, HasExited=$($activationProcess.HasExited)"
                Stop-Process -Id $activationProcess.Id -Force
                throw 'Second Beacon.Next instance did not exit within 15 seconds.'
            }
            Write-Stage "${Phase}: activation instance exited with code $($activationProcess.ExitCode)"
            if ($activationProcess.ExitCode -ne 0) {
                throw "Second Beacon.Next instance exited with code $($activationProcess.ExitCode)."
            }
        }
        else {
            Write-Stage "${Phase}: sending hotkey"
            Invoke-BeaconHotkey
        }

        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        while ([DateTime]::UtcNow -lt $deadline) {
            $content = Read-BeaconLog
            if ($content -match 'Hotkey or activation pipe') {
                break
            }
            Start-Sleep -Milliseconds 200
        }

        $content = Read-BeaconLog
        if ($content -match 'ERROR|Exception') { throw "Error marker found in $logDirectory." }
        if ($content -notmatch 'Hotkey or activation pipe') { throw 'Window activation was not observed.' }
        Write-Stage "${Phase}: all log markers observed"
        return $process
    }
    catch {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        throw
    }
    finally {
        foreach ($file in Get-ChildItem -LiteralPath $logDirectory -Filter '*.log' -ErrorAction SilentlyContinue) {
            Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $logArchive "$Phase-$($file.Name)") -Force
        }
    }
}

$firstProcess = Test-Beacon $smokeA 'smoke-a'
Stop-Process -Id $firstProcess.Id
$firstProcess.WaitForExit()

New-Item -ItemType Directory -Path $smokeB | Out-Null
Move-Item -LiteralPath (Join-Path $smokeA 'Beacon') -Destination (Join-Path $smokeB 'Beacon')
$secondProcess = Test-Beacon $smokeB 'smoke-b'
if (-not $KeepRunning) {
    Stop-Process -Id $secondProcess.Id
    $secondProcess.WaitForExit()
}

Write-Host 'Portable launch, activation, and folder-move restart passed.'
