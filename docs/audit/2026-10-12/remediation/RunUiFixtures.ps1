param(
    [ValidateSet('All', 'Assets', 'Play')][string]$Phase = 'All',
    [string]$ProjectRoot = 'C:/Users/jinji/Documents/GitHub/Project_TheCall'
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath($ProjectRoot)
$evidenceRoot = Join-Path $projectRoot 'docs/audit/2026-10-12/remediation/evidence/ui-fixtures'
$logRoot = Join-Path $evidenceRoot ('cli-' + (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ'))
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$script:stepNumber = 0
$script:screenshots = [Collections.Generic.List[object]]::new()
$script:fixtureType = 'LOCAL_UI_FIXTURE_NOT_NATURAL_RUN'
$book = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets/StreamingAssets/call-book.json') -Raw | ConvertFrom-Json
if ($book.techs.Count -ne 6) { throw 'Formal content must contain six research definitions.' }
$fixtureClass = 'TheCall.Editor.PresentationFixtureAcceptance'

function Quote-CSharp([string]$Value) {
    return '"' + $Value.Replace('\', '\\').Replace('"', '\"').Replace("`r", '\r').Replace("`n", '\n') + '"'
}
function Invoke-EditorEval([string]$Method, [string]$Code) {
    $script:stepNumber++
    $safeName = $Method -replace '[^A-Za-z0-9_-]', '_'
    $path = Join-Path $logRoot ('{0:D4}-{1}.json' -f $script:stepNumber, $safeName)
    $started = (Get-Date).ToUniversalTime().ToString('o')
    $raw = $null
    $parsed = $null
    try {
        $raw = (& unity --no-banner --non-interactive command eval --project-path $projectRoot --caller plugin --skill unity-cli --format json -- $Code 30000 2>&1 | Out-String)
        $exitCode = $LASTEXITCODE
        $parsed = $raw | ConvertFrom-Json
        $envelope = [ordered]@{ kind = $script:fixtureType; method = $Method; code = $Code; startedUtc = $started; exitCode = $exitCode; response = $parsed; raw = $raw }
        $envelope | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $path -Encoding utf8
        if ($exitCode -ne 0 -or -not $parsed.success -or -not $parsed.data.result.success) {
            throw ('Editor eval rejected or failed: ' + ($parsed | ConvertTo-Json -Depth 15))
        }
        $value = $parsed.data.result.result
        Write-Host ("[$($script:stepNumber)] $Method : $value")
        return $value
    }
    catch {
        [ordered]@{ kind = $script:fixtureType; method = $Method; code = $Code; startedUtc = $started; failed = $true; error = $_.ToString(); response = $parsed; raw = $raw } |
            ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $path -Encoding utf8
        [ordered]@{ kind = $script:fixtureType; failedMethod = $Method; code = $Code; log = $path; error = $_.ToString() } |
            ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $logRoot 'FAILED.json') -Encoding utf8
        throw "UI fixture stopped at '$Method'. Concrete code: $Code. Error: $($_.Exception.Message). Saved log: $path"
    }
}
function Call([string]$Method, [string]$Arguments = '') {
    return Invoke-EditorEval $Method "return $fixtureClass.$Method($Arguments);"
}
function Wait-Frame([string]$Reason) {
    $start = [int](Invoke-EditorEval "FrameStart-$Reason" 'return UnityEngine.Time.frameCount;')
    $deadline = (Get-Date).AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 150
        $frame = [int](Invoke-EditorEval "FrameAdvance-$Reason" 'return UnityEngine.Time.frameCount;')
        if ($frame -gt $start) { return $frame }
    } while ((Get-Date) -lt $deadline)
    throw "Actual Play Mode frame did not advance for $Reason (frame=$start)."
}
function Player-Step([string]$Method, [string]$Arguments = '') {
    $value = Call $Method $Arguments
    Wait-Frame $Method | Out-Null
    return $value
}
function Capture([string]$Filename) {
    Wait-Frame "BeforeCapture-$Filename" | Out-Null
    $path = [string](Call 'Capture' (Quote-CSharp $Filename))
    Wait-Frame "AfterCapture-$Filename" | Out-Null
    $deadline = (Get-Date).AddSeconds(20)
    while ((-not (Test-Path -LiteralPath $path)) -or (Get-Item -LiteralPath $path).Length -lt 24) {
        if ((Get-Date) -gt $deadline) { throw "End-of-frame screenshot not written: $path" }
        Start-Sleep -Milliseconds 150
        Wait-Frame "WaitCapture-$Filename" | Out-Null
    }
    $bytes = [IO.File]::ReadAllBytes($path)
    if ($bytes[0] -ne 137 -or $bytes[1] -ne 80 -or $bytes[2] -ne 78 -or $bytes[3] -ne 71) { throw "Capture is not a PNG: $path" }
    $width = [int]$bytes[16] * 16777216 + [int]$bytes[17] * 65536 + [int]$bytes[18] * 256 + [int]$bytes[19]
    $height = [int]$bytes[20] * 16777216 + [int]$bytes[21] * 65536 + [int]$bytes[22] * 256 + [int]$bytes[23]
    if ($width -le 0 -or $height -le 0) { throw "PNG has empty dimensions: $path" }
    $script:screenshots.Add([ordered]@{ filename = $Filename; path = $path; width = $width; height = $height; bytes = $bytes.Length; sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash; visualReview = 'pending' })
    $script:screenshots | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $logRoot 'screenshots.json') -Encoding utf8
}
function Wait-PlayState([bool]$Expected) {
    $deadline = (Get-Date).AddSeconds(45)
    do {
        Start-Sleep -Milliseconds 250
        $actual = Invoke-EditorEval 'PlayState' 'return UnityEditor.EditorApplication.isPlaying && !UnityEditor.EditorApplication.isCompiling;'
        if ([bool]$actual -eq $Expected) { return }
    } while ((Get-Date) -lt $deadline)
    throw "Editor did not reach expected isPlaying=$Expected."
}

# Inspect the existing instance; do not open another Editor.
& unity --version | Set-Content -LiteralPath (Join-Path $logRoot 'unity-version.txt') -Encoding utf8
$statusRaw = & unity --no-banner --non-interactive status --format json --project-path $projectRoot | Out-String
$statusRaw | Set-Content -LiteralPath (Join-Path $logRoot 'initial-status.json') -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Existing Editor status unavailable; fixture runner will not open a duplicate Editor.' }
$planState = [string](Invoke-EditorEval 'NaturalPlanGuard' 'return TheCall.Editor.M5PlayModeAutomation.PlanStatus;')
if ($planState -match '^running') { throw "Natural player plan is still running: $planState. Finish and archive it before local fixtures." }

if ($Phase -ne 'Play') {
    Invoke-EditorEval 'ExitPlayModeForAssets' 'UnityEditor.EditorApplication.isPlaying = false; return "requested non-Play Mode";' | Out-Null
    Wait-PlayState $false
    Invoke-EditorEval 'Apply' 'return TheCall.Editor.PresentationRemediation.Apply();' | Out-Null
    Call 'Idempotency' | Out-Null
    Call 'Assets' | Out-Null
}
if ($Phase -eq 'Assets') { Write-Output "Assets fixture completed. Logs: $logRoot"; exit 0 }
Invoke-EditorEval 'EnterPlayModeForFixtures' 'UnityEditor.EditorApplication.isPlaying = true; return "requested Play Mode";' | Out-Null
Wait-PlayState $true
Wait-Frame 'FixturesStart' | Out-Null

foreach ($count in @(1, 3, 6, 18)) {
    Player-Step 'Begin' ('"tools",' + $count) | Out-Null
    Player-Step 'CheckTools' "$count" | Out-Null
    Player-Step 'HoverTool' "$($count - 1)" | Out-Null
    Capture "tools-$count.png"
}
foreach ($count in @(19, 30)) {
    Player-Step 'Begin' ('"inventory",' + $count) | Out-Null
    Player-Step 'CheckInventory' "$count" | Out-Null
    Player-Step 'DragInventory' "$($count - 1),0" | Out-Null
    Capture "inventory-$count-last-drag.png"
    Player-Step 'ReturnExtraction' '0' | Out-Null
    Player-Step 'DragInventory' '0,1' | Out-Null
    Player-Step 'ReturnExtraction' '1' | Out-Null
    Capture "inventory-$count-return.png"
    Player-Step 'CheckInventory' "$count,true" | Out-Null
    Player-Step 'SellInventory' "$($count - 1)" | Out-Null
    Player-Step 'CheckInventory' "$($count - 1),true" | Out-Null
    Capture "sell-$count-last.png"
    Player-Step 'SellInventory' '0' | Out-Null
    Player-Step 'CheckInventory' "$($count - 2),true" | Out-Null
    Capture "sell-$count-first.png"
}
Player-Step 'Begin' '"capacity",12' | Out-Null
foreach ($tech in $book.techs) {
    $name = [string]$tech.name
    Player-Step 'Research' ((Quote-CSharp $name) + ',true') | Out-Null
    Capture ('research-' + $name + '.png')
    Player-Step 'Research' ((Quote-CSharp $name) + ',false') | Out-Null
}
Player-Step 'BackResearch' | Out-Null
Player-Step 'CheckCapacity' | Out-Null
for ($seat = 0; $seat -lt 6; $seat++) { Player-Step 'DragParent' "0,$seat" | Out-Null }
Player-Step 'PrepareGenes' '3' | Out-Null
Player-Step 'DragGene' '0,"breeding",0,true' | Out-Null
Capture 'capacity-reserved-three.png'
Player-Step 'Discard' '0,true' | Out-Null
Capture 'capacity-fourth-gene.png'
Player-Step 'Discard' '0,false' | Out-Null
Capture 'capacity-full-reserved-rejection.png'
Player-Step 'ReturnGene' '0' | Out-Null
Capture 'capacity-original-slot-return.png'
Player-Step 'Begin' '"reject",1' | Out-Null
Player-Step 'Research' ((Quote-CSharp ([string]$book.techs[0].name)) + ',false') | Out-Null
Capture 'research-insufficient.png'
Player-Step 'BackResearch' | Out-Null
foreach ($kind in @('install', 'reject', 'full', 'duplicate')) {
    Player-Step 'Begin' ((Quote-CSharp $kind) + ',1') | Out-Null
    $success = if ($kind -eq 'install') { 'true' } else { 'false' }
    Player-Step 'DragGene' "0,`"monster`",0,$success" | Out-Null
    Capture "install-$kind.png"
    if ($kind -eq 'duplicate') {
        Player-Step 'EnableDuplicateFixture' | Out-Null
        Player-Step 'DragGene' '0,"monster",0,true' | Out-Null
        Capture 'install-duplicate-permitted.png'
    }
}
foreach ($category in @('personality', 'race', 'mood')) {
    $count = switch ($category) { 'personality' { $book.personalities.Count } 'race' { $book.races.Count } 'mood' { $book.moods.Count } }
    for ($index = 0; $index -lt $count; $index++) {
        Player-Step 'Consumer' ((Quote-CSharp $category) + ',' + $index) | Out-Null
        Capture ('consumer-{0}-{1:D2}.png' -f $category, $index)
    }
}
for ($index = 0; $index -lt $book.skills.Count; $index++) {
    Player-Step 'SkillConsumer' "$index" | Out-Null
    Capture ('consumer-skill-{0:D2}.png' -f $index)
}
Player-Step 'Begin' '"details",1' | Out-Null
foreach ($corner in @('top-left', 'top-right', 'bottom-left', 'bottom-right')) {
    Player-Step 'DetailsAt' (Quote-CSharp $corner) | Out-Null
    Capture "details-$corner.png"
}
Player-Step 'HideDetails' | Out-Null
Invoke-EditorEval 'ExitPlayModeAfterFixtures' 'UnityEditor.EditorApplication.isPlaying = false; return "requested non-Play Mode";' | Out-Null
Wait-PlayState $false
Invoke-EditorEval 'ValidateFinalAssets' 'return TheCall.Editor.PresentationRemediation.ValidateSaved();' | Out-Null
$duplicates = @($script:screenshots | Group-Object sha256 | Where-Object Count -gt 1 | ForEach-Object { [ordered]@{ sha256 = $_.Name; filenames = @($_.Group.filename) } })
[ordered]@{ kind = $script:fixtureType; success = $true; evalCalls = $script:stepNumber; screenshotCount = $script:screenshots.Count; duplicateHashes = $duplicates; visualReview = 'Required: inspect each screenshot; file/hash/consumer checks do not prove readable pixels.'; logs = $logRoot } |
    ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $logRoot 'COMPLETED.json') -Encoding utf8
Write-Output "UI fixture assertions completed, screenshots require visual review. Logs: $logRoot"
