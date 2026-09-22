<#
.SYNOPSIS
  Drive AI Connect 4 (Avalonia) through Windows UI Automation for verification.

.DESCRIPTION
  Tracks exactly one process started by this helper (run state under %TEMP%\c4-verify).
  Never attach to or kill an instance you did not launch.

.EXAMPLE
  .\control-c4.ps1 launch -Build
  .\control-c4.ps1 doctor
  .\control-c4.ps1 wait-name -Name "Ready"
  .\control-c4.ps1 snapshot -Path ..\artifacts\$runId\tree.uia.txt
  .\control-c4.ps1 cleanup
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory = $true)]
    [ValidateSet(
        'launch', 'doctor', 'cleanup', 'stop',
        'get-title', 'info', 'snapshot', 'screenshot',
        'wait-name', 'invoke', 'get-enabled', 'find-text', 'assert-text'
    )]
    [string]$Command,

    [string]$RunId,
    [string]$Exe,
    [string]$Path,
    [string]$Name,
    [string]$Contains,
    [int]$TimeoutSec = 30,
    [switch]$Build,
    [switch]$WithoutApiKey
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:SkillRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$script:DefaultArtifactsRoot = Join-Path $script:SkillRoot 'artifacts'
$script:StateRoot = Join-Path $env:TEMP 'c4-verify'
$script:WindowTitle = 'AI Connect 4'
$script:ProcessName = 'AIConnect4.App'

