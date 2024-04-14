$obsPath = "$env:ProgramFiles\obs-studio"

# Get the OBS process
$obsProcess = Get-Process | Where-Object { $_.ProcessName -eq "obs64" }

# Check if OBS is running
if ($obsProcess) {
    # Terminate OBS process
    Stop-Process -Id $obsProcess.Id
    Write-Host "OBS has been terminated."
} else {
    Write-Host "OBS is not running."
}

# Copy the DLL file to the OBS plugins directory
Copy-Item -Path ".\FaderSyncPlugin\bin\Release\net8.0\win-x64\publish\*" -Destination "$obsPath\obs-plugins\64bit" -Force
#Copy-Item -Path ".\UtilityClient\bin\Debug\net8.0\*" -Destination "$obsPath\obs-plugins\64bit" -Force

# Start OBS with the specified working directory
Start-Process -FilePath "$obsPath\bin\64bit\obs64.exe" -WorkingDirectory "$obsPath\bin\64bit" -Wait
