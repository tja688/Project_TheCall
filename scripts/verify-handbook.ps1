param([switch]$KeepPlaying)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$evidence = Join-Path $project 'docs/audit/2026-10-13/handbook'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()

function Invoke-UnityCommand([string]$Name, [string[]]$CommandArguments = @()) {
    $raw = & unity --no-banner --non-interactive command $Name --project-path $project --caller plugin --skill unity-cli --format json -- @CommandArguments
    $response = ($raw -join "`n") | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$response.success) { throw ($response.errors | ConvertTo-Json -Compress) }
    if ($Name -eq 'eval' -and !$response.data.result.success) { throw 'C# eval failed' }
    return $response.data.result
}
function Eval([string]$Code) { return (Invoke-UnityCommand 'eval' @($Code)).result }
function Check([bool]$Success, [string]$Name) {
    if (!$Success) { throw "FAIL: $Name" }
    $checks.Add(@{ check = $Name; result = 'PASS' })
}
function Capture([string]$Name) {
    # Decode the original screen capture directly; no camera-only rendering or image editing.
    $capture = Invoke-UnityCommand 'capture_game_view' @('--source', 'screen', '--width', '1920', '--height', '1080')
    [System.IO.File]::WriteAllBytes((Join-Path $evidence "$Name.png"), [Convert]::FromBase64String($capture.base64))
    $checks.Add(@{ check = "Game View screen capture: $Name"; result = 'PASS'; bytes = $capture.bytes })
}

$status = (& unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json) | ConvertFrom-Json
if (!$status.success -or $status.data.count -ne 1) { throw 'A ready Editor on this project is required.' }
if (Eval 'return UnityEditor.EditorApplication.isPlaying;') { throw 'Exit Play Mode before running the complete saved-scene acceptance.' }
Check ((Eval 'TheCall.Editor.HandbookSceneSetup.ApplyAndSave(); return TheCall.Editor.HandbookAcceptance.ValidateSaved();') -like 'PASS*') 'Saved Main scene references'
$firstHash = (Get-FileHash (Join-Path $project 'Assets/Scenes/Main.unity')).Hash
Eval 'TheCall.Editor.HandbookSceneSetup.ApplyAndSave(); return "saved";' | Out-Null
Check ($firstHash -eq (Get-FileHash (Join-Path $project 'Assets/Scenes/Main.unity')).Hash) 'Authoring repair is idempotent'

