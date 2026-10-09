param([string]$Version = "1.0.0")
$ErrorActionPreference = "Stop"
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Use a semantic version such as 1.0.0." }
if ($env:OS -ne 'Windows_NT') { throw "Packaging and executable verification must run on Windows." }
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
[xml]$props = Get-Content Directory.Build.props
if ($props.Project.PropertyGroup.Version -ne $Version) { throw "Update Directory.Build.props to $Version before publishing." }
$destination = Join-Path $root "artifacts/CA-Desk-v$Version-win-x64"
dotnet publish src/CaDesk.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:Version=$Version -o $destination
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }
(Get-Content docs/READ-ME-FIRST.txt -Raw).Replace("1.0.0", $Version) | Set-Content "$destination/READ-ME-FIRST.txt" -Encoding utf8
Copy-Item docs/THIRD-PARTY-NOTICES.md $destination
$checks = Join-Path $root "artifacts/package-checks-$([Guid]::NewGuid().ToString('N'))"
$process = Start-Process -FilePath "$destination/CA-Desk.exe" -ArgumentList @("--verify-install", "`"$checks`"") -PassThru
if (-not $process.WaitForExit(45000)) { Stop-Process -Id $process.Id; throw "Packaged app did not finish its launch check." }
Copy-Item $checks "artifacts/package-verification" -Recurse -Force
if ($process.ExitCode -ne 0 -or -not (Test-Path "$checks/result.txt")) { throw "Packaged executable check failed. Inspect $checks." }
Get-Content "$checks/result.txt"
# Verification databases stay in CI scratch space and never enter the distribution.
$zip = Join-Path $root "artifacts/CA-Desk-v$Version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path "$destination/*" -DestinationPath $zip -CompressionLevel Optimal
$hash=(Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path $zip -Leaf)" | Set-Content "$zip.sha256" -Encoding ascii
Write-Output "Package ready: $zip"
