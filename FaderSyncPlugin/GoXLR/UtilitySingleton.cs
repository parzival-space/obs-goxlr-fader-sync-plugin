using FaderSync.OBS;
using GoXLRUtilityClient;

namespace FaderSync.GoXLR;

public static class UtilitySingleton
{
    private static readonly Logger Log = new Logger(typeof(Plugin), Module.Name);
    
    private static Utility? _utility;
    public static Utility GetInstance()
    {
        _utility ??= new Utility();
        if (_utility.IsConnectionAlive()) return _utility;

        _utility.OnException += (sender, exception) =>
        {
            Log.Error($"Something internally went wrong in the GoXLR Utility API Client: {exception.Message}");
        };

        _utility.OnMessage += (sender, s) =>
        {
            Log.Info($"Data: {s}");
            Log.Info(_utility.Status?["mixers"]?["S210816051CQK"]?["levels"]?["volumes"]?.ToJsonString()!);
        };
        
        // create new GoXLR Utility Client
        Log.Warning("Utility not connected. Connecting...");
        if (_utility.ConnectAsync().Wait(TimeSpan.FromSeconds(5)))
        {
            Log.Info($"Connected to GoXLR Utility v{_utility.Status?["config"]?["daemon_version"]}");
        }
        else
        {
            Log.Error("Failed to connect to GoXLR Utility after waiting for 5 seconds.");
        }
        
        return _utility;
    }
}