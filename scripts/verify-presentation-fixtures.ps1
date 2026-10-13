#Requires -Version 7.3
param()
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$evidence = (Join-Path $project 'docs/audit/2026-10-13/architecture/ui-fixtures').Replace('\', '/')
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()

function Invoke-Editor([string]$Name, [string[]]$Arguments = @()) {
    $raw = & unity --no-banner --non-interactive command $Name --project-path $project --caller plugin --skill unity-cli --format json -- @Arguments
    $response = ($raw -join "`n") | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$response.success) { throw "$Name $($Arguments[0]): $($response | ConvertTo-Json -Depth 8)" }
    if ($Name -eq 'eval' -and !$response.data.result.success) { throw ($response.data.result | ConvertTo-Json -Depth 8) }
    return $response.data.result
}
function Eval([string]$Code) { return (Invoke-Editor 'eval' @($Code, '30000')).result }
function Step([string]$Code) {
    $value = Eval $Code
    if ($value -notmatch 'PASS') { throw "No PASS verdict: $value" }
    $checks.Add(@{ kind = 'LOCAL_UI_FIXTURE_NOT_NATURAL_RUN'; result = $value })
    Write-Output $value
    Invoke-Editor 'wait_for' @('--condition', '{"member":"UnityEngine.Time.frameCount","op":"changed"}', '--timeout_s', '10') | Out-Null
}
function Begin([string]$Kind, [int]$Count) {
    Step "return TheCall.Editor.PresentationFixtureAcceptance.Begin(`"$Kind`",$Count,`"$evidence`");"
}
function Capture([string]$Name) {
    $capture = Invoke-Editor 'capture_game_view' @('--source', 'screen', '--width', '1920', '--height', '1080')
    [IO.File]::WriteAllBytes((Join-Path $evidence "$Name.png"), [Convert]::FromBase64String($capture.base64))
}

if (Eval 'return UnityEditor.EditorApplication.isPlaying;') { throw 'Exit Play Mode before running fixtures.' }
$baseline = Invoke-Editor 'console_status'
Invoke-Editor 'set_autotick' | Out-Null
Invoke-Editor 'editor_play' | Out-Null
& unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
try {
    Begin 'tools' 18
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.CheckTools(18);'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.HoverTool(17);'
    Capture 'tools-18'
    Begin 'inventory' 30
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.CheckInventory(30);'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.DragInventory(29,0);'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.ReturnExtraction(0);'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.CheckInventory(30,true);'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.SellInventory(29);'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.CheckInventory(29,true);'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.SellInventory(0);'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.CheckInventory(28,true);'
    Capture 'sell-inventory'
    Begin 'install' 1
    Step 'var op=UnityEngine.Object.FindAnyObjectByType<TheCall.OperationScreenView>(); var chip=op.skillChips[0]; var button=chip.button; var label=chip.label; try { chip.button=null; chip.label=null; return TheCall.Editor.PresentationFixtureAcceptance.DragGene(0,"monster",0,true); } finally { chip.button=button; chip.label=label; UnityEngine.Object.FindAnyObjectByType<TheCall.TheCallPresentation>().Refresh(); }'
    Capture 'gene-install-no-controls'
    Begin 'reject' 1
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.DragGene(0,"monster",0,false);'
    Begin 'capacity' 6
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.Research("基因实验");'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.BackResearch();'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.DragParent(0,0);'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.DragParent(0,1);'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.PrepareGenes(3);'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.DragGene(0,"breeding",0,true);'
    Capture 'gene-reserved'
    Step 'return TheCall.Editor.PresentationFixtureAcceptance.ReturnGene(0);'
    Capture 'gene-returned'
    $console = Invoke-Editor 'console' @('--level', 'error', '--since', [string]$baseline.cursor, '--since_session', [string]$baseline.session)
    if ($console.entries.Count -ne 0 -or $console.groundTruth.compilationFailed) { throw 'New Unity errors in local fixtures.' }
    $checks | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $evidence 'acceptance.json') -Encoding utf8
    Write-Output "PASS. Local fixture evidence: $evidence"
}
finally {
    Invoke-Editor 'editor_stop' | Out-Null
    & unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
}
