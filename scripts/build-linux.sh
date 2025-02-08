#!/bin/bash

OBS_PLUGIN_PATH=~/.config/obs-studio/plugins/FaderSyncPlugin

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
mkdir -p "$(dirname $OBS_PLUGIN_PATH)"

if [ -d $OBS_PLUGIN_PATH ]; then
  echo "Removing old plugin files"
  rm -rf "$OBS_PLUGIN_PATH"
fi

echo "Copying new plugin files"
cp -r "./FaderSyncPlugin/bin/Release/net8.0/win-x64/package/package-src/FaderSyncPlugin" "$OBS_PLUGIN_PATH"

# Fire up OBS semi-detatched, logs will go here, but you can still run shell commands :p
obs &
