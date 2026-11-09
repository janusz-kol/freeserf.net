<#
.SYNOPSIS
    Builds and runs WindowsDebug with full Verbose logging.
.DESCRIPTION
    Launch run-debug.bat on Windows 10 or later with the .NET 9 SDK installed.
    Build and game output (including stderr) is displayed and saved under logs/.
    Each run gets separate timestamped files. Game data must be installed as usual.
#>
$ErrorActionPreference = 'Stop'
$exitCode = 1

Push-Location $PSScriptRoot
try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'The .NET 9 SDK is required. Install it and run this script again.'
    }

    $logDirectory = Join-Path $PSScriptRoot 'logs'
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    $stamp = (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + $PID
    $buildLog = Join-Path $logDirectory ("build-debug-$stamp.log")
    $gameLog = Join-Path $logDirectory ("freeserf-debug-$stamp.log")
    $project = Join-Path $PSScriptRoot 'FreeserfNet\FreeserfNet.csproj'
    $executable = Join-Path $PSScriptRoot 'FreeserfNet\bin\WindowsDebug\FreeserfNet.exe'

    Write-Host "Build log: $buildLog"
    # Windows PowerShell 5.1 treats native stderr as error records. Keep logging
    # those records and use the native exit code to decide whether the build failed.
    $ErrorActionPreference = 'Continue'
    & dotnet build $project -c WindowsDebug --nologo 2>&1 | Tee-Object -FilePath $buildLog
    $buildExitCode = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($buildExitCode -ne 0) {
        $exitCode = $buildExitCode
        throw "Debug build failed (exit code $buildExitCode). See $buildLog"
    }
    if (-not (Test-Path -LiteralPath $executable)) {
        throw "Built executable not found: $executable"
    }

    Write-Host "Game log: $gameLog"
    "Started: $(Get-Date -Format o)`r`nExecutable: $executable`r`nArguments: -d 0 -c" |
        Tee-Object -FilePath $gameLog
    $ErrorActionPreference = 'Continue'
    # -d 0 selects Verbose; -c selects console output, captured without a size limit.
    & $executable -d 0 -c 2>&1 | Tee-Object -FilePath $gameLog -Append
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    "Exited: $(Get-Date -Format o), code: $exitCode" |
        Tee-Object -FilePath $gameLog -Append
}
catch {
    Write-Host $_ -ForegroundColor Red
}
finally {
    Pop-Location
}
exit $exitCode