function Get-DefaultExe {
    $candidates = @(
        (Join-Path $script:RepoRoot 'src\AIConnect4.App\bin\Debug\net8.0\AIConnect4.App.exe'),
        (Join-Path $script:RepoRoot 'src\AIConnect4.App\bin\Release\net8.0\AIConnect4.App.exe')
    )
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath $c) { return (Resolve-Path $c).Path }
    }
    throw "AIConnect4.App.exe not found under src\AIConnect4.App\bin. Run: dotnet build `"AIConnect4.sln`""
}

function Get-ActiveRunId {
    if ($RunId) { return $RunId }
    $pointer = Join-Path $script:StateRoot 'active-run-id.txt'
    if (Test-Path -LiteralPath $pointer) {
        $id = (Get-Content -LiteralPath $pointer -Raw).Trim()
        if ($id) { return $id }
    }
    throw "No active run id. Pass -RunId or run launch first."
}

function Get-RunDir([string]$Id) {
    Join-Path $script:StateRoot $Id
}

function Get-RunStatePath([string]$Id) {
    Join-Path (Get-RunDir $Id) 'run.json'
}

function Read-RunState {
    $id = Get-ActiveRunId
    $path = Get-RunStatePath $id
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Run state missing: $path"
    }
    return (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json)
}

function Write-RunState($State) {
    $dir = Get-RunDir $State.runId
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $path = Get-RunStatePath $State.runId
    ($State | ConvertTo-Json -Depth 6) | Set-Content -LiteralPath $path -Encoding utf8
    Set-Content -LiteralPath (Join-Path $script:StateRoot 'active-run-id.txt') -Value $State.runId -Encoding utf8
}

function Ensure-UiAssemblies {
    Add-Type -AssemblyName UIAutomationClient -ErrorAction Stop
    Add-Type -AssemblyName UIAutomationTypes -ErrorAction Stop
    Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
    Add-Type -AssemblyName System.Drawing -ErrorAction Stop
    if (-not ('C4VerifyNative' -as [type])) {
        Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class C4VerifyNative {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
}
"@
    }
}

function Get-MainWindow([int]$ProcessId) {
    Ensure-UiAssemblies
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-PidCondition $ProcessId
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSec)
    do {
        $win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
        if ($win -and $win.Current.Name -eq $script:WindowTitle) {
            return $win
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Main window for PID $ProcessId did not appear within ${TimeoutSec}s (expected title '$script:WindowTitle')."
}

function Activate-Window($Window) {
    Ensure-UiAssemblies
    $hwnd = [IntPtr]$Window.Current.NativeWindowHandle
    if ([C4VerifyNative]::IsIconic($hwnd)) {
        [void][C4VerifyNative]::ShowWindow($hwnd, 9)
    }
    [void][C4VerifyNative]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 200
}

function New-NameCondition([string]$ElementName) {
    return New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::NameProperty, $ElementName)
}

function New-TypeCondition($ControlType) {
    return New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $ControlType)
}

function New-PidCondition([int]$ProcessId) {
    return New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId)
}

function Find-Named($Parent, [string]$ElementName, $ControlType = $null) {
    Ensure-UiAssemblies
    $nameCond = New-NameCondition $ElementName
    if ($null -eq $ControlType) {
        return $Parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCond)
    }
    $typeCond = New-TypeCondition $ControlType
    $and = New-Object System.Windows.Automation.AndCondition ($nameCond, $typeCond)
    return $Parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $and)
}

function Find-TextContaining($Parent, [string]$Substring) {
    Ensure-UiAssemblies
    $typeCond = New-TypeCondition ([System.Windows.Automation.ControlType]::Text)
    $texts = $Parent.FindAll([System.Windows.Automation.TreeScope]::Descendants, $typeCond)
    foreach ($t in $texts) {
        $n = $t.Current.Name
        if ($n -and ($n -like "*$Substring*")) {
            return $t
        }
    }
    return $null
}

function Invoke-NamedElement($Window, [string]$ElementName) {
    Ensure-UiAssemblies
    $el = Find-Named $Window $ElementName
    if (-not $el) { throw "Element not found for invoke: '$ElementName'" }
    $patterns = @($el.GetSupportedPatterns())
    if ($patterns -notcontains [System.Windows.Automation.InvokePattern]::Pattern) {
        throw "Element '$ElementName' has no InvokePattern."
    }
    $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Write-UiaTree($Element, [string]$OutPath, [int]$MaxDepth = 8) {
    Ensure-UiAssemblies
    $lines = New-Object System.Collections.Generic.List[string]
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    function Walk($el, $depth) {
        if ($depth -gt $MaxDepth) { return }
        $name = $el.Current.Name
        $type = $el.Current.ControlType.ProgrammaticName
        $enabled = $el.Current.IsEnabled
        $lines.Add(('{0}{1} name="{2}" enabled={3}' -f ('  ' * $depth), $type, $name, $enabled))
        $child = $walker.GetFirstChild($el)
        while ($null -ne $child) {
            Walk $child ($depth + 1)
            $child = $walker.GetNextSibling($child)
        }
    }
    Walk $Element 0
    $dir = Split-Path -Parent $OutPath
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $lines | Set-Content -LiteralPath $OutPath -Encoding utf8
}

function Save-WindowScreenshot($Window, [string]$OutPath) {
    Ensure-UiAssemblies
    $bounds = $Window.Current.BoundingRectangle
    if ($bounds.Width -lt 1 -or $bounds.Height -lt 1) {
        throw "Window bounding rectangle is empty; is the window minimized?"
    }
    $width = [Math]::Max(1, [int][Math]::Ceiling($bounds.Width))
    $height = [Math]::Max(1, [int][Math]::Ceiling($bounds.Height))
    $bmp = New-Object System.Drawing.Bitmap $width, $height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.CopyFromScreen([int]$bounds.X, [int]$bounds.Y, 0, 0, $bmp.Size)
        $dir = Split-Path -Parent $OutPath
        if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
        $bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $g.Dispose()
        $bmp.Dispose()
    }
}

function Invoke-Launch {
    New-Item -ItemType Directory -Force -Path $script:StateRoot | Out-Null
    $id = if ($RunId) { $RunId } else { [guid]::NewGuid().ToString('N').Substring(0, 12) }
    $runDir = Get-RunDir $id
    $scratchDir = Join-Path $runDir 'scratch'
    $artifactDir = Join-Path $script:DefaultArtifactsRoot $id
    New-Item -ItemType Directory -Force -Path $scratchDir, $artifactDir | Out-Null

    if ($Build) {
        Push-Location $script:RepoRoot
        try {
            & dotnet build 'AIConnect4.sln' -c Debug --nologo
            if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit $LASTEXITCODE" }
        }
        finally { Pop-Location }
    }

    $exePath = if ($Exe) { (Resolve-Path $Exe).Path } else { Get-DefaultExe }

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $exePath
    $startInfo.WorkingDirectory = (Split-Path -Parent $exePath)
    $startInfo.UseShellExecute = $false

    # Child process inherits env; optionally clear the API key for the missing-key Ready path.
    foreach ($entry in [System.Environment]::GetEnvironmentVariables().GetEnumerator()) {
        try { $startInfo.EnvironmentVariables[$entry.Key] = [string]$entry.Value } catch { }
    }
    $hadKey = -not [string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable('OPENROUTER_API_KEY'))
    if ($WithoutApiKey) {
        $startInfo.EnvironmentVariables['OPENROUTER_API_KEY'] = ''
        $hadKey = $false
    }

    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo = $startInfo
    if (-not $proc.Start()) { throw "Failed to start $exePath" }

    $state = [pscustomobject]@{
        runId       = $id
        pid         = $proc.Id
        exe         = $exePath
        startedUtc  = [DateTime]::UtcNow.ToString('o')
        scratchDir  = $scratchDir
        artifactDir = $artifactDir
        repoRoot    = $script:RepoRoot
        hadApiKey   = $hadKey
        withoutApiKey = [bool]$WithoutApiKey
    }
    Write-RunState $state

    $win = Get-MainWindow -ProcessId $proc.Id
    Activate-Window $win

    [pscustomobject]@{
        runId       = $id
        pid         = $proc.Id
        title       = $win.Current.Name
        exe         = $exePath
        scratchDir  = $scratchDir
        artifactDir = $artifactDir
        hadApiKey   = $hadKey
        withoutApiKey = [bool]$WithoutApiKey
    } | ConvertTo-Json -Depth 4
}

function Invoke-Doctor {
    $state = Read-RunState
    $proc = Get-Process -Id $state.pid -ErrorAction SilentlyContinue
    if (-not $proc) {
        throw "Doctor FAIL: PID $($state.pid) is not running."
    }
    if ($proc.ProcessName -ne $script:ProcessName) {
        throw "Doctor FAIL: PID $($state.pid) is '$($proc.ProcessName)', expected '$script:ProcessName'."
    }
    $win = Get-MainWindow -ProcessId $state.pid
    $title = $win.Current.Name
    if ($title -ne $script:WindowTitle) {
        throw "Doctor FAIL: unexpected title '$title' (expected '$script:WindowTitle')."
    }
    $exeOk = Test-Path -LiteralPath $state.exe
    $readyText = $null
    $readyExact = Find-Named $win 'Ready'
    $readyMissing = Find-Named $win 'Ready (API key missing)'
    if ($readyExact) { $readyText = 'Ready' }
    elseif ($readyMissing) { $readyText = 'Ready (API key missing)' }

    [pscustomobject]@{
        status      = 'ok'
        runId       = $state.runId
        pid         = $state.pid
        title       = $title
        exe         = $state.exe
        exeExists   = $exeOk
        scratchDir  = $state.scratchDir
        artifactDir = $state.artifactDir
        hadApiKey   = $state.hadApiKey
        statusText  = $readyText
    } | ConvertTo-Json -Depth 4
}

function Invoke-Cleanup {
    $state = $null
    try { $state = Read-RunState } catch { }
    if ($state) {
        $proc = Get-Process -Id $state.pid -ErrorAction SilentlyContinue
        if ($proc) {
            Stop-Process -Id $state.pid -Force
            Start-Sleep -Milliseconds 500
        }
        $runDir = Get-RunDir $state.runId
        if (Test-Path -LiteralPath $runDir) {
            Remove-Item -LiteralPath $runDir -Recurse -Force
        }
        $pointer = Join-Path $script:StateRoot 'active-run-id.txt'
        if (Test-Path -LiteralPath $pointer) {
            $active = (Get-Content -LiteralPath $pointer -Raw).Trim()
            if ($active -eq $state.runId) {
                Remove-Item -LiteralPath $pointer -Force
            }
        }
        Write-Output "Cleaned run $($state.runId). Evidence kept at $($state.artifactDir)"
    }
    else {
        Write-Output 'No active run state to clean.'
    }
}

function Require-OwnedWindow {
    $state = Read-RunState
    $proc = Get-Process -Id $state.pid -ErrorAction SilentlyContinue
    if (-not $proc) { throw "Owned process PID $($state.pid) is not running. Run doctor." }
    $win = Get-MainWindow -ProcessId $state.pid
    Activate-Window $win
    return @{ State = $state; Window = $win }
}

switch ($Command) {
    'launch' {
        Invoke-Launch
    }
    'doctor' {
        Invoke-Doctor
    }
    'cleanup' {
        Invoke-Cleanup
    }
    'stop' {
        Invoke-Cleanup
    }
    'info' {
        $state = Read-RunState
        $state | ConvertTo-Json -Depth 4
    }
    'get-title' {
        $ctx = Require-OwnedWindow
        Write-Output $ctx.Window.Current.Name
    }
    'snapshot' {
        if (-not $Path) { throw 'snapshot requires -Path.' }
        $ctx = Require-OwnedWindow
        $target = $ctx.Window
        if ($Name) {
            $named = Find-Named $ctx.Window $Name
            if (-not $named) { throw "Named element not found for snapshot: $Name" }
            $target = $named
        }
        Write-UiaTree $target $Path
        Write-Output "Wrote UIA snapshot: $Path"
    }
    'screenshot' {
        if (-not $Path) { throw 'screenshot requires -Path.' }
        $ctx = Require-OwnedWindow
        Save-WindowScreenshot $ctx.Window $Path
        Write-Output "Wrote screenshot: $Path"
    }
    'wait-name' {
        if (-not $Name) { throw 'wait-name requires -Name.' }
        $ctx = Require-OwnedWindow
        Ensure-UiAssemblies
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSec)
        do {
            $el = Find-Named $ctx.Window $Name
            if (-not $el) { $el = Find-TextContaining $ctx.Window $Name }
            if ($el) {
                Write-Output "Found: $($el.Current.Name)"
                return
            }
            Start-Sleep -Milliseconds 250
        } while ([DateTime]::UtcNow -lt $deadline)
        throw "Timed out waiting for name/text '$Name'."
    }
    'invoke' {
        if (-not $Name) { throw "invoke requires -Name (e.g. 'Play')." }
        $ctx = Require-OwnedWindow
        Invoke-NamedElement $ctx.Window $Name
        Start-Sleep -Milliseconds 300
        Write-Output "Invoked: $Name"
    }
    'get-enabled' {
        if (-not $Name) { throw 'get-enabled requires -Name.' }
        $ctx = Require-OwnedWindow
        $el = Find-Named $ctx.Window $Name
        if (-not $el) { throw "Element not found: $Name" }
        [pscustomobject]@{
            name    = $Name
            enabled = [bool]$el.Current.IsEnabled
        } | ConvertTo-Json -Compress
    }
    'find-text' {
        if (-not $Contains) { throw 'find-text requires -Contains.' }
        $ctx = Require-OwnedWindow
        $el = Find-TextContaining $ctx.Window $Contains
        if (-not $el) { throw "No Text element containing '$Contains'." }
        Write-Output $el.Current.Name
    }
    'assert-text' {
        if (-not $Contains) { throw 'assert-text requires -Contains.' }
        $ctx = Require-OwnedWindow
        $el = Find-TextContaining $ctx.Window $Contains
        if (-not $el) { throw "Assertion failed: no Text containing '$Contains'." }
        Write-Output "OK: found '$($el.Current.Name)'"
    }
    default {
        throw "Unknown command: $Command"
    }
}