& dotnet test (Join-Path $project 'kernel/TheCall.Rules.Tests/TheCall.Rules.Tests.csproj') --no-restore --filter 'FullyQualifiedName~IndexTests' --logger 'trx;LogFileName=handbook.trx' --results-directory (Join-Path $evidence 'test-results')
Check ($LASTEXITCODE -eq 0) 'NUnit handbook and legacy skill index tests'
$baseline = Invoke-UnityCommand 'console_status'
Invoke-UnityCommand 'set_autotick' | Out-Null
Invoke-UnityCommand 'editor_play' | Out-Null
& unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
try {
    $frames = Invoke-UnityCommand 'wait_for' @('--condition', '{"member":"UnityEngine.Time.frameCount","op":"changed"}', '--timeout_s', '10')
    Check $frames.met 'Player frames advance'
    Eval 'System.Linq.Enumerable.First(UnityEngine.Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>(),w=>w.GetType().Name=="GameView").Focus();' | Out-Null
    $checks.Add(@{ check = 'Runtime contracts'; result = (Eval 'return TheCall.Editor.HandbookAcceptance.Run();') })
    Eval 'return TheCall.Editor.HandbookAcceptance.ClickVisible("SettingsButton");' | Out-Null
    Eval 'var c=UnityEngine.Object.FindAnyObjectByType<TheCall.SettingsChromeView>(); if(UnityEngine.Resources.Load<TheCall.GameAudioCatalog>("GameAudioCatalog")?.mixer==null) throw new System.Exception("Mixer missing"); UnityEditor.SessionState.SetFloat("Handbook.Acceptance.Master",TheCall.GameAudioSettings.MasterVolume); c.masterVolume.value=.37f;' | Out-Null
    Check (Eval 'var c=UnityEngine.Object.FindAnyObjectByType<TheCall.SettingsChromeView>(); var mixer=UnityEngine.Resources.Load<TheCall.GameAudioCatalog>("GameAudioCatalog").mixer; mixer.GetFloat("MasterVolume",out var db); c.masterVolume.value=UnityEditor.SessionState.GetFloat("Handbook.Acceptance.Master",1f); var music=TheCall.GameAudioSettings.MusicVolume; var sfx=TheCall.GameAudioSettings.SfxVolume; c.musicVolume.value=.42f; c.sfxVolume.value=.63f; var wired=System.Math.Abs(TheCall.GameAudioSettings.MusicVolume-.42f)<.001f && System.Math.Abs(TheCall.GameAudioSettings.SfxVolume-.63f)<.001f; c.musicVolume.value=music; c.sfxVolume.value=sfx; return wired && System.Math.Abs(db-20f*UnityEngine.Mathf.Log10(.37f))<.02f;') 'Audio sliders reach settings and live mixer; preferences restored'
    Capture 'settings'
    Eval 'return TheCall.Editor.HandbookAcceptance.ClickVisible("HandbookButton");' | Out-Null
    Capture 'guide'
    Eval 'return TheCall.Editor.HandbookAcceptance.ClickVisible("Category_4");' | Out-Null
    Eval 'return TheCall.Editor.HandbookAcceptance.ClickVisible("Entry_Mood:热爱");' | Out-Null
    Capture 'mood'
    Eval 'return TheCall.Editor.HandbookAcceptance.ClickVisible("Category_2");' | Out-Null
    Eval 'return TheCall.Editor.HandbookAcceptance.ClickVisible("Rarity_4");' | Out-Null
    Capture 'negative-personalities'
    Eval 'return TheCall.Editor.HandbookAcceptance.ClickVisible("Category_5");' | Out-Null
    Eval 'var b=UnityEngine.Object.FindAnyObjectByType<TheCall.SettingsChromeView>().codex; b.search.text="五行罗盘";' | Out-Null
    Eval 'return TheCall.Editor.HandbookAcceptance.ClickVisible("Entry_Tool:五行罗盘");' | Out-Null
    Capture 'tool'
    Eval 'return TheCall.Editor.HandbookAcceptance.ClickVisible("Category_1");' | Out-Null
    Eval 'var b=UnityEngine.Object.FindAnyObjectByType<TheCall.SettingsChromeView>().codex; System.Linq.Enumerable.First(UnityEngine.Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>(),w=>w.GetType().Name=="GameView").Focus(); UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(b.search.gameObject); b.search.ActivateInputField();' | Out-Null
    Eval 'var w=System.Linq.Enumerable.First(UnityEngine.Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>(),w=>w.GetType().Name=="GameView"); w.Focus(); foreach(var ch in "能量体") w.SendEvent(new UnityEngine.Event {type=UnityEngine.EventType.KeyDown,character=ch,keyCode=UnityEngine.KeyCode.None});' | Out-Null
    Check (Eval 'var b=UnityEngine.Object.FindAnyObjectByType<TheCall.SettingsChromeView>().codex; var count=System.Linq.Enumerable.Count(TheCall.TheCallApp.Interface.SendQuery(new TheCall.HandbookIndexQuery()),e=>e.Category==TheCall.HandbookCategory.Skill && e.Matches("能量体")); return b.search.text=="能量体" && b.VisibleCount==count;') 'Typed Chinese search reaches filtered results'
    Capture 'skill-search'
    Eval 'System.Linq.Enumerable.First(UnityEngine.Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>(),w=>w.GetType().Name=="GameView").Focus(); UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,new UnityEngine.InputSystem.LowLevel.KeyboardState());' | Out-Null
    Eval 'UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape));' | Out-Null
    Check (Eval 'UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,new UnityEngine.InputSystem.LowLevel.KeyboardState()); var c=UnityEngine.Object.FindAnyObjectByType<TheCall.SettingsChromeView>(); return c.SettingsOpen && !c.CodexOpen;') 'Escape returns from handbook to settings'
    Eval 'System.Linq.Enumerable.First(UnityEngine.Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>(),w=>w.GetType().Name=="GameView").Focus();' | Out-Null
    Eval 'UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape));' | Out-Null
    Check (Eval 'UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,new UnityEngine.InputSystem.LowLevel.KeyboardState()); return !UnityEngine.Object.FindAnyObjectByType<TheCall.SettingsChromeView>().SettingsOpen;') 'Escape closes settings'
    Eval 'return TheCall.Editor.HandbookAcceptance.ClickVisible("Candidate_0");' | Out-Null
    Check (Eval 'return TheCall.TheCallApp.Interface.SendQuery(new TheCall.RunPhaseQuery()) == TheCall.RunPhase.Operation;') 'Natural opening selection enters operation'
    Eval 'return TheCall.Editor.HandbookAcceptance.ClickVisible("SettingsButton");' | Out-Null
    Eval 'return TheCall.Editor.HandbookAcceptance.ClickVisible("HandbookButton");' | Out-Null
    Check (Eval 'return UnityEngine.Object.FindAnyObjectByType<TheCall.SettingsChromeView>().CodexOpen;') 'Operation visible settings and handbook'
    Eval 'var c=UnityEngine.Object.FindAnyObjectByType<TheCall.SettingsChromeView>(); c.codex.closeButton.onClick.Invoke(); c.settingsCloseButton.onClick.Invoke(); return TheCall.Editor.HandbookAcceptance.ClickVisible("ResearchButton");' | Out-Null
    Eval 'return TheCall.Editor.HandbookAcceptance.ClickVisible("SettingsButton");' | Out-Null
    Capture 'research-settings'
    Eval 'UnityEngine.Object.FindAnyObjectByType<TheCall.SettingsChromeView>().settingsCloseButton.onClick.Invoke(); return TheCall.Editor.HandbookAcceptance.ClickVisible("BackButton");' | Out-Null
    Check (Eval 'return UnityEngine.Object.FindAnyObjectByType<TheCall.OperationScreenView>().gameObject.activeInHierarchy;') 'Research settings and original back button remain reachable'
    $console = Invoke-UnityCommand 'console' @('--level', 'error', '--since', [string]$baseline.cursor, '--since_session', [string]$baseline.session)
    Check ($console.entries.Count -eq 0 -and !$console.groundTruth.compilationFailed) 'No new Unity console errors'
    $checks | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $evidence 'acceptance.json') -Encoding utf8
    Write-Output "PASS. Evidence: $evidence"
}
finally {
    if (!$KeepPlaying) {
        Invoke-UnityCommand 'editor_stop' | Out-Null
        & unity --no-banner --non-interactive status --until-ready --project-path $project --timeout 300 --format json | Out-Null
    }
}
