#Requires -Version 5.1
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot,

    [int]$TimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'

# Pipeline 0.8+ expects a recent Unity CLI (loopback on 127.0.0.1, native JSON status, etc.).
$MinimumCliVersionLabel = '1.0.0-beta.13'

function Test-UnityCliVersionAtLeast {
    param(
        [string]$Actual,
        [string]$Minimum
    )

    $pattern = '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-(?<prerelease>.+))?$'
    if ($Actual -notmatch $pattern -or $Minimum -notmatch $pattern) {
        return $null
    }

    $actualParts = [ordered]@{
        Major = [int]$Matches.major
        Minor = [int]$Matches.minor
        Patch = [int]$Matches.patch
        Prerelease = $Matches.prerelease
    }

    $null = $Minimum -match $pattern
    $minimumParts = [ordered]@{
        Major = [int]$Matches.major
        Minor = [int]$Matches.minor
        Patch = [int]$Matches.patch
        Prerelease = $Matches.prerelease
    }

    foreach ($key in @('Major', 'Minor', 'Patch')) {
        if ($actualParts[$key] -ne $minimumParts[$key]) {
            return ($actualParts[$key] -gt $minimumParts[$key])
        }
    }

    if (-not $actualParts.Prerelease -and -not $minimumParts.Prerelease) {
        return $true
    }

    if ($actualParts.Prerelease -and -not $minimumParts.Prerelease) {
        return $false
    }

    if (-not $actualParts.Prerelease -and $minimumParts.Prerelease) {
        return $true
    }

    $actualBeta = $null
    $minimumBeta = $null
    if ($actualParts.Prerelease -match '^beta\.(\d+)$') {
        $actualBeta = [int]$Matches[1]
    }

    if ($minimumParts.Prerelease -match '^beta\.(\d+)$') {
        $minimumBeta = [int]$Matches[1]
    }

    if ($null -ne $actualBeta -and $null -ne $minimumBeta) {
        return ($actualBeta -ge $minimumBeta)
    }

    return $null
}

function Get-UnityJsonOutput {
    param([string[]]$UnityArgs)

    $raw = & unity @UnityArgs 2>&1 | ForEach-Object { $_.ToString() }
    $text = ($raw -join [Environment]::NewLine).Trim()
    if (-not $text) {
        return $null
    }

    $start = $text.IndexOf('{')
    if ($start -lt 0) {
        throw "Unity CLI 未返回 JSON:`n$text"
    }

    return ($text.Substring($start) | ConvertFrom-Json)
}

