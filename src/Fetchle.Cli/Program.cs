using System.Reflection;

if (args is ["--version"])
{
    Console.WriteLine(typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);
    return 0;
}
return 2;
