namespace Synk.Spotify;

public class Logger
{
    private readonly string? prefix;

    public Logger(string? prefix = null)
    {
        this.prefix = prefix;
    }
    public void LogInfo(string message)
    {
        Console.WriteLine("[INFO] " + prefix + message);
    }

    public void LogWarning(string warning)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[WARNING] " + prefix + warning);
        Console.ResetColor();
    }

    public void LogError(string error)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine("[ERROR] " + prefix + error);
        Console.ResetColor();
    }
}
