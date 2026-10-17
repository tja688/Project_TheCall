#Requires -Version 7.3
param([int[]]$Modes=@(0,1,2))
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$evidence=Join-Path $project 'docs/audit/2026-10-17/facility'
function Invoke-Editor([string]$Name,[string[]]$Arguments=@()) {
    $raw=& unity --no-banner --non-interactive command $Name --project-path $project --caller plugin --skill unity-cli --format json -- @Arguments
    $response=($raw -join "`n") | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$response.success) {throw ($response | ConvertTo-Json -Depth 8)}
    if ($Name -eq 'eval' -and !$response.data.result.success) {throw ($response.data.result | ConvertTo-Json -Depth 8)}
    return $response.data.result
}
function Eval([string]$code) {(Invoke-Editor 'eval' @($code,'30000')).result}
if (Eval 'return UnityEditor.EditorApplication.isPlaying;') {throw 'Exit Play Mode before the independent replay.'}
Invoke-Editor 'set_autotick' | Out-Null
Invoke-Editor 'clear_console' | Out-Null
Eval 'UnityEngine.Application.runInBackground=true;' | Out-Null
Invoke-Editor 'editor_play' | Out-Null
& unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
$plan=(Join-Path $project 'docs/audit/2026-10-12/remediation/evidence/natural-plan.json').Replace('\','/')
try {
    foreach($mode in $Modes) {
        if($mode -notin 0,1,2) {throw 'Modes must be 0, 1 or 2'}
        $directory=(Join-Path $evidence "natural-$mode").Replace('\','/')
        Eval "return TheCall.Editor.M5PlayModeAutomation.BeginPlan(`"$plan`",$mode,`"$directory`");" | Out-Null
        $deadline=(Get-Date).AddMinutes(20); $previous=''
        do {
            Start-Sleep -Seconds 5
            $progress=Eval 'return TheCall.Editor.M5PlayModeAutomation.PlanStatus;'
            if($progress -ne $previous) {Write-Output "mode=$mode $progress"; $previous=$progress}
            if($progress -notmatch '^running|^completed') {throw $progress}
            if((Get-Date) -gt $deadline) {throw "Replay timeout mode=$mode"}
        } while($progress -match '^running')
        if($progress -ne 'completed 125') {throw "Wrong input count: $progress"}
        $oldFailure=Join-Path $directory 'player-replay-failure.txt'
        if(Test-Path -LiteralPath $oldFailure) {
            $archive=Join-Path $project 'Temp/FacilityArt/previous-replay-failures'
            New-Item -ItemType Directory -Path $archive -Force | Out-Null
            Move-Item -LiteralPath $oldFailure -Destination (Join-Path $archive "mode-$mode.txt") -Force
        }
    }
    $comparison=[Collections.Generic.List[object]]::new()
    $baseline=Join-Path $project 'docs/audit/2026-10-15/lab/natural-0'
    foreach($mode in $Modes) {
        foreach($file in 'final-state.txt','production-log.txt','state-trace.txt') {
            $path=Join-Path $evidence "natural-$mode/$file"
            $prior=Join-Path $baseline $file
            if(!(Test-Path $prior)) {throw "Missing recorded baseline: $prior"}
            $actual=[IO.File]::ReadAllText($path)
            $expected=[IO.File]::ReadAllText($prior)
            $match=$actual -ceq $expected
            $comparison.Add(@{mode=$mode;file=$file;matches=$match;sha256=(Get-FileHash $path).Hash})
            if(!$match) {throw "Replay differs from pre-integration baseline: mode=$mode $file"}
        }
    }
    $comparison | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $evidence 'replay-comparison.json') -Encoding utf8
    $console=Invoke-Editor 'console' @('--tail','50','--level','error')
    $console | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $evidence 'replay-console.json') -Encoding utf8
    if($console.entries.Count -gt 0) {throw 'Runtime Unity error during recorded player replay'}
    Write-Output "PASS recorded seven-day replay, 125 actual inputs per mode, all states and production records equal pre-art baseline"
} finally {
    Invoke-Editor 'editor_stop' | Out-Null
    & unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
}
