using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using FaderSync.GoXLR;
using ObsInterop;

namespace FaderSync.OBS;

// ReSharper disable once ClassNeverInstantiated.Global
public class GoXlrChannelSyncFilter
{
    private static readonly Logger Log = new(typeof(Plugin));

    /**
     * Registers the Filter in OBS.
     */
    public static unsafe void Register(string moduleBaseName)
    {
        var sourceInfo = new obs_source_info();
        fixed (byte* id = Encoding.UTF8.GetBytes($"{moduleBaseName}/{nameof(GoXlrChannelSyncFilter)}"))
        {
            sourceInfo.id = (sbyte*)id;
            sourceInfo.type = obs_source_type.OBS_SOURCE_TYPE_FILTER;
            sourceInfo.output_flags = ObsSource.OBS_SOURCE_AUDIO;
            sourceInfo.get_name = &GetName;
            sourceInfo.create = &Create;
            sourceInfo.destroy = &Destroy;
            sourceInfo.video_tick = &Tick;
            sourceInfo.update = &Update;
            sourceInfo.get_defaults = &GetDefaults;
            sourceInfo.get_properties = &GetProperties;

            ObsSource.obs_register_source_s(&sourceInfo, (nuint)Marshal.SizeOf(sourceInfo));
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe sbyte* GetName(void* data)
    {
        fixed (byte* namePtr = "Sync volume with GoXLR Channel"u8)
        {
            return (sbyte*)namePtr;
        }
    }

    /**
     * Initialized the Filter for OBS and also creates a context since this is not a class.
     * This function can be seen as some sort of constructor.
     */
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe void* Create(obs_data* settings, obs_source* source)
    {
        Log.Debug("Filter created!");

        var context = ObsBmem.bzalloc<FilterContext>();
        context->Source = source;
        context->Settings = settings;

        fixed (byte* sChannelNameId = "CHANNEL_NAME"u8.ToArray(), sDeviceSerialId = "DEVICE_SERIAL"u8.ToArray())
        {
            context->DeviceSerial = ObsData.obs_data_get_string(settings, (sbyte*)sDeviceSerialId);
            context->ChannelName = ObsData.obs_data_get_string(settings, (sbyte*)sChannelNameId);
        }

        return context;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe void Destroy(void* data)
    {
        Log.Debug("Filter destroyed!");

        var context = (FilterContext*)data;
        ObsBmem.bfree(context);
    }

    /**
     * Gets called every frame.
     * Requests current volume from the GoXLR Utility and translates the volume to the OBS volume scale.
     */
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe void Tick(void* data, float seconds)
    {
        var utility = UtilitySingleton.GetInstance();
        var context = (FilterContext*)data;

        var deviceSerial = Marshal.PtrToStringUTF8((IntPtr)context->DeviceSerial);
        var channelName = Marshal.PtrToStringUTF8((IntPtr)context->ChannelName);

        var target = Obs.obs_filter_get_parent(context->Source);
        var systemVolume = utility.Status["mixers"]?[deviceSerial ?? ""]?["levels"]?["volumes"]?[channelName ?? ""]?
            .GetValue<int>() ?? 0;

        // Ok, the GoXLR seems to decrease the volume by 1dB for every (on average) 4.85 volume steps, it
        // doesn't appear to be an exact science, but this should get us close enough to accurate for now.

        // So, start simply, how many multiples of 4.85 are we below max (number of dB we need to decrease by)?
        var utilityBase = (255f - systemVolume) / 4.85f;

        // Below 140, the adjustment increases, so we need to accommodate for that here.
        if (systemVolume < 140)
        {
            var count = 140 - systemVolume;
            utilityBase += count * 0.115f;
        }

        // Now we convert this into an OBS value...
        var obsVolume = (float)Math.Pow(10, -utilityBase / 20f);

        // check if channel is muted
        var isMuted = false;
        var faderStatus = (JsonObject?)utility.Status["mixers"]?[deviceSerial ?? ""]?["fader_status"];
        if (faderStatus != null)
            foreach (var faderEntry in faderStatus)
            {
                if (faderEntry.Value?["channel"]?.GetValue<string>() != channelName) continue;

                isMuted = faderEntry.Value?["mute_state"]?.GetValue<string>() == "MutedToAll" ||
                          (faderEntry.Value?["mute_state"]?.GetValue<string>() == "MutedToX" &&
                           (
                               faderEntry.Value?["mute_type"]?.GetValue<string>() == "ToStream" ||
                               faderEntry.Value?["mute_type"]?.GetValue<string>() == "All"
                           ));
                break;
            }

        // only update channel if values changed
        var oldMuteState = (byte)1 == Obs.obs_source_muted(target);
        var oldVolume = Obs.obs_source_get_volume(target);
        if (Math.Abs(oldVolume - obsVolume) > 0.0001 || isMuted != oldMuteState)
        {
            // Update OBS Volume
            Obs.obs_source_set_volume(target, obsVolume);
            Obs.obs_source_set_muted(target, isMuted ? (byte)1 : (byte)0);
            Log.Info($"Updated volume for channel '{channelName}' to {obsVolume} (muted: {isMuted})");
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe void GetDefaults(obs_data* settings)
    {
        // do nothing
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe obs_properties* GetProperties(void* data)
    {
        var properties = ObsProperties.obs_properties_create();
        var context = (FilterContext*)data;
        
        // Get the currently configured Serial as a String (we'll need this later)
        var serial = Marshal.PtrToStringUTF8((IntPtr)context->DeviceSerial);
        var deviceSerialError = "---ERROR---";

        fixed (byte*
            // device serial input
            sDeviceSerialId = "DEVICE_SERIAL"u8.ToArray(),
            sDeviceSerialDescription = "Device Serial"u8.ToArray(),

            // We need a 'Default' serial in case something is wrong when loading
            sDeviceSerialError = Encoding.UTF8.GetBytes(deviceSerialError),
            sDeviceSerialDisconnected = "Error Connecting to the GoXLR Utility"u8.ToArray(),
            sDeviceSerialNoDevices = "No GoXLR Devices Detected"u8.ToArray(),
            
            // add channels
            sChannelNameId = "CHANNEL_NAME"u8.ToArray(),
            sChannelNameDescription = "Channel Name"u8.ToArray(),

            // channel names
            sChannelMic = "Microphone"u8.ToArray(),
            sChannelMicId = "Mic"u8.ToArray(),
            sChannelChat = "Chat"u8.ToArray(),
            sChannelChatId = "Chat"u8.ToArray(),
            sChannelMusic = "Music"u8.ToArray(),
            sChannelMusicId = "Music"u8.ToArray(),
            sChannelGame = "Game"u8.ToArray(),
            sChannelGameId = "Game"u8.ToArray(),
            sChannelConsole = "Console"u8.ToArray(),
            sChannelConsoleId = "Console"u8.ToArray(),
            sChannelLineIn = "Line In"u8.ToArray(),
            sChannelLineInId = "LineIn"u8.ToArray(),
            sChannelSystem = "System"u8.ToArray(),
            sChannelSystemId = "System"u8.ToArray(),
            sChannelSample = "Samples / VOD"u8.ToArray(),
            sChannelSampleId = "Sample"u8.ToArray(),
            sChannelHeadphones = "Headphones"u8.ToArray(),
            sChannelHeadphonesId = "Headphones"u8.ToArray(),
            sChannelMicMonitor = "Mic Monitor"u8.ToArray(),
            sChannelMicMonitorId = "MicMonitor"u8.ToArray(),
            sChannelLineOut = "Line Out"u8.ToArray(),
            sChannelLineOutId = "LineOut"u8.ToArray()
            )
        {
            // Create the Serial Dropdown...
            var deviceList = ObsProperties.obs_properties_add_list(properties, (sbyte*)sDeviceSerialId, (sbyte*)sDeviceSerialDescription,
                obs_combo_type.OBS_COMBO_TYPE_LIST, obs_combo_format.OBS_COMBO_FORMAT_STRING);
            
            // channel selection list
            var channelList = ObsProperties.obs_properties_add_list(properties, (sbyte*)sChannelNameId,
                (sbyte*)sChannelNameDescription, obs_combo_type.OBS_COMBO_TYPE_LIST,
                obs_combo_format.OBS_COMBO_FORMAT_STRING);
            ObsProperties.obs_property_list_add_string(channelList, (sbyte*)sChannelMic, (sbyte*)sChannelMicId);
            ObsProperties.obs_property_list_add_string(channelList, (sbyte*)sChannelChat, (sbyte*)sChannelChatId);
            ObsProperties.obs_property_list_add_string(channelList, (sbyte*)sChannelMusic, (sbyte*)sChannelMusicId);
            ObsProperties.obs_property_list_add_string(channelList, (sbyte*)sChannelGame, (sbyte*)sChannelGameId);
            ObsProperties.obs_property_list_add_string(channelList, (sbyte*)sChannelConsole, (sbyte*)sChannelConsoleId);
            ObsProperties.obs_property_list_add_string(channelList, (sbyte*)sChannelLineIn, (sbyte*)sChannelLineInId);
            ObsProperties.obs_property_list_add_string(channelList, (sbyte*)sChannelSystem, (sbyte*)sChannelSystemId);
            ObsProperties.obs_property_list_add_string(channelList, (sbyte*)sChannelSample, (sbyte*)sChannelSampleId);
            ObsProperties.obs_property_list_add_string(channelList, (sbyte*)sChannelHeadphones, (sbyte*)sChannelHeadphonesId);
            ObsProperties.obs_property_list_add_string(channelList, (sbyte*)sChannelMicMonitor, (sbyte*)sChannelMicMonitorId);
            ObsProperties.obs_property_list_add_string(channelList, (sbyte*)sChannelLineOut, (sbyte*)sChannelLineOutId);
            
            // Before we Proceed, we need to fetch a list of the available GoXLRs on the System...
            var utility = UtilitySingleton.GetInstance();
            var mixers = (JsonObject?)utility.Status["mixers"];
            var locatedDevices = new ArrayList();
            var forcedSerial = false;

            // Iterate the status and add all the currently connected serials to a list.
            if (mixers != null) {
                foreach (var mixer in mixers) {
                    locatedDevices.Add(mixer.Key);
                }
            }

            // Get an initial count of devices which we'll use for stuff later!
            var locatedDeviceCount = locatedDevices.Count;

            // If the user has previously configured a GoXLR, but it's not currently attached to the Utility, we need to
            // force the serial into the list to prevent arbitrary device switching later on. We'll also flag this as a
            // forced entry, so we can appropriately label it.
            if (serial != "" && !locatedDevices.Contains(serial)) {
                locatedDevices.Add(serial);
                forcedSerial = true;
            }

            if (locatedDevices.Count == 0) {
                // We're in some kind of error state. Either the utility connection is broken or there are no GoXLRs attached, and the
                // user hasn't previously defined a GoXLR. In this case we'll forcibly add the 'Error' serial to the list, so we can
                // display the problem to the user in the drop-down.
                locatedDevices.Add(deviceSerialError);
            }

            // Start filling out the list...
            foreach (var located in locatedDevices) {
                fixed (byte* sSerial = Encoding.UTF8.GetBytes((string)located)) {
                    if (located.Equals(deviceSerialError) && mixers == null) {
                        // Unable to Connect to the Utility, no GoXLR previously configured in the Filter
                        ObsProperties.obs_property_list_add_string(deviceList, (sbyte*)sDeviceSerialDisconnected, (sbyte*)sDeviceSerialError);
                    } else if (located.Equals(deviceSerialError) && locatedDeviceCount == 0) {
                        // No GoXLR Devices Attached, no GoXLR previously configured in the Filter
                        ObsProperties.obs_property_list_add_string(deviceList, (sbyte*)sDeviceSerialNoDevices, (sbyte*)sDeviceSerialError);
                    } else if (located.Equals(deviceSerialError) && locatedDeviceCount > 0) {
                        // In this scenario we've left an Error State. By not pushing the Error Serial into the list OBS will automatically
                        // switch the dropdown to the first entry (a real GoXLR Serial) forcing an update and taking us out of the error state.
                    } else {
                        var title = (string)located;

                        // Has this device been forced into the located list due to it being disconnected?
                        if (forcedSerial && located.Equals(serial)) {
                            // We can do a *LOT* better than this and potentially check WHY it's disconnected...
                            title = $"{located} - Disconnected";
                        }
                        fixed(byte* sTitle = Encoding.UTF8.GetBytes(title)) {
                            ObsProperties.obs_property_list_add_string(deviceList, (sbyte*)sTitle, (sbyte*)sSerial);
                        }
                    }
                }
            }
        }

        return properties;
    }

    /**
     * Called when the user changes the settings for the filter
     */
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static unsafe void Update(void* data, obs_data* settings)
    {
        var context = (FilterContext*)data;

        fixed (byte* sChannelNameId = "CHANNEL_NAME"u8.ToArray(), sDeviceSerialId = "DEVICE_SERIAL"u8.ToArray())
        {
            context->DeviceSerial = ObsData.obs_data_get_string(settings, (sbyte*)sDeviceSerialId);
            context->ChannelName = ObsData.obs_data_get_string(settings, (sbyte*)sChannelNameId);
        }
    }

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    private unsafe struct FilterContext
    {
        public obs_source* Source;
        public obs_data* Settings;
        public bool* IsEnabled;

        public sbyte* DeviceSerial;
        public sbyte* ChannelName;
    }
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
}