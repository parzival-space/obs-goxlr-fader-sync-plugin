using System.Runtime.InteropServices;
using System.Text;
using FaderSync.GoXLR;
using GoXLRUtilityClient;
using ObsInterop;

namespace FaderSync.OBS;

// ReSharper disable once ClassNeverInstantiated.Global
public class GoXlrChannelSyncFilter
{
    private static readonly Logger Log = new Logger(typeof(Plugin));
    private const string ModuleName = "FaderSyncPlugin";

    unsafe struct FilterContext
    {
        public obs_source* Source;
        public obs_data* Settings;
        public bool* IsEnabled;

        public sbyte* DeviceSerial;
        public sbyte* ChannelName;
    }
    
    /**
     * Registers the Filter in OBS. 
     */
    public static unsafe void Register(String moduleBaseName)
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

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static unsafe sbyte* GetName(void* data)
    {
        fixed (byte* namePtr = "Sync volume with GoXLR Channel"u8)
            return (sbyte*)namePtr;
    }

    /**
     * Initialized the Filter for OBS and also creates a context since this is not a class.
     * This function can be seen as some sort of constructor.
     */
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
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
    
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
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
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static unsafe void Tick(void* data, float seconds)
    {
        var utility = UtilitySingleton.GetInstance();
        var context = (FilterContext*)data;

        var deviceSerial = Marshal.PtrToStringUTF8((IntPtr)context->DeviceSerial);
        var channelName = Marshal.PtrToStringUTF8((IntPtr)context->ChannelName);
        
        var target = Obs.obs_filter_get_parent(context->Source);
        var systemVolume = utility.Status?["mixers"]?[deviceSerial]?["levels"]?["volumes"]?[channelName]?
            .GetValue<int>() ?? 0;

        // the goxlr and obs volume scale don't match 1:1. the goxlr volume floor seems to be around -60db.
        // in obs this eqals to 0.001, so we have an effective range of 0.001 - 1.
        // this calculates the volume based on this range.
        //
        // goxlr_min = 0, goxlr_max = 255
        // obs_min = 0.001 (-60db), obs_max = 1 (0db)
        // ((goxlr_value - goxlr_min) / (goxlr_max - goxlr_min)) * (obs_max - obs_min) + obs_min
        float obsVolume = ((systemVolume - 0f) / (255f - 0f)) * (1f - 0.001f) + 0.001f;
        
        // update volume
        Obs.obs_source_set_volume(target, obsVolume);
    }
    
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static unsafe void GetDefaults(obs_data* settings)
    {
        // Todo: implement
    }
    
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static unsafe obs_properties* GetProperties(void* data)
    {
        var properties = ObsProperties.obs_properties_create();

        fixed (byte* 
            tWarnTitle = "Attention"u8.ToArray(),
            tWarnMessage = "The GoXLR Utility is currently not running."u8.ToArray(),

            // add channels
            sChannelNameId = "CHANNEL_NAME"u8.ToArray(),
            sChannelNameDescription = "Channel Name"u8.ToArray(),

            // device serial input
            sDeviceSerialId = "DEVICE_SERIAL"u8.ToArray(),
            sDeviceSerialDescription = "Device Serial"u8.ToArray())
        {
            // channel name text field
            ObsProperties.obs_properties_add_text(properties, (sbyte*)sChannelNameId, (sbyte*)sChannelNameDescription,
                obs_text_type.OBS_TEXT_DEFAULT);
            
            // device serial text field
            ObsProperties.obs_properties_add_text(properties, (sbyte*)sDeviceSerialId, (sbyte*)sDeviceSerialDescription,
                obs_text_type.OBS_TEXT_DEFAULT);

            //ObsProperties.obs_properties_add_text(properties, (sbyte*)tWarnTitle, (sbyte*)tWarnMessage, obs_text_type.OBS_TEXT_INFO);
        }

        return properties;
    }

    /**
     * Called when the user changes the settings for the filter
     */
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static unsafe void Update(void* data, obs_data* settings)
    {
        var context = (FilterContext*)data;

        fixed (byte* sChannelNameId = "CHANNEL_NAME"u8.ToArray(), sDeviceSerialId = "DEVICE_SERIAL"u8.ToArray())
        {
            context->DeviceSerial = ObsData.obs_data_get_string(settings, (sbyte*)sDeviceSerialId);
            context->ChannelName = ObsData.obs_data_get_string(settings, (sbyte*)sChannelNameId);
        }
    }
}