param([string]$Version = "1.1.2")
$ErrorActionPreference = "Stop"
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Use a semantic version such as 1.0.0." }
if ($env:OS -ne 'Windows_NT') { throw "Packaging and executable verification must run on Windows." }
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
[xml]$props = Get-Content Directory.Build.props
if ($props.Project.PropertyGroup.Version -ne $Version) { throw "Update Directory.Build.props to $Version before publishing." }
$destination = Join-Path $root "artifacts/CA-Desk-v$Version-win-x64"
if (Test-Path $destination) { Remove-Item $destination -Recurse -Force }
dotnet publish src/CaDesk.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -p:Version=$Version -o $destination
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }
$files = @(Get-ChildItem $destination -Recurse -File)
if ($files.Count -ne 1 -or $files[0].Name -ne 'CA-Desk.exe') { throw 'Single-file publish left dependencies outside the executable.' }
(Get-Content docs/READ-ME-FIRST.txt -Raw).Replace("{{VERSION}}", $Version) | Set-Content "$destination/READ-ME-FIRST.txt" -Encoding utf8
Copy-Item docs/THIRD-PARTY-NOTICES.md $destination
$checks = Join-Path $root "artifacts/package-checks-$([Guid]::NewGuid().ToString('N'))"
$copyRoot = Join-Path ([IO.Path]::GetTempPath()) "CA-Desk-copy-$([Guid]::NewGuid().ToString('N'))"
$desktop = Join-Path $copyRoot 'Desktop copy'
$working = Join-Path $copyRoot 'Unrelated working folder'
New-Item -ItemType Directory -Path $desktop, $working -Force | Out-Null
Copy-Item "$destination/CA-Desk.exe" "$desktop/CA-Desk.exe"
if (@(Get-ChildItem $desktop -Force).Count -ne 1) { throw 'Copied-executable test must contain only the EXE.' }
$previousExtraction = $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
try {
    # A fresh extraction cache prevents another app launch from supplying dependencies.
    $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $copyRoot 'Fresh runtime cache'
    $process = Start-Process -FilePath "$desktop/CA-Desk.exe" -WorkingDirectory $working -ArgumentList @("--verify-install", "`"$checks`"") -PassThru
    if (-not $process.WaitForExit(90000)) { Stop-Process -Id $process.Id; throw "Copied single-file app did not finish its launch check." }
}
finally { $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = $previousExtraction }
Copy-Item $checks "artifacts/package-verification" -Recurse -Force
if ($process.ExitCode -ne 0 -or -not (Test-Path "$checks/result.txt")) { throw "Packaged executable check failed. Inspect $checks." }
Get-Content "$checks/result.txt"
'PASS: Copied only CA-Desk.exe into a separate Desktop copy folder with spaces; launched from an unrelated working directory with a fresh runtime cache; WPF, native SQLite, profile, data, backup and title-bar checks passed without adjacent dependencies.' | Set-Content 'artifacts/package-verification/desktop-copy.txt'
Remove-Item $copyRoot -Recurse -Force
# Verification databases stay in CI scratch space and never enter the distribution.
$zip = Join-Path $root "artifacts/CA-Desk-v$Version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path "$destination/*" -DestinationPath $zip -CompressionLevel Optimal
$hash=(Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path $zip -Leaf)" | Set-Content "$zip.sha256" -Encoding ascii
$standalone = Join-Path $root "artifacts/CA-Desk-v$Version-win-x64.exe"
Copy-Item "$destination/CA-Desk.exe" $standalone
$exeHash = (Get-FileHash $standalone -Algorithm SHA256).Hash.ToLowerInvariant()
"$exeHash  $(Split-Path $standalone -Leaf)" | Set-Content "$standalone.sha256" -Encoding ascii
Write-Output "Package ready: $zip"
Write-Output "Standalone executable ready: $standalone"
