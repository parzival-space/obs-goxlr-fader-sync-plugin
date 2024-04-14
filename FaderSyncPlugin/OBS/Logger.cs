using System.Reflection;
using System.Text;
using ObsInterop;

namespace FaderSync.OBS;

public class Logger
{
    private static string _moduleName = "Unknown";
    
    public enum LogLevel
    {
        ERROR = ObsBase.LOG_ERROR,
        WARNING = ObsBase.LOG_WARNING,
        INFO = ObsBase.LOG_INFO,
        DEBUG = ObsBase.LOG_DEBUG,
    }
    
    private static unsafe void Log(LogLevel level, string text)
    {
        fixed (byte* logMessagePtr = Encoding.UTF8.GetBytes($"[{_moduleName}] {text.Replace("%", "%%")}"))
            ObsBase.blog((int)level, (sbyte*)logMessagePtr);
    }

    public static void SetModuleName(string newName)
    {
        _moduleName = newName;
    }
    
    private readonly string _className;
    
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public Logger(MemberInfo loggerClass, string moduleName)
    {
        _className = loggerClass.Name;
        Logger.SetModuleName(moduleName);
    }
    public Logger(MemberInfo loggerClass)
    {
        _className = loggerClass.Name;
    }

    public void Error(string message) => Log(LogLevel.ERROR, $"<{_className}> {message}");
    public void Warning(string message) => Log(LogLevel.WARNING, $"<{_className}> {message}");
    public void Info(string message) => Log(LogLevel.INFO, $"<{_className}> {message}");
    public void Debug(string message) => Log(LogLevel.DEBUG, $"<{_className}> {message}");
}