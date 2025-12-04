#!/bin/bash

OBS_PLUGINS_DIR=~/.config/obs-studio/plugins
SOURCE_DIR=$PWD/FaderSyncPlugin/bin/Release/net9.0/linux-x64/package/package-src/FaderSyncPlugin
TARGET_DIR=$OBS_PLUGINS_DIR/FaderSyncPlugin

# build the plugin binaries
dotnet publish -r linux-amd64
if ! [ $? -eq 0 ]; then
	echo "Build Failed, aborting..";
	exit 1
fi

# kill obs if it's running
pidof obs > /dev/null
if [ $? -eq 0 ]; then
	kill `pidof obs`
	echo "OBS has been terminated"
fi

# copy plugin files into obs plugins directory
mkdir -p "$TARGET_DIR"
if [ -d $TARGET_DIR ]; then
  rm -rf "$TARGET_DIR"
fi
cp -r "$SOURCE_DIR/" "$TARGET_DIR"

# start obs
echo "Plugin installed. Starting OBS..."
obs &