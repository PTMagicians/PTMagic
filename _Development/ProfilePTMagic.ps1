param(
    [int]$ProcessId = 0,
    [int]$DurationSeconds = 180,
    [string]$OutputRoot = ""
)

$ErrorActionPreference = "Stop"

function Get-DurationArgument([int]$Seconds) {
    if ($Seconds -le 0) {
        throw "DurationSeconds must be greater than zero."
    }

    $duration = [TimeSpan]::FromSeconds($Seconds)
    return "{0:00}:{1:00}:{2:00}:{3:00}" -f [int]$duration.TotalDays, $duration.Hours, $duration.Minutes, $duration.Seconds
}

function Ensure-DotNetTrace {
    $traceCommand = Get-Command "dotnet-trace" -ErrorAction SilentlyContinue
    if ($null -ne $traceCommand) {
        return [string]$traceCommand.Source
    }

    Write-Host "dotnet-trace was not found. Installing the latest global tool..."
    $installOutput = @(& dotnet tool install --global dotnet-trace 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to install dotnet-trace.`n$($installOutput -join [Environment]::NewLine)"
    }

    $globalToolsPath = Join-Path $env:USERPROFILE ".dotnet\tools"
    if ($env:PATH -notlike "*$globalToolsPath*") {
        $env:PATH = "$env:PATH;$globalToolsPath"
    }

    $traceExe = Join-Path $globalToolsPath "dotnet-trace.exe"
    if (Test-Path $traceExe) {
        return [string]$traceExe
    }

    $traceCommand = Get-Command "dotnet-trace" -ErrorAction SilentlyContinue
    if ($null -eq $traceCommand) {
        throw "dotnet-trace was installed but could not be found. Expected path: $traceExe"
    }

    return [string]$traceCommand.Source
}

function Find-PTMagicProcessId([string]$DotNetTracePath) {
    $processList = & $DotNetTracePath ps 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet-trace ps failed.`n$($processList -join [Environment]::NewLine)"
    }

    $processMatches = @()
    foreach ($line in $processList) {
        if ($line -match "PTMagic(\.dll|\.exe)?" -and $line -match "^\s*(\d+)") {
            $processMatches += [PSCustomObject]@{
                ProcessId = [int]$Matches[1]
                Line = $line
            }
        }
    }

    if ($processMatches.Count -eq 1) {
        Write-Host "Automatically selected PTMagic process $($processMatches[0].ProcessId):"
        Write-Host "  $($processMatches[0].Line)"
        return $processMatches[0].ProcessId
    }

    Write-Host ""
    Write-Host "dotnet-trace ps output:"
    $processList | ForEach-Object { Write-Host $_ }
    Write-Host ""

    if ($processMatches.Count -eq 0) {
        throw "No PTMagic process was found automatically. Run this script again with -ProcessId <PID>."
    }

    Write-Host "Multiple PTMagic processes were found:"
    $processMatches | ForEach-Object { Write-Host "  $($_.Line)" }
    throw "Run this script again with -ProcessId <PID> for the instance you want to profile."
}

$dotNetTrace = [string](Ensure-DotNetTrace)

if ($ProcessId -eq 0) {
    $ProcessId = Find-PTMagicProcessId $dotNetTrace
}

