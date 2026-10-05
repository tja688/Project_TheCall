#Requires -Version 5.1
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot
)

$ErrorActionPreference = 'Stop'

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
    param([string]$ProjectPath)

    return Get-UnityJsonOutput @(
        '--no-banner',
        '--non-interactive',
        'status',
        '--format', 'json',
        '--project', $ProjectPath
    )
}

function Test-EditorRunningLocally {
    param([string]$NormalizedProject)

    $pipelineJson = Get-UnityJsonOutput @(
        '--no-banner',
        '--non-interactive',
        'pipeline',
        'list',
        '--format', 'json'
    )

    if (-not $pipelineJson -or -not $pipelineJson.success) {
        return $false
    }

    foreach ($instance in @($pipelineJson.data.instances)) {
        if (-not $instance.projectPath) {
            continue
        }

        $instancePath = Get-NormalizedPath $instance.projectPath
        if ($instancePath -eq $NormalizedProject -and $instance.isRunning) {
            return $true
        }
    }

    return $false
}

function Wait-ForReadyInstance {
    param(
        [string]$ProjectPath,
        [string]$NormalizedProject,
        [int]$TimeoutSeconds = 300,
        [int]$PollIntervalSeconds = 2
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastState = 'unknown'

    while ((Get-Date) -lt $deadline) {
        $statusJson = Invoke-UnityStatus -ProjectPath $ProjectPath
        $instance = Get-ProjectStatusInstance -StatusJson $statusJson -NormalizedProject $NormalizedProject

        if ($instance) {
            $lastState = [string]$instance.state
            if ($lastState -eq 'ready') {
                Write-Host "Unity 编辑器已就绪 (PID $($instance.pid), 端口 $($instance.port), 版本 $($instance.version))."
                return 0
            }

            Write-Host "等待 Unity 就绪... 当前状态: $lastState"
        }
        else {
            Write-Host '等待 Unity Pipeline 连接...'
        }

        Start-Sleep -Seconds $PollIntervalSeconds
    }

    Write-Host "[错误] 等待 Unity 就绪超时 (${TimeoutSeconds}s)，最后状态: $lastState。" -ForegroundColor Red
    if ($lastState -ne 'ready') {
        Write-Host '若编辑器已打开但 CLI 无法连接，请运行: unity pipeline install' -ForegroundColor Yellow
    }
    return 6
}

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

if (Test-EditorRunningLocally -NormalizedProject $normalizedProject) {
    Write-Host '检测到 Unity 编辑器已在运行，但 Pipeline 尚未连接，等待就绪...'
    exit (Wait-ForReadyInstance -ProjectPath $normalizedProject -NormalizedProject $normalizedProject)
}

Write-Host '未找到本项目的 Unity 实例，正在启动编辑器...'
$openJson = Get-UnityJsonOutput @(
    '--no-banner',
    '--non-interactive',
    'open',
    $normalizedProject,
    '--args', '-automated',
    '--format', 'json'
)

if ($openJson -and $openJson.success -eq $false) {
    $message = ($openJson.errors | ForEach-Object { $_.message }) -join '; '
    if (-not $message) {
        $message = '打开 Unity 项目失败'
    }

    Write-Host "[错误] $message" -ForegroundColor Red
    exit 1
}

Write-Host '已发送打开项目请求，等待编辑器就绪...'
exit (Wait-ForReadyInstance -ProjectPath $normalizedProject -NormalizedProject $normalizedProject)
