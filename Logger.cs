namespace Synk.Spotify;

internal class Logger
{
    internal void LogInfo(string message)
    {
        Console.WriteLine("[INFO] " + message);
    }

    internal void LogWarning(string warning)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[WARNING] " + warning);
        Console.ResetColor();
    }

    internal void LogError(string error)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine("[ERROR] " + error);
        Console.ResetColor();
    }
}