try {
    $targetProcess = Get-Process -Id $ProcessId -ErrorAction Stop
}
catch {
    throw "Process ID $ProcessId is not running."
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $PSScriptRoot "PerformanceProfiles"
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outputDirectory = Join-Path $OutputRoot $timestamp
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$traceFile = Join-Path $outputDirectory "PTMagic-$ProcessId.nettrace"
$metricsFile = Join-Path $outputDirectory "process-metrics.csv"
$exclusiveReport = Join-Path $outputDirectory "top-exclusive.txt"
$inclusiveReport = Join-Path $outputDirectory "top-inclusive.txt"
$metadataFile = Join-Path $outputDirectory "profile-info.txt"
$durationArgument = Get-DurationArgument $DurationSeconds

$processStartTime = $targetProcess.StartTime
$profileStartTime = Get-Date
$machineCpuCount = [Environment]::ProcessorCount

@(
    "PTMagic Stage 0 Performance Profile",
    "Captured: $($profileStartTime.ToString('O'))",
    "Process ID: $ProcessId",
    "Process name: $($targetProcess.ProcessName)",
    "Process start: $($processStartTime.ToString('O'))",
    "Logical processors: $machineCpuCount",
    "Requested duration: $DurationSeconds seconds",
    "Trace profiles: dotnet-sampled-thread-time,dotnet-common,gc-verbose",
    "",
    "For the normal-raid baseline, capture a second-or-later raid after startup/recovery work has completed."
) | Set-Content -Path $metadataFile -Encoding UTF8

Write-Host ""
Write-Host "PTMagic Stage 0 performance capture"
Write-Host "  PID:              $ProcessId"
Write-Host "  Duration:         $DurationSeconds seconds"
Write-Host "  Logical CPUs:     $machineCpuCount"
Write-Host "  Output directory: $outputDirectory"
Write-Host ""
Write-Host "For a representative normal-cycle profile, this capture should include exactly one ordinary scheduled raid, not the initial startup raid."
Write-Host ""

$traceArguments = @(
    "collect",
    "--process-id", $ProcessId,
    "--profile", "dotnet-sampled-thread-time,dotnet-common,gc-verbose",
    "--duration", $durationArgument,
    "--output", ('"' + $traceFile + '"')
)

$traceProcess = Start-Process -FilePath $dotNetTrace -ArgumentList $traceArguments -PassThru -NoNewWindow

$metrics = [System.Collections.Generic.List[object]]::new()
$previousSampleTime = Get-Date
$previousCpuMilliseconds = $targetProcess.TotalProcessorTime.TotalMilliseconds

while (-not $traceProcess.HasExited) {
    Start-Sleep -Seconds 1

    try {
        $currentProcess = Get-Process -Id $ProcessId -ErrorAction Stop
    }
    catch {
        Write-Warning "PTMagic process $ProcessId exited before profiling completed."
        break
    }

    $sampleTime = Get-Date
    $currentCpuMilliseconds = $currentProcess.TotalProcessorTime.TotalMilliseconds
    $elapsedMilliseconds = ($sampleTime - $previousSampleTime).TotalMilliseconds
    $cpuDeltaMilliseconds = $currentCpuMilliseconds - $previousCpuMilliseconds

    $cpuPercentMachine = 0.0
    if ($elapsedMilliseconds -gt 0 -and $machineCpuCount -gt 0) {
        $cpuPercentMachine = ($cpuDeltaMilliseconds / ($elapsedMilliseconds * $machineCpuCount)) * 100.0
    }

    $metrics.Add([PSCustomObject]@{
        TimestampUtc = $sampleTime.ToUniversalTime().ToString("O")
        CpuPercentOfMachine = [Math]::Round($cpuPercentMachine, 3)
        TotalCpuSeconds = [Math]::Round($currentProcess.TotalProcessorTime.TotalSeconds, 3)
        WorkingSetMB = [Math]::Round($currentProcess.WorkingSet64 / 1MB, 3)
        PrivateMemoryMB = [Math]::Round($currentProcess.PrivateMemorySize64 / 1MB, 3)
        ThreadCount = $currentProcess.Threads.Count
        HandleCount = $currentProcess.HandleCount
    })

    $previousSampleTime = $sampleTime
    $previousCpuMilliseconds = $currentCpuMilliseconds
}

$traceProcess.WaitForExit()

if ($metrics.Count -gt 0) {
    $metrics | Export-Csv -Path $metricsFile -NoTypeInformation -Encoding UTF8
}

if (-not (Test-Path $traceFile)) {
    throw "dotnet-trace did not create the expected trace file: $traceFile"
}

Write-Host "Generating exclusive hot-method report..."
& $dotNetTrace report $traceFile topN --number 100 2>&1 | Set-Content -Path $exclusiveReport -Encoding UTF8
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Unable to generate exclusive topN report. The raw .nettrace file is still available."
}

Write-Host "Generating inclusive hot-method report..."
& $dotNetTrace report $traceFile topN --number 100 --inclusive 2>&1 | Set-Content -Path $inclusiveReport -Encoding UTF8
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Unable to generate inclusive topN report. The raw .nettrace file is still available."
}

if ($metrics.Count -gt 0) {
    $averageCpu = ($metrics | Measure-Object -Property CpuPercentOfMachine -Average).Average
    $peakCpu = ($metrics | Measure-Object -Property CpuPercentOfMachine -Maximum).Maximum
    $averageWorkingSet = ($metrics | Measure-Object -Property WorkingSetMB -Average).Average
    $peakWorkingSet = ($metrics | Measure-Object -Property WorkingSetMB -Maximum).Maximum

    @(
        "",
        "Process metric summary:",
        "Average CPU (% of whole machine): $([Math]::Round($averageCpu, 3))",
        "Peak sampled CPU (% of whole machine): $([Math]::Round($peakCpu, 3))",
        "Average working set (MB): $([Math]::Round($averageWorkingSet, 3))",
        "Peak working set (MB): $([Math]::Round($peakWorkingSet, 3))"
    ) | Add-Content -Path $metadataFile -Encoding UTF8
}

Write-Host ""
Write-Host "Capture complete."
Write-Host "  Raw trace:         $traceFile"
Write-Host "  Process metrics:   $metricsFile"
Write-Host "  Exclusive report:  $exclusiveReport"
Write-Host "  Inclusive report:  $inclusiveReport"
Write-Host "  Profile metadata:  $metadataFile"
Write-Host ""
Write-Host "The exclusive report identifies methods doing work directly; the inclusive report identifies expensive call paths."
Write-Host "Keep the raw .nettrace file because it also contains GC and sampled allocation events from gc-verbose."
