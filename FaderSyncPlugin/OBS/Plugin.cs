using System.Runtime.InteropServices;
using FaderSync.GoXLR;
using ObsInterop;

namespace FaderSync.OBS
{
    public static class Plugin
    {
        private static readonly Logger Log = new Logger(typeof(Plugin), Module.Name);

        [UnmanagedCallersOnly(EntryPoint = "obs_module_set_pointer",
            CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static unsafe void obs_module_set_pointer(obs_module* obsModulePointer)
        {
            // do nothing, needs to exist for OBS to load
        }

        [UnmanagedCallersOnly(EntryPoint = "obs_module_ver",
            CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static uint obs_module_ver()
        {
            // OBS builds its own LIBOBS_API_VER as (major << 24) | (minor << 16) | patch and rejects
            // any module that reports a *newer* major/minor than the running libobs:
            //
            //     uint32_t ver = mod.ver ? mod.ver() & 0xFFFF0000 : 0;
            //     if (ver > LIBOBS_API_VER) return MODULE_INCOMPATIBLE_VER;
            //
            // So we report the oldest version we support, not the version we were built against.
            // The previous code shifted the major version by 30 bits, which overflowed a uint and
            // produced garbage (30 -> 0x80000000, which OBS reads as "libobs 128.0" and refuses).
            const uint major = 30;
            const uint minor = 0;
            const uint patch = 0;

            return (major << 24) | (minor << 16) | patch;
        }

        [UnmanagedCallersOnly(EntryPoint = "obs_module_load",
            CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static bool obs_module_load()
        {
            Log.Info($"Loading {Module.Name} v{Module.Version}");
            
            Log.Info("Preparing Utility Client...");
            _ = UtilitySingleton.GetInstance();
            
            Log.Info("Loading filters...");
            GoXlrChannelSyncFilter.Register(Module.Name);
            
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
            UtilitySingleton.Shutdown();
        }

        [UnmanagedCallersOnly(EntryPoint = "obs_module_set_locale",
            CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static unsafe void obs_module_set_locale(char* locale)
        {
            // TODO: add locale support
        }

        [UnmanagedCallersOnly(EntryPoint = "obs_module_free_locale",
            CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        public static void obs_module_free_locale()
        {
            // do nothing, needs to exist for OBS to load
        }
    }
}