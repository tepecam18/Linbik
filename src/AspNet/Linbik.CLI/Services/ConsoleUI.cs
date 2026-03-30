namespace Linbik.CLI.Services;

/// <summary>
/// Console output helpers with colored formatting
/// </summary>
internal static class ConsoleUI
{
    public static void Success(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("✅ ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("❌ ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void Warning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("⚠️  ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void Info(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("ℹ️  ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void Step(string message)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write("🔗 ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void Header(string message)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"  {message}");
        Console.ResetColor();
        Console.WriteLine(new string('─', Math.Min(message.Length + 4, 60)));
    }

    public static string? Prompt(string message, string? defaultValue = null)
    {
        if (defaultValue != null)
            Console.Write($"  {message} [{defaultValue}]: ");
        else
            Console.Write($"  {message}: ");

        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? defaultValue : input;
    }

    public static bool Confirm(string message, bool defaultYes = true)
    {
        var hint = defaultYes ? "[Y/n]" : "[y/N]";
        Console.Write($"  {message} {hint}: ");
        var input = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(input))
            return defaultYes;

        return input is "y" or "yes" or "evet" or "e";
    }
}
