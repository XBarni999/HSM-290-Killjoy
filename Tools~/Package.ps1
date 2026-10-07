$ErrorActionPreference = 'Stop'
$modRoot = Split-Path $PSScriptRoot -Parent
$runtimeRoot = Join-Path $PSScriptRoot 'Runtime\Kinzhal'
$runtimeDll = Join-Path $runtimeRoot 'bin\Release\net472\HSM-290-Killjoy.dll'
$bundlePath = Join-Path $runtimeRoot 'Bundle\Kh47M2.nobp'
$deliveryRoot = Join-Path $modRoot 'Delivery~'
$version = ([xml](Get-Content -LiteralPath (Join-Path $runtimeRoot 'Kinzhal.csproj') -Raw)).Project.PropertyGroup.Version
if (!(Test-Path -LiteralPath $runtimeDll) -or !(Test-Path -LiteralPath $bundlePath)) { throw 'Build the Blueprinter bundle and Release DLL first.' }
$assembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($runtimeDll))
$stream = $assembly.GetManifestResourceStream('Kinzhal.Kh47M2.nobp')
if ($null -eq $stream) { throw 'Embedded Blueprinter bundle is missing.' }
$sha = [Security.Cryptography.SHA256]::Create()
try { $embeddedHash = ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
finally { $stream.Dispose(); $sha.Dispose() }
$sourceHash = (Get-FileHash -LiteralPath $bundlePath -Algorithm SHA256).Hash
if ($sourceHash -ne $embeddedHash) { throw 'Embedded bundle hash does not match the generated bundle.' }
New-Item -ItemType Directory -Force -Path $deliveryRoot | Out-Null
Copy-Item -LiteralPath $runtimeDll -Destination (Join-Path $deliveryRoot 'HSM-290-Killjoy.dll') -Force
Copy-Item -LiteralPath (Join-Path $modRoot 'README.md') -Destination (Join-Path $deliveryRoot 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $modRoot 'CHANGELOG.md') -Destination (Join-Path $deliveryRoot 'CHANGELOG.md') -Force
$dllHash = (Get-FileHash -LiteralPath $runtimeDll -Algorithm SHA256).Hash
@("DLL SHA256: $dllHash", "BUNDLE SHA256: $sourceHash", 'Embedded bundle verified. Target hits and impact behavior confirmed by user; full range and multiplayer remain unverified.') | Set-Content -LiteralPath (Join-Path $deliveryRoot 'SHA256.txt')
Compress-Archive -LiteralPath (Join-Path $deliveryRoot 'HSM-290-Killjoy.dll'), (Join-Path $deliveryRoot 'README.md'), (Join-Path $deliveryRoot 'CHANGELOG.md'), (Join-Path $deliveryRoot 'SHA256.txt') -DestinationPath (Join-Path $deliveryRoot "HSM-290-Killjoy_$version.zip") -Force
Write-Output "Verified package: $deliveryRoot"
