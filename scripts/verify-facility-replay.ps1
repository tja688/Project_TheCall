#Requires -Version 7.3
param([int[]]$Modes=@(0,1,2), [string]$EvidenceDirectory='docs/audit/2026-10-17/facility')
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$evidence=Join-Path $project $EvidenceDirectory
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$recording=Join-Path $project ('Temp/FacilityReplay/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $recording -Force | Out-Null
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
        if($mode -eq 0) {
            $instrumentPath=(Join-Path $evidence 'instrument-transforms.json').Replace('\','/')
            $probe=@"
var samples=0; var maxNeedleError=0f; var maxTankError=0f; var peakLight=0f;
var energyValues=new System.Collections.Generic.HashSet<int>();
var observed=false; var lastFrame=-1;
UnityEditor.EditorApplication.CallbackFunction probe=null;
probe=()=> {
    if(!UnityEngine.Application.isPlaying) {UnityEditor.EditorApplication.update-=probe; return;}
    var show=UnityEngine.Object.FindAnyObjectByType<TheCall.Scoring.ScoringShow>();
    if(show==null) {
        if(!observed) return;
        System.IO.File.WriteAllText("$instrumentPath",Newtonsoft.Json.JsonConvert.SerializeObject(new {samples, distinctEnergyValues=energyValues.Count,maxNeedleError,maxTankError,peakLight},Newtonsoft.Json.Formatting.Indented));
        UnityEditor.EditorApplication.update-=probe; return;
    }
    if(lastFrame==UnityEngine.Time.frameCount) return;
    lastFrame=UnityEngine.Time.frameCount; observed=true;
    var view=UnityEngine.Object.FindAnyObjectByType<TheCall.FacilitySceneView>();
    var due=TheCall.TheCallApp.Interface.SendQuery(new TheCall.LevelTargetQuery()).EnergyDue;
    var ratio=UnityEngine.Mathf.Clamp01(view.PresentedEnergy/UnityEngine.Mathf.Max(1,due));
    var actual=UnityEngine.Mathf.DeltaAngle(0,view.needle.localEulerAngles.z);
    maxNeedleError=UnityEngine.Mathf.Max(maxNeedleError,UnityEngine.Mathf.Abs(actual-UnityEngine.Mathf.Lerp(70,-28,ratio)));
    maxTankError=UnityEngine.Mathf.Max(maxTankError,UnityEngine.Mathf.Abs(view.collectorFluid.localScale.y-UnityEngine.Mathf.Lerp(0.035f,1,ratio)));
    peakLight=UnityEngine.Mathf.Max(peakLight,view.collectorLamp.lightSource.intensity);
    energyValues.Add(UnityEngine.Mathf.RoundToInt(view.PresentedEnergy*1000)); samples++;
};
UnityEditor.EditorApplication.update+=probe;
"@
            Eval $probe | Out-Null
        }
        # Live traces are repeatedly rewritten. Keep their single writer in ignored Temp,
        # away from repository readers that can memory-map text files on Windows.
        $directory=(Join-Path $recording "natural-$mode").Replace('\','/')
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
        $published=Join-Path $evidence "natural-$mode"
        New-Item -ItemType Directory -Path $published -Force | Out-Null
        Get-ChildItem -LiteralPath $directory -File | Copy-Item -Destination $published -Force
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
    if(0 -in $Modes) {
        $instruments=Get-Content -LiteralPath (Join-Path $evidence 'instrument-transforms.json') -Raw | ConvertFrom-Json
        if($instruments.samples -lt 5 -or $instruments.distinctEnergyValues -lt 5 -or $instruments.maxNeedleError -gt 0.001 -or $instruments.maxTankError -gt 0.00001 -or $instruments.peakLight -lt 0.2) {throw 'Actual instrument transforms or receiving light failed'}
    }
    $console=Invoke-Editor 'console' @('--tail','50','--level','error')
    $console | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $evidence 'replay-console.json') -Encoding utf8
    if($console.entries.Count -gt 0) {throw 'Runtime Unity error during recorded player replay'}
    Write-Output "PASS recorded seven-day replay, 125 actual inputs per mode, all states and production records equal pre-art baseline"
} finally {
    Invoke-Editor 'editor_stop' | Out-Null
    & unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
}
