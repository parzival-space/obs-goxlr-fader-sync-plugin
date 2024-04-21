using System.Reflection;

namespace FaderSync;

public static class Module
{
    public static readonly string Name = Assembly.GetExecutingAssembly()?.GetName()?.Name!;

    public static readonly string Version = Assembly.GetExecutingAssembly()?
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion!
        .Split('+')[0]!; // remove commit hash
}