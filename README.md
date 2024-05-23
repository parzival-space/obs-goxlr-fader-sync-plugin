# GoXLR Fader Sync Plugin
An OBS Studio Plugin that enables synchronizing the volume slider of an audio source with an audio channel from your GoXLR. 
This allows you to manage the volume of audio sources using the GoXLR's faders.

This plugin is essentially a port of the [goxlr-obs-fader-sync](https://github.com/FrostyCoolSlug/goxlr-obs-fader-sync) 
project developed by [FrostyCoolSlug](https://github.com/FrostyCoolSlug) into an OBS plugin.

## Requirements
* [OBS Studio](https://obsproject.com/) v30.0+
* [GoXLR Utility](https://github.com/GoXLR-on-Linux/goxlr-utility) v1.0.0+
* [.NET Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) 8.0+

Note: While the plugin may work with some older versions of OBS Studio and GoXLR Utility, 
it was tested and developed with the above versions in mind.

### Additional Development Requirements
If you plan on contributing to this project, you will also need to fulfill the .NET Native AOT requirements. 
You can find a detailed list and installation instructions [here](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/?tabs=net8plus%2Cwindows#prerequisites).

## Installation

Detailed Installation Instructions can be found in the [Wiki Pages](https://github.com/parzival-space/obs-goxlr-fader-sync-plugin/wiki/Installation).

Binaries for Windows and Linux are available in the Releases section. 
Once downloaded, place the binaries into the appropriate plugin directory:

<b>Windows</b>
```
C:\ProgramData\obs-studio\plugins\
```

<b>Linux</b>
```
~/.config/obs-studio/plugins
```

## Usage-Guide
Please refer to the [Wiki Pages](https://github.com/parzival-space/obs-goxlr-fader-sync-plugin/wiki/Usage-Guide).

## Contributing
Contributions to this project are welcome. 
To simplify building the project, there are two build scripts in the repository that create the 
binary file and automatically install them into your OBS.

To contribute, follow these steps:
1. [Fork](https://github.com/parzival-space/obs-goxlr-fader-sync-plugin/fork) this repository.
2. Create a new feature branch.
3. Make your changes.
4. Create a pull request.

## Special Thanks
Special thanks to the following individuals who made this project possible:
* <b>[FrostyCoolSlug](https://github.com/FrostyCoolSlug)</b>: For creating the GoXLR Utility and assisting in the creation of this plugin.
* <b>[YorVeX](https://github.com/YorVeX/)</b>: For investigating how to create OBS plugins using .NET and documenting the process. Also, a huge thank you for creating the .NET bindings for the OBS API.
