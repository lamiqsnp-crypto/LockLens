param([switch]$Test)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path -LiteralPath $compiler)) { throw 'Requires 64-bit Windows and .NET Framework 4.5 or later.' }
    & $compiler /nologo /target:winexe /platform:x64 /optimize+ /win32manifest:app.manifest /win32icon:LockLens.ico /resource:LockLens.ico,LockLens.AppIcon /out:LockLens.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll LockLens.cs L10n.cs
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
    if ($Test) {
        $report = Join-Path $PSScriptRoot 'test-results.txt'
        $testProcess = Start-Process -FilePath (Join-Path $PSScriptRoot 'LockLens.exe') -ArgumentList '--self-test',('"' + $report + '"') -PassThru -WindowStyle Hidden
        if (-not $testProcess.WaitForExit(180000)) { $testProcess.Kill(); throw 'Test timed out.' }
        Get-Content -LiteralPath $report -Encoding UTF8
        if ($testProcess.ExitCode -ne 0) { throw 'Self-test failed.' }
        $windowReport = Join-Path $PSScriptRoot 'window-checks.txt'
        $windowTest = Start-Process -FilePath (Join-Path $PSScriptRoot 'LockLens.exe') -ArgumentList '--ui-check',('"' + $windowReport + '"') -PassThru -WindowStyle Hidden
        if (-not $windowTest.WaitForExit(30000)) { $windowTest.Kill(); throw 'Window test timed out.' }
        Get-Content -LiteralPath $windowReport -Encoding UTF8
        if ($windowTest.ExitCode -ne 0) { throw 'Window/language test failed.' }
    }
    Write-Host 'Built LockLens.exe (portable, x64).'
} finally { Pop-Location }
