using FaderSync.OBS;
using GoXLRUtilityClient;

namespace FaderSync.GoXLR;

public static class UtilitySingleton
{
    private static readonly Logger Log = new Logger(typeof(Plugin), Module.Name);
    
    private static Utility? _utility;
    private static Thread? _connectionThread = null;

    private static async void Connect()
    {
        // create tasks
        await _utility!.ConnectAsync();

        if (_utility.IsConnectionAlive())
        {
            Log.Info($"Connected to GoXLR Utility v{_utility.Status?["config"]?["daemon_version"]}");
        }
        else
        {
            Log.Warning("Failed to connect to GoXLR Utility. Retrying...");
        }
    }
    
    public static Utility GetInstance()
    {
        _utility ??= new Utility();
        if (_utility.IsConnectionAlive()) return _utility;

        _utility.OnException += (sender, exception) =>
        {
            Log.Error($"Something internally went wrong in the GoXLR Utility API Client: {exception.Message}");
        };

        if (_connectionThread is not { IsAlive: true } && !_utility.IsConnectionAlive())
        {
            _connectionThread = new Thread(Connect);
            _connectionThread.Start();
        }
        
        return _utility;
    }
}