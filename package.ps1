param([Parameter(Mandatory = $true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & (Join-Path $PSScriptRoot 'build.ps1') -Test
    if (-not (Test-Path -LiteralPath $OutputDirectory)) { New-Item -ItemType Directory -Path $OutputDirectory | Out-Null }
    $packageFolder = Join-Path $OutputDirectory 'LockLens-1.0'
    $portable = Join-Path $OutputDirectory 'LockLens.zip'
    $sources = Join-Path $OutputDirectory 'LockLens-source.zip'
    foreach ($target in @($packageFolder, $portable, $sources)) { if (Test-Path -LiteralPath $target) { throw ('Output already exists: ' + $target) } }
    New-Item -ItemType Directory -Path $packageFolder | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LockLens.exe'),(Join-Path $PSScriptRoot 'README.md'),(Join-Path $PSScriptRoot '使用说明.txt') -Destination $packageFolder
    $hash = (Get-FileHash -LiteralPath (Join-Path $packageFolder 'LockLens.exe') -Algorithm SHA256).Hash
    Set-Content -LiteralPath (Join-Path $packageFolder 'SHA256.txt') -Value ($hash + '  LockLens.exe') -Encoding ASCII
    $verification = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'test-results.txt') -Raw -Encoding UTF8) + [Environment]::NewLine + (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'window-checks.txt') -Raw -Encoding UTF8)
    Set-Content -LiteralPath (Join-Path $packageFolder 'verification.txt') -Value $verification -Encoding UTF8
    $runtimeFiles = @('LockLens.exe','README.md','使用说明.txt','SHA256.txt') | ForEach-Object { Join-Path $packageFolder $_ }
    Compress-Archive -LiteralPath $runtimeFiles -DestinationPath $portable -CompressionLevel Optimal
    $sourceFiles = @('.gitignore','LockLens.cs','L10n.cs','LockLens.ico','app.manifest','build.ps1','package.ps1','make_icon.py','README.md','RELEASE-NOTES.md','使用说明.txt') | ForEach-Object { Join-Path $PSScriptRoot $_ }
    Compress-Archive -LiteralPath $sourceFiles -DestinationPath $sources -CompressionLevel Optimal
    Get-Item -LiteralPath $portable,$sources | Select-Object FullName,Length
    Write-Output ('Executable SHA256: ' + $hash)
} finally { Pop-Location }