function Get-NormalizedPath {
    param([string]$Path)
    return [System.IO.Path]::GetFullPath($Path.TrimEnd('\', '/'))
}

function Assert-UnityCliVersion {
    $versionLine = (& unity --version 2>&1 | Select-Object -First 1).ToString().Trim()
    if (-not $versionLine) {
        throw '未找到 unity CLI。请安装 Unity CLI 并确保其在 PATH 中。'
    }

    $ok = Test-UnityCliVersionAtLeast -Actual $versionLine -Minimum $MinimumCliVersionLabel
    if ($null -eq $ok) {
        Write-Warning "无法比较 CLI 版本 ($versionLine)，请确认已 >= $MinimumCliVersionLabel。"
        return
    }

    if (-not $ok) {
        Write-Host "[错误] Unity CLI 版本过旧 ($versionLine)，Pipeline 0.8 需要至少 $MinimumCliVersionLabel。请运行: unity upgrade -y" -ForegroundColor Red
        exit 2
    }
}

function Remove-StaleUnityCliRollback {
    $bin = Join-Path $env:LOCALAPPDATA 'Unity\bin'
    if (-not (Test-Path -LiteralPath $bin)) {
        return
    }

    foreach ($name in @('unity.exe.previous', 'unity.exe.previous.sha256')) {
        $path = Join-Path $bin $name
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force
            Write-Host "已删除旧 CLI 回滚文件: $path"
        }
    }
}

function Get-ProjectStatusInstance {
    param(
        [object]$StatusJson,
        [string]$NormalizedProject
    )

    if (-not $StatusJson -or -not $StatusJson.success) {
        return $null
    }

    foreach ($instance in @($StatusJson.data.instances)) {
        if (-not $instance.project) {
            continue
        }

        $instancePath = Get-NormalizedPath $instance.project
        if ($instancePath -eq $NormalizedProject) {
            return $instance
        }
    }

    return $null
}

function Invoke-UnityStatus {
    param(
        [string]$ProjectPath,
        [switch]$UntilReady
    )

    $args = @(
        '--no-banner',
        '--non-interactive',
        'status',
        '--format', 'json',
        '--project-path', $ProjectPath
    )

    if ($UntilReady) {
        $args += @('--until-ready', '--timeout', "$TimeoutSeconds")
    }

    return Get-UnityJsonOutput $args
}

function Test-EditorProcessRunning {
    param([string]$NormalizedProject)

    $needle = $NormalizedProject.TrimEnd('\')
    $processes = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue)
    foreach ($proc in $processes) {
        $exe = [string]$proc.ExecutablePath
        if ($exe -notmatch '\\Editor\\Unity\.exe$') {
            continue
        }

        $command = ([string]$proc.CommandLine) -replace '/', '\'
        if ($command.IndexOf($needle, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }

    return $false
}

function Get-PipelineInstance {
    param([string]$NormalizedProject)

    # pipeline list 会受当前工作目录影响；在无 Editor 进程时可能误报 isRunning。
    $previous = Get-Location
    try {
        Set-Location -LiteralPath $env:SystemRoot
        $pipelineJson = Get-UnityJsonOutput @(
            '--no-banner',
            '--non-interactive',
            'pipeline',
            'list',
            '--format', 'json'
        )
    }
    finally {
        Set-Location -LiteralPath $previous.Path
    }

    if (-not $pipelineJson -or -not $pipelineJson.success) {
        return $null
    }

    foreach ($instance in @($pipelineJson.data.instances)) {
        if (-not $instance.projectPath) {
            continue
        }

        $instancePath = Get-NormalizedPath $instance.projectPath
        if ($instancePath -eq $NormalizedProject) {
            return $instance
        }
    }

    return $null
}

function Write-UnityConnectionHints {
    Write-Host ''
    Write-Host '连接失败时可按顺序排查:' -ForegroundColor Yellow
    Write-Host '  1. unity --version  → 过旧则 unity upgrade -y'
    Write-Host '  2. unity pipeline list --format json  → isReachable 与 apiUrl 应为 127.0.0.1'
    Write-Host '  3. unity status --format json --project-path <项目>  → 查看 state / blockedBy'
    Write-Host '  4. Safe Mode → 修复编译错误后重启 Editor'
    Write-Host '  5. 仅当缺少包时再运行 unity pipeline install'
}

function Wait-ForReadyInstance {
    param(
        [string]$ProjectPath,
        [string]$NormalizedProject
    )

    $statusJson = Invoke-UnityStatus -ProjectPath $ProjectPath -UntilReady
    $instance = Get-ProjectStatusInstance -StatusJson $statusJson -NormalizedProject $NormalizedProject

    if ($instance -and [string]$instance.state -eq 'ready') {
        Write-Host "Unity 编辑器已就绪 (PID $($instance.pid), 端口 $($instance.port), 版本 $($instance.version))."
        return 0
    }

    $lastState = if ($instance) { [string]$instance.state } else { 'unknown' }
    Write-Host "[错误] 等待 Unity 就绪超时 (${TimeoutSeconds}s)，最后状态: $lastState。" -ForegroundColor Red
    Write-UnityConnectionHints
    return 6
}

Assert-UnityCliVersion
Remove-StaleUnityCliRollback

$normalizedProject = Get-NormalizedPath $ProjectRoot
Write-Host "项目路径: $normalizedProject"

if (-not (Test-Path -LiteralPath (Join-Path $normalizedProject 'ProjectSettings\ProjectVersion.txt'))) {
    Write-Host '[错误] 当前目录不是有效的 Unity 项目。' -ForegroundColor Red
    exit 1
}

$statusJson = Invoke-UnityStatus -ProjectPath $normalizedProject
$instance = Get-ProjectStatusInstance -StatusJson $statusJson -NormalizedProject $normalizedProject

if ($instance -and $instance.state -eq 'ready') {
    Write-Host "已存在就绪的 Unity 实例，直接复用 (PID $($instance.pid), 端口 $($instance.port))。"
    exit 0
}

if ($instance) {
    Write-Host "检测到 Unity 实例但未就绪 (state: $($instance.state))，继续等待..."
    exit (Wait-ForReadyInstance -ProjectPath $normalizedProject -NormalizedProject $normalizedProject)
}

if (Test-EditorProcessRunning -NormalizedProject $normalizedProject) {
    $pipelineInstance = Get-PipelineInstance -NormalizedProject $normalizedProject
    if ($pipelineInstance -and $pipelineInstance.safeMode -and $pipelineInstance.safeMode.detected) {
        Write-Host '[错误] Unity 处于 Safe Mode，Pipeline 无法连接。请先修复脚本编译错误后重新启动。' -ForegroundColor Red
        exit 1
    }

    Write-Host '检测到 Unity 编辑器已在运行，但 Pipeline 尚未连接，等待就绪...'
    exit (Wait-ForReadyInstance -ProjectPath $normalizedProject -NormalizedProject $normalizedProject)
}

Write-Host '未找到本项目的 Unity 实例，正在启动编辑器...'
$openOut = Join-Path $env:TEMP 'StartUnity-open.out.txt'
$openErr = Join-Path $env:TEMP 'StartUnity-open.err.txt'
Remove-Item -LiteralPath $openOut, $openErr -Force -ErrorAction SilentlyContinue

# unity open 拉起的编辑器会继承调用方的 stdout。用管道接住时，管道要等编辑器退出才关闭。
$openArgLine = '--no-banner --non-interactive open "{0}" --args -automated --format json' -f $normalizedProject
$openProc = Start-Process -FilePath 'unity' -ArgumentList $openArgLine -Wait -PassThru -WindowStyle Hidden `
    -RedirectStandardOutput $openOut -RedirectStandardError $openErr

$openText = ''
foreach ($logPath in @($openOut, $openErr)) {
    if (Test-Path -LiteralPath $logPath) {
        $openText += [System.IO.File]::ReadAllText($logPath)
    }
}

$openJson = $null
$jsonStart = $openText.IndexOf('{')
if ($jsonStart -ge 0) {
    try {
        $openJson = $openText.Substring($jsonStart) | ConvertFrom-Json
    }
    catch {
        $openJson = $null
    }
}

if ($openProc.ExitCode -ne 0 -or ($openJson -and $openJson.success -eq $false)) {
    $message = ''
    if ($openJson -and $openJson.errors) {
        $message = ($openJson.errors | ForEach-Object { $_.message }) -join '; '
    }

    if (-not $message) {
        $message = "打开 Unity 项目失败 (exit $($openProc.ExitCode))"
    }

    Write-Host "[错误] $message" -ForegroundColor Red
    exit 1
}

Write-Host '已发送打开项目请求，等待编辑器就绪...'
exit (Wait-ForReadyInstance -ProjectPath $normalizedProject -NormalizedProject $normalizedProject)
