# Runs the Unity tests headless and prints a summary.
#   powershell -ExecutionPolicy Bypass -File Tools/run-unity-tests.ps1 [-TestPlatform PlayMode] [-Graphics] [-TestFilter Name] [-UnityVersion 6000.3.25f1]
# Reads the editor version from ProjectSettings/ProjectVersion.txt when not given.
# -Graphics keeps the GPU on (no -nographics), so PlayMode smoke tests can save screenshots to Logs/screenshots.
# -TestFilter runs only matching tests (full or partial names); naming an [Explicit] test runs it.
# Exit codes follow Unity: 0 = all passed, 2 = test failures, 1 = errors (compile, licence, ...).
param(
    [string]$UnityVersion,
    [string]$TestPlatform = "EditMode",
    [switch]$Graphics,
    [string]$TestFilter
)

$ErrorActionPreference = "Stop"
$project = Resolve-Path (Join-Path $PSScriptRoot "..")

if (-not $UnityVersion) {
    $versionFile = Join-Path $project "ProjectSettings/ProjectVersion.txt"
    if (-not (Test-Path $versionFile)) { throw "No ProjectSettings/ProjectVersion.txt. Run Tools/setup/adopt-unity-project.mjs first, or pass -UnityVersion." }
    $UnityVersion = ((Get-Content $versionFile | Select-String "m_EditorVersion:").ToString() -split ":\s*")[1].Trim()
}

$unity = "C:\Program Files\Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe"
if (-not (Test-Path $unity)) { throw "Unity $UnityVersion not found at $unity. Install it from Unity Hub." }

$results = Join-Path $project "Logs/test-results-$TestPlatform.xml"
$log = Join-Path $project "Logs/test-run-$TestPlatform.log"
New-Item -ItemType Directory -Force (Split-Path $results) | Out-Null
if (Test-Path -LiteralPath $results) { [System.IO.File]::Delete($results) } # never report a stale run

Write-Host "Running $TestPlatform tests with Unity $UnityVersion ..."
$unityArgs = @("-batchmode")
if (-not $Graphics) { $unityArgs += "-nographics" }
$unityArgs += @("-projectPath", "$project", "-runTests", "-testPlatform", $TestPlatform, "-testResults", $results, "-logFile", $log)
if ($TestFilter) { $unityArgs += @("-testFilter", $TestFilter) }
# Wait for Unity itself: -Wait would also wait for children it leaves behind (shader compilers idle for minutes).
$process = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru -NoNewWindow
$null = $process.Handle # PS 5.1 quirk: keeps ExitCode readable after the process exits
$process.WaitForExit()
$code = $process.ExitCode

if (Test-Path $results) {
    [xml]$xml = Get-Content $results
    $run = $xml.'test-run'
    Write-Host ("Result: {0} - {1} passed, {2} failed, {3} skipped (total {4})" -f $run.result, $run.passed, $run.failed, $run.skipped, $run.total)
    foreach ($case in $xml.SelectNodes("//test-case[@result='Failed']")) {
        Write-Host "  FAILED $($case.fullname)" -ForegroundColor Red
        Write-Host "    $($case.failure.message.'#cdata-section')"
    }
} else {
    Write-Host "No results file. Check the log: $log" -ForegroundColor Yellow
    Get-Content $log -Tail 40 | Select-String -Pattern "error|licen" | ForEach-Object { Write-Host "  $_" }
}

exit $code
