using FaderSync.OBS;
using GoXLRUtilityClient;

namespace FaderSync.GoXLR;

public static class UtilitySingleton
{
    private static readonly Logger Log = new Logger(typeof(Plugin), Module.Name);

    /// Minimum delay between two connection attempts.
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    private static readonly object Lock = new();

    private static Utility? _utility;
    private static Thread? _connectionThread;
    private static DateTime _lastAttempt = DateTime.MinValue;
    private static bool _reportedFailure;
    private static bool _shuttingDown;

    /**
     * Runs on a dedicated background thread and must never let an exception escape.
     * An unhandled exception on a background thread does not just fail the connection,
     * it terminates the entire OBS process.
     */
    private static void Connect()
    {
        var utility = _utility;
        if (utility == null) return;

        try
        {
            utility.ConnectAsync().GetAwaiter().GetResult();

            if (utility.IsConnectionAlive())
            {
                Log.Info($"Connected to GoXLR Utility v{utility.Status["config"]?["daemon_version"]}");
                _reportedFailure = false;
                return;
            }

            Report("Failed to connect to the GoXLR Utility.");
        }
        catch (UnauthorizedAccessException)
        {
            // The GoXLR Utility owns the named pipe. If the Utility runs elevated while OBS does not,
            // Windows' integrity policy denies write access to that pipe and we end up here.
            Report("Access to the GoXLR Utility was denied. This usually means the Utility runs as " +
                   "administrator while OBS does not - start both with the same privileges.");
        }
        catch (TimeoutException)
        {
            Report("The GoXLR Utility did not respond. Is it running?");
        }
        catch (Exception exception)
        {
            Report($"Could not connect to the GoXLR Utility ({exception.GetType().Name}: {exception.Message})");
        }
    }

    /**
     * Logs a connection problem once instead of on every retry, so we don't flood the OBS log.
     */
    private static void Report(string message)
    {
        if (_reportedFailure) return;
        _reportedFailure = true;
        Log.Warning($"{message} Retrying every {RetryInterval.TotalSeconds:0} seconds in the background.");
    }

    public static Utility GetInstance()
    {
        // Fast path: this is called from the filter's video_tick, so it runs once per frame per filter.
        var current = _utility;
        if (current != null && current.IsConnectionAlive()) return current;

        lock (Lock)
        {
            if (_utility == null)
            {
                _utility = new Utility();
                _utility.OnException += (_, exception) =>
                    Log.Error($"Something internally went wrong in the GoXLR Utility API Client: {exception.Message}");
            }

            if (_shuttingDown) return _utility;
            if (_utility.IsConnectionAlive()) return _utility;
            if (_connectionThread is { IsAlive: true }) return _utility;
            if (DateTime.UtcNow - _lastAttempt < RetryInterval) return _utility;

            _lastAttempt = DateTime.UtcNow;
            _connectionThread = new Thread(Connect)
            {
                IsBackground = true,
                Name = "FaderSync-GoXLR-Connect",
            };
            _connectionThread.Start();

            return _utility;
        }
    }

    /**
     * Called when OBS unloads the module. Stops reconnecting and tears the client down.
     */
    public static void Shutdown()
    {
        Utility? utility;
        lock (Lock)
        {
            _shuttingDown = true;
            utility = _utility;
            _utility = null;
        }

        try
        {
            utility?.Dispose();
        }
        catch (Exception exception)
        {
            Log.Warning($"Failed to shut down the GoXLR Utility client cleanly: {exception.Message}");
        }
    }
}
