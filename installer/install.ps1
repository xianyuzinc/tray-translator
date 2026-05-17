$ErrorActionPreference = "Stop"

$appName = "TrayTranslator"
$installDir = Join-Path $env:LOCALAPPDATA "Programs\TrayTranslator"
$sourceDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$programsDir = [Environment]::GetFolderPath("Programs")
$shortcutPath = Join-Path $programsDir "TrayTranslator.lnk"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\TrayTranslator"

New-Item -ItemType Directory -Force -Path $installDir | Out-Null

Get-Process -Name "TrayTranslator" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Copy-Item -LiteralPath (Join-Path $sourceDir "TrayTranslator.exe") -Destination (Join-Path $installDir "TrayTranslator.exe") -Force

$configPath = Join-Path $sourceDir "TrayTranslator.exe.config"
if (Test-Path -LiteralPath $configPath) {
    Copy-Item -LiteralPath $configPath -Destination (Join-Path $installDir "TrayTranslator.exe.config") -Force
}

Copy-Item -LiteralPath (Join-Path $sourceDir "uninstall.ps1") -Destination (Join-Path $installDir "uninstall.ps1") -Force

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $installDir "TrayTranslator.exe"
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = "TrayTranslator"
$shortcut.Save()

New-Item -Path $uninstallKey -Force | Out-Null
Set-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value $appName
Set-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value "0.1.0"
Set-ItemProperty -Path $uninstallKey -Name "Publisher" -Value "xianyuzinc"
Set-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value $installDir
Set-ItemProperty -Path $uninstallKey -Name "DisplayIcon" -Value (Join-Path $installDir "TrayTranslator.exe")
Set-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value ("powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"" + (Join-Path $installDir "uninstall.ps1") + "`"")
New-ItemProperty -Path $uninstallKey -Name "NoModify" -PropertyType DWord -Value 1 -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "NoRepair" -PropertyType DWord -Value 1 -Force | Out-Null

Start-Process -FilePath (Join-Path $installDir "TrayTranslator.exe")
