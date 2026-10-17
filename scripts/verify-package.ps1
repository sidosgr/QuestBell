$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $taskRoot
$taskTestRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('QuestBell-install-test/' + [guid]::NewGuid().ToString('N'))
$taskInstallPath = Join-Path $taskTestRoot 'QuestBell'
$taskDataPath = Join-Path $taskTestRoot 'data'
New-Item -ItemType Directory -Force -Path $taskTestRoot | Out-Null
$taskInstallerPath = Join-Path $taskRoot 'artifacts/QuestBell-Setup-1.3.1.exe'
$taskArguments = @('/silent','/no-register',('/dir="' + $taskInstallPath + '"'))
$taskSetup = Start-Process -FilePath $taskInstallerPath -ArgumentList $taskArguments -PassThru -Wait -WindowStyle Hidden
if ($taskSetup.ExitCode -ne 0) { throw 'Isolated installation failed. See %TEMP%/QuestBell-setup-error.txt.' }
$taskApp = Start-Process -FilePath (Join-Path $taskInstallPath 'QuestBell.exe') -ArgumentList @('--smoke-test','--data-dir',('"' + $taskDataPath + '"')) -PassThru -Wait -WindowStyle Hidden
if ($taskApp.ExitCode -ne 0) { throw 'Installed app smoke test failed.' }
Copy-Item -LiteralPath (Join-Path $taskDataPath 'smoke-report.json') -Destination 'artifacts/installed-app-report.json'
$taskUpgrade = Start-Process -FilePath $taskInstallerPath -ArgumentList $taskArguments -PassThru -Wait -WindowStyle Hidden
if ($taskUpgrade.ExitCode -ne 0) { throw 'Isolated upgrade failed.' }
$taskUninstall = Start-Process -FilePath (Join-Path $taskInstallPath 'QuestBell.Uninstall.exe') -ArgumentList @('/silent','/no-register') -PassThru -Wait -WindowStyle Hidden
for ($taskAttempt=0; $taskAttempt -lt 30 -and (Test-Path -LiteralPath $taskInstallPath); $taskAttempt++) { Start-Sleep -Milliseconds 300 }
$taskReport = [ordered]@{
    Result='PASS'
    InstallerSHA256=(Get-FileHash -LiteralPath $taskInstallerPath -Algorithm SHA256).Hash
    InstallExitCode=$taskSetup.ExitCode
    AppExitCode=$taskApp.ExitCode
    UpgradeExitCode=$taskUpgrade.ExitCode
    UninstallExitCode=$taskUninstall.ExitCode
    InstallFolderRemoved=(!(Test-Path -LiteralPath $taskInstallPath))
    DataPreserved=(Test-Path -LiteralPath (Join-Path $taskDataPath 'questbell.json'))
    TestedOn=[Environment]::OSVersion.VersionString
    TestRoot=$taskTestRoot
}
if (!$taskReport.InstallFolderRemoved -or !$taskReport.DataPreserved) { throw 'Uninstall verification failed.' }
$taskReport | ConvertTo-Json | Set-Content -LiteralPath 'artifacts/installer-test-report.json' -Encoding utf8
$taskReport | ConvertTo-Json
