#Requires -Version 7.3
param([switch]$KeepPlaying)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$evidence = Join-Path $project 'docs/audit/2026-10-21/audio'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null

function Invoke-Editor([string]$Name, [string[]]$Arguments = @()) {
    $raw = & unity --no-banner --non-interactive command $Name --project-path $project --caller plugin --skill unity-cli --format json -- @Arguments
    $response = ($raw -join "`n") | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$response.success) { throw ($response | ConvertTo-Json -Depth 8) }
    if ($Name -eq 'eval' -and !$response.data.result.success) { throw ($response.data.result | ConvertTo-Json -Depth 8) }
    return $response.data.result
}
function Eval([string]$Code) { return (Invoke-Editor 'eval' @($Code, '30000')).result }

& unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Editor is not ready.' }
if (Eval 'return UnityEditor.EditorApplication.isPlaying;') { throw 'Exit Play Mode before running acceptance.' }
if (Eval 'return (bool)typeof(UnityEditor.TestTools.TestRunner.Api.TestRunnerApi).GetMethod("IsRunActive", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);') { throw 'An Editor test run is still active. Wait for it to finish before entering Play Mode.' }
$baseline = Invoke-Editor 'console_status'
$runInBackground = Eval 'return UnityEngine.Application.runInBackground;'
Invoke-Editor 'set_autotick' | Out-Null
Eval 'UnityEngine.Application.runInBackground = true;' | Out-Null
Invoke-Editor 'editor_play' | Out-Null
& unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
try {
    $frames = Invoke-Editor 'wait_for' @('--condition', '{"member":"UnityEngine.Time.frameCount","op":"changed"}', '--timeout_s', '10')
    if (!$frames.met) { throw 'Player frames are not advancing.' }
    $path = $evidence.Replace('\', '/')
    Eval "return TheCall.Editor.GameplayAudioAcceptance.Begin(`"$path`");" | Out-Null
    $deadline = (Get-Date).AddMinutes(5)
    do {
        Start-Sleep -Seconds 3
        $progress = Eval 'return TheCall.Editor.GameplayAudioAcceptance.Status;'
        if ((Get-Date) -gt $deadline) { throw 'Audio acceptance timed out.' }
    } while ($progress -eq 'running')
    if ($progress -ne 'completed') { throw "Audio acceptance $progress. See runtime.json and Console." }
    $report = Get-Content (Join-Path $evidence 'runtime.json') -Raw | ConvertFrom-Json
    if (!$report.passed) { throw 'Runtime evidence did not pass.' }
    $errors = Invoke-Editor 'console' @('--level', 'error', '--since', [string]$baseline.cursor, '--since_session', [string]$baseline.session)
    $errors | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $evidence 'console.json')
    if ($errors.entries.Count -gt 0) { throw 'New Unity errors. See console.json.' }
    Write-Output "PASS $($report.checks.Count) gameplay audio checks, $($report.sounds.Count) actual AudioSource events"
}
finally {
    if (!$KeepPlaying) {
        Invoke-Editor 'editor_stop' | Out-Null
        & unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
        Eval "UnityEngine.Application.runInBackground = $($runInBackground.ToString().ToLowerInvariant());" | Out-Null
    }
}
