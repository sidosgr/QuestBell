param([string]$Dotnet = "$PSScriptRoot/.tools/dotnet/dotnet.exe")
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Set-Location -LiteralPath $PSScriptRoot
if (!(Test-Path -LiteralPath $Dotnet)) { $Dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
& $Dotnet run --project tests/QuestBell.Tests -c Release
if ($LASTEXITCODE) { throw 'Core verification failed.' }
& $Dotnet publish src/QuestBell/QuestBell.csproj -c Release -r win-x64 --self-contained true -p:DebugType=none -p:DebugSymbols=false -o artifacts/publish --nologo
if ($LASTEXITCODE) { throw 'App publish failed.' }
$taskCsc = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskRefs = @('/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Core.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll')
$taskSetupSources = @((Join-Path $PSScriptRoot 'installer/Setup.cs'),(Join-Path $PSScriptRoot 'installer/SetupSupport.cs'),(Join-Path $PSScriptRoot 'installer/ShellShortcut.cs'))
& $taskCsc /nologo /target:exe /platform:anycpu /optimize+ /main:SetupRegressionTests ('/out:' + (Join-Path $PSScriptRoot 'artifacts/SetupRegressionTests.exe')) ('/win32manifest:' + (Join-Path $PSScriptRoot 'src/QuestBell/app.manifest')) @taskRefs @taskSetupSources (Join-Path $PSScriptRoot 'tests/SetupRegressionTests.cs')
if ($LASTEXITCODE) { throw 'Installer regression build failed.' }
& (Join-Path $PSScriptRoot 'artifacts/SetupRegressionTests.exe')
if ($LASTEXITCODE) { throw 'Installer regression checks failed.' }
& $taskCsc /nologo /target:winexe /platform:anycpu /optimize+ /define:UNINSTALL ('/out:' + (Join-Path $PSScriptRoot 'artifacts/publish/QuestBell.Uninstall.exe')) ('/win32manifest:' + (Join-Path $PSScriptRoot 'src/QuestBell/app.manifest')) ('/win32icon:' + (Join-Path $PSScriptRoot 'src/QuestBell/Assets/questbell.ico')) @taskRefs @taskSetupSources
if ($LASTEXITCODE) { throw 'Uninstaller build failed.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskZip = Join-Path $PSScriptRoot 'artifacts/Payload.zip'
if (Test-Path -LiteralPath $taskZip) { Remove-Item -LiteralPath $taskZip }
[System.IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $PSScriptRoot 'artifacts/publish'), $taskZip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
& $taskCsc /nologo /target:winexe /platform:anycpu /optimize+ ('/out:' + (Join-Path $PSScriptRoot 'artifacts/QuestBell-Setup-1.4.0.exe')) ('/win32manifest:' + (Join-Path $PSScriptRoot 'src/QuestBell/app.manifest')) ('/win32icon:' + (Join-Path $PSScriptRoot 'src/QuestBell/Assets/questbell.ico')) ('/resource:' + $taskZip + ',Payload.zip') ('/resource:' + (Join-Path $PSScriptRoot 'src/QuestBell/Assets/questbell.ico') + ',questbell.ico') ('/resource:' + (Join-Path $PSScriptRoot 'src/QuestBell/Assets/personal.png') + ',personal.png') @taskRefs @taskSetupSources
if ($LASTEXITCODE) { throw 'Installer build failed.' }
$taskInstaller = Get-Item -LiteralPath 'artifacts/QuestBell-Setup-1.4.0.exe'
$taskHash = (Get-FileHash -LiteralPath $taskInstaller.FullName -Algorithm SHA256).Hash
Set-Content -LiteralPath 'artifacts/SHA256.txt' -Value "$taskHash  $($taskInstaller.Name)" -Encoding ascii
Write-Output "Installer ready: $($taskInstaller.FullName) ($([Math]::Round($taskInstaller.Length / 1MB, 1)) MB)"
