$obsPath = "$env:ProgramFiles\obs-studio"
$obsFile = "$obsPath\bin\64bit\obs64.exe"
$installPath = "C:\ProgramData\obs-studio\plugins\FaderSyncPlugin"

# Check if OBS is running
$obsProcess = Get-Process | Where-Object { $_.MainModule.FileName -like "$obsFile" }
Write-Host $obsProcess.Name
if ($obsProcess) {
    Stop-Process -Name obs64 -Force
    Write-Host "OBS has been terminated"
}
else {
    Write-Host "OBS is not running."
}

# Build the project, using dotnet publish to get native AOT binaries
dotnet publish -r win-x64 --self-contained
if ($LastExitCode -ne 0) {
    Write-Host "Build Failed, aborting.."
    exit 1
}

# Copy plugin to OBS plugin path
if (Test-Path $installPath) {
    Write-Host "Removing old plugin files"
    Remove-Item -Path "$installPath" -Recurse -Force
}
Write-Host "Copying new plugin files"
Copy-Item -Recurse -Path "./FaderSyncPlugin/bin/Release/net8.0/win-x64/package/package-src/FaderSyncPlugin" -Destination "$installPath" -Force

# Start OBS
Write-Host "Starting OBS"
Start-Process "$obsFile" -WorkingDirectory "$obsPath\bin\64bit"