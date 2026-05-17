$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $scriptDir
$distDir = Join-Path $root "dist"
$stagingDir = Join-Path $distDir "setup-staging"
$setupPath = Join-Path $distDir "TrayTranslatorSetup.exe"
$sedPath = Join-Path $distDir "TrayTranslatorSetup.sed"

function Find-MSBuild {
    $candidates = @(
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    $command = Get-Command "msbuild.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    throw "MSBuild was not found. Install Visual Studio Build Tools or open the solution in Visual Studio."
}

$msbuild = Find-MSBuild
& $msbuild (Join-Path $root "TrayTranslator.sln") /p:Configuration=Release /m
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed."
}

Remove-Item -LiteralPath $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

Copy-Item -LiteralPath (Join-Path $root "bin\Release\TrayTranslator.exe") -Destination $stagingDir -Force
Copy-Item -LiteralPath (Join-Path $root "bin\Release\TrayTranslator.exe.config") -Destination $stagingDir -Force
Copy-Item -LiteralPath (Join-Path $scriptDir "install.ps1") -Destination $stagingDir -Force
Copy-Item -LiteralPath (Join-Path $scriptDir "uninstall.ps1") -Destination $stagingDir -Force
Copy-Item -LiteralPath (Join-Path $scriptDir "install.cmd") -Destination $stagingDir -Force

$sedSetupPath = $setupPath
$sedStagingDir = $stagingDir.TrimEnd("\") + "\"

$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=1
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=TrayTranslator has been installed.
TargetName=$sedSetupPath
FriendlyName=TrayTranslator Setup
AppLaunched=install.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=install.cmd
UserQuietInstCmd=install.cmd
SourceFiles=SourceFiles
[Strings]
FILE0="TrayTranslator.exe"
FILE1="TrayTranslator.exe.config"
FILE2="install.ps1"
FILE3="uninstall.ps1"
FILE4="install.cmd"
[SourceFiles]
SourceFiles0=$sedStagingDir
[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
%FILE3%=
%FILE4%=
"@

[System.IO.File]::WriteAllText($sedPath, $sed, [System.Text.UTF8Encoding]::new($false))

$iexpress = Join-Path $env:WINDIR "System32\iexpress.exe"
if (!(Test-Path -LiteralPath $iexpress)) {
    throw "IExpress was not found on this Windows installation."
}

Remove-Item -LiteralPath $setupPath -Force -ErrorAction SilentlyContinue

$process = Start-Process -FilePath $iexpress -ArgumentList @("/N", "/Q", $sedPath) -Wait -PassThru
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $setupPath)) {
    $process = Start-Process -FilePath $iexpress -ArgumentList @("/N", "/Q", "/M", $sedPath) -Wait -PassThru
}

if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $setupPath)) {
    throw "IExpress failed to build the setup executable."
}

Get-Item -LiteralPath $setupPath
