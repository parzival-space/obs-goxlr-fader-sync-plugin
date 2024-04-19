#!/bin/bash

OBS_PATH=~/.config/obs-studio/plugins/obs-goxlr-fader-sync/bin/64bit

# Check if OBS is running..
pidof obs
if [ $? -eq 0 ]; then
	kill `pidof obs`
	echo "OBS has been terminated"
else
	echo "OBS is not running."
fi

# Build the project, we need to use dotnet publish to get native AOT binaries..
dotnet publish -r linux-amd64
if ! [ $? -eq 0 ]; then
	echo "Build Failed, aborting..";
	exit 1
fi

# Make sure the plugin path exists..
mkdir -p $OBS_PATH

# Needs specific naming..
cp FaderSyncPlugin/bin/Release/net8.0/linux-x64/publish/FaderSyncPlugin.so $OBS_PATH/obs-goxlr-fader-sync.so

# Fire up OBS semi-detatched, logs will go here, but you can still run shell commands :p
obs &
