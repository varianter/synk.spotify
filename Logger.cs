namespace Synk.Spotify;

internal class Logger
{
    private readonly string? prefix;

    public Logger(string? prefix = null)
    {
        this.prefix = prefix;
    }
    internal void LogInfo(string message)
    {
        Console.WriteLine("[INFO] " + prefix + message);
    }

    internal void LogWarning(string warning)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[WARNING] " + prefix + warning);
        Console.ResetColor();
    }

    internal void LogError(string error)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine("[ERROR] " + prefix + error);
        Console.ResetColor();
    }
}
