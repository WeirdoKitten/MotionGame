param([string[]]$Suites = @('PlayMode'))
$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$unityEditor = @($env:UNITY_EDITOR, 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe', 'D:\Unity\6000.6.0f1\Editor\Unity.exe') |
    Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
if (-not $unityEditor) { throw 'Unity 6000.6.0f1 not found. Set UNITY_EDITOR to Unity.exe.' }
foreach ($suite in $Suites) {
    if ($suite -notin @('EditMode', 'PlayMode')) { throw 'Choose EditMode or PlayMode.' }
    $reportPath = Join-Path $projectRoot ('Logs\' + $suite.ToLowerInvariant() + '-results.xml')
    $logPath = Join-Path $projectRoot ('Logs\' + $suite.ToLowerInvariant() + '.log')
    $arguments = @('-batchmode', '-buildTarget', 'Win64', '-projectPath', ('"' + $projectRoot + '"'), '-runTests', '-testPlatform', $suite, '-testResults', ('"' + $reportPath + '"'), '-logFile', ('"' + $logPath + '"'))
    Write-Output ('TEST_START ' + $suite)
    $testProcess = Start-Process -FilePath $unityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $testProcess.WaitForExit()
    if ($testProcess.ExitCode -ne 0) { throw ('Tests failed. See ' + $logPath) }
    [xml]$report = Get-Content -LiteralPath $reportPath
    Write-Output ($suite + ': ' + $report.'test-run'.passed + ' passed, ' + $report.'test-run'.failed + ' failed')
}
