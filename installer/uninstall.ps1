$ErrorActionPreference = "SilentlyContinue"

$installDir = Join-Path $env:LOCALAPPDATA "Programs\TrayTranslator"
$programsDir = [Environment]::GetFolderPath("Programs")
$shortcutPath = Join-Path $programsDir "TrayTranslator.lnk"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\TrayTranslator"

Get-Process -Name "TrayTranslator" -ErrorAction SilentlyContinue | Stop-Process -Force

Remove-Item -LiteralPath $shortcutPath -Force
Remove-Item -LiteralPath $uninstallKey -Recurse -Force

Set-Location $env:TEMP
Remove-Item -LiteralPath $installDir -Recurse -Force
