param([string]$OutputDirectory = 'artifacts/upgrade-checks')
$ErrorActionPreference = 'Stop'
$fixtureRoot = Join-Path $env:RUNNER_TEMP "CA-Desk-upgrade-$([Guid]::NewGuid().ToString('N'))"
$oldRoot = Join-Path $fixtureRoot 'old-release'
New-Item -ItemType Directory -Path $oldRoot -Force | Out-Null
gh release download v1.0.1 --repo Fahad-uz/CA-Desk --pattern 'CA-Desk-v1.0.1-win-x64.zip' --dir $oldRoot
if ($LASTEXITCODE -ne 0) { throw 'Previous published release could not be downloaded.' }
Expand-Archive (Join-Path $oldRoot 'CA-Desk-v1.0.1-win-x64.zip') (Join-Path $oldRoot 'app')
$exe = (Get-ChildItem (Join-Path $oldRoot 'app') -Recurse -Filter 'CA-Desk.exe' | Select-Object -First 1).FullName
$oldDirectory = Split-Path $exe
$oldCheck = Join-Path $fixtureRoot 'old-executable-check'
$process = Start-Process $exe -ArgumentList "--verify-install `"$oldCheck`"" -PassThru
if (-not $process.WaitForExit(45000)) { Stop-Process -Id $process.Id; throw 'Previous-release executable verification timed out.' }
if ($process.ExitCode -ne 0) { throw 'Published v1.0.1 executable did not pass its isolated verification.' }
$profile = Join-Path $fixtureRoot 'profile'
dotnet run --project tests/CaDesk.UpgradeFixtureBuilder -c Release "-p:OldReleaseDirectory=$oldDirectory" -- $profile
if ($LASTEXITCODE -ne 0) { throw 'Old-release fixture creation failed.' }
dotnet run --project tests/CaDesk.WindowsTests -c Release --no-build -- $OutputDirectory $profile
if ($LASTEXITCODE -ne 0) { throw 'Prior-release upgrade checks failed. Do not publish.' }
dotnet run --project tests/CaDesk.UpgradeFixtureBuilder -c Release --no-build "-p:OldReleaseDirectory=$oldDirectory" -- --probe-newer (Join-Path $profile 'CADesk/Data/cadesk.db')
if ($LASTEXITCODE -ne 0) { throw 'The prior release did not reject the newer schema.' }
Copy-Item (Join-Path $oldCheck 'result.txt') (Join-Path $OutputDirectory 'previous-executable-result.txt')
'PASS: Previous release executable launched; its published Core DLL created 150 clients and 3,000 work areas; forward migration, full-field preservation, backup restore and newer-schema rejection passed.' | Set-Content (Join-Path $OutputDirectory 'upgrade-result.txt')
# All fixture databases are fictional and temporary. Upload only reports and rendered interface images.
