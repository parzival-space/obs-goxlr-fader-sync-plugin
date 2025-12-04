# Builds and installs the FaderSyncPlugin for testing on Windows.
# Important: This has not been tested. Please create a PR if you run into any issues.

$obsPluginsDir = "$env:ProgramData\obs-studio\plugins"
$obsDir = "$env:ProgramFiles\obs-studio\bin\64bit"
$sourceDir = ".\FaderSyncPlugin\bin\Release\net9.0\win-x64\package\package-src\FaderSyncPlugin"
$targetDir = "$obsPluginsDirectory\FaderSyncPlugin"

# build the plugin binaries
dotnet publish -r win-x64 --self-contained
if ($LastExitCode -ne 0) {
    Write-Host "Build Failed, aborting.."
    exit 1
}

# kill obs if running
if (Get-Process | Where-Object { $_.MainModule.FileName -like "$obsProgramFile" }) {
    Stop-Process -Name obs64 -Force
    Write-Host "OBS has been terminated"
}

# copy plugin files into obs plugins directory
Remove-Item -Path "$targetDir" -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item -Recurse -Path "$sourceDir" -Destination "$targetDir" -Force

# start obs
Write-Host "Plugin installed. Starting OBS..."
Start-Process "$obsDirectory\obs64.exe" -WorkingDirectory "$obsDirectory"