[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$QA,
    [string]$UnityExe
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath
$local = Join-Path $root '.local'
if ($QA) {
    $project = Join-Path $local 'art-playtest'
    $exe = Join-Path $local 'title-sparks-qa\Smoke\AfterHours.exe'
    $log = Join-Path $local 'build-windows-qa.log'
} else {
    $project = $root
    $exe = Join-Path $root 'Builds\TitleSparks\AfterHours.exe'
    $log = Join-Path $local 'build-windows-playtest.log'
}

if (!$PSCmdlet.ShouldProcess($exe, 'Build or update the Windows player')) {
    return
}

$output = Split-Path -Parent $exe
$prefix = $output + [IO.Path]::DirectorySeparatorChar
$processes = Get-CimInstance Win32_Process -Filter "Name = 'AfterHours.exe' OR Name = 'UnityCrashHandler64.exe'"
foreach ($process in $processes) {
    if ($process.ExecutablePath -and $process.ExecutablePath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Close the player in $output before rebuilding it."
    }
}

$settings = Join-Path $project 'ProjectSettings\ProjectSettings.asset'
if ($QA -and (!(Test-Path -LiteralPath $settings -PathType Leaf) -or
    !(Select-String -LiteralPath $settings -Pattern '^\s*companyName:\s*AfterHours QA\s*$' -Quiet))) {
    throw 'QA requires the isolated .local\art-playtest project with companyName AfterHours QA.'
}

if (!$UnityExe) {
    $versionFile = Join-Path $project 'ProjectSettings\ProjectVersion.txt'
    $version = Select-String -LiteralPath $versionFile -Pattern '^m_EditorVersion:\s*(\S+)\s*$'
    if (!$version) {
        throw "No Unity editor version found in $versionFile."
    }
    $editorPath = 'Unity\Hub\Editor\{0}\Editor\Unity.exe' -f $version.Matches[0].Groups[1].Value
    $candidates = @(
        ([IO.Path]::Combine($env:ProgramFiles, $editorPath))
        ([IO.Path]::Combine('D:\Program Files', $editorPath))
    )
    $UnityExe = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (!$UnityExe) {
        throw "Unity $($version.Matches[0].Groups[1].Value) was not found. Pass its executable path with -UnityExe."
    }
}

$editor = (Resolve-Path -LiteralPath $UnityExe).ProviderPath
$null = [IO.Directory]::CreateDirectory($local)
$lockPath = Join-Path $local 'build-windows.lock'
try {
    $buildLock = [IO.File]::Open($lockPath, 'OpenOrCreate', 'ReadWrite', 'None')
} catch [IO.IOException] {
    throw "Another Windows build holds the lock at $lockPath."
}
try {
    $null = [IO.Directory]::CreateDirectory($output)
    [IO.File]::WriteAllText($log, '')
    $arguments = @(
        '-batchmode', '-nographics', '-quit',
        '-projectPath', ('"{0}"' -f $project),
        '-buildWindows64Player', ('"{0}"' -f $exe),
        '-logFile', ('"{0}"' -f $log)
    )
    $build = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($build.ExitCode -ne 0 -or !(Test-Path -LiteralPath $exe -PathType Leaf) -or
        !(Select-String -LiteralPath $log -Pattern '^Build Finished, Result: Success\.\s*$' -Quiet)) {
        throw "Windows build did not finish successfully (Unity exit code $($build.ExitCode)). See $log."
    }
    Write-Output $exe
} finally {
    $buildLock.Dispose()
}
