using System.Reflection;

namespace Fetchle.Cli.Commands;

public sealed record CliEnvironment(
    string CurrentDirectory,
    bool StdoutRedirected,
    Func<string, string?> GetVariable,
    TimeProvider Clock,
    string Version)
{
    public static CliEnvironment FromProcess() => new(
        Environment.CurrentDirectory,
        Console.IsOutputRedirected,
        Environment.GetEnvironmentVariable,
        TimeProvider.System,
        typeof(CliEnvironment).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);
}
