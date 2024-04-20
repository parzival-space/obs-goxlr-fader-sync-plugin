using System.Runtime.InteropServices;
using FaderSync.GoXLR;
using ObsInterop;

namespace FaderSync.OBS
{
    public static class Plugin
    {
        private const string ModuleName = "FaderSyncPlugin";
        
        private static readonly Logger Log = new Logger(typeof(Plugin), ModuleName);

        [UnmanagedCallersOnly(EntryPoint = "obs_module_set_pointer",
            CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static unsafe void obs_module_set_pointer(obs_module* obsModulePointer)
        {

        }

        [UnmanagedCallersOnly(EntryPoint = "obs_module_ver",
            CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static uint obs_module_ver()
        {
            var major = (uint)Obs.Version.Major;
            var minor = (uint)Obs.Version.Minor;
            var patch = (uint)Obs.Version.Build;
            var version = (major << 24) | (minor << 16) | patch;
            return version;
        }

        [UnmanagedCallersOnly(EntryPoint = "obs_module_load",
            CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static unsafe bool obs_module_load()
        {
            Log.Info("Loading Plugin...");
            
            Log.Info("Preparing Utility Client...");
            _ = UtilitySingleton.GetInstance();
            
            Log.Info("Loading filters...");
            GoXlrChannelSyncFilter.Register(ModuleName);
            
            Log.Info("Preloading complete.");
            return true;
        }

        [UnmanagedCallersOnly(EntryPoint = "obs_module_post_load",
            CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static void obs_module_post_load()
        {
            Log.Info("Plugin loaded!");
        }

        [UnmanagedCallersOnly(EntryPoint = "obs_module_unload",
            CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static void obs_module_unload()
        {
            Log.Info("Plugin unloading...");
            UtilitySingleton.GetInstance().Dispose();
        }

        [UnmanagedCallersOnly(EntryPoint = "obs_module_set_locale",
            CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static unsafe void obs_module_set_locale(char* locale)
        {

        }

        [UnmanagedCallersOnly(EntryPoint = "obs_module_free_locale",
            CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static unsafe void obs_module_free_locale()
        {

        }
    }
}