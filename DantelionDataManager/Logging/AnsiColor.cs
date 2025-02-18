using System.Text.RegularExpressions;

namespace DantelionDataManager.Log
{
    public static class AnsiColor
    {
        public const string reset = "\x1B[0m";
        public const string black = "\x1B[30m";
        private static string ApplyColorToAll(string message, string color)
        {
#if DEBUG
            string[] result = Regex.Split(message, @"({[a-zA-Z]+})");
            string retval = string.Empty;
            for (int i = 0; i < result.Length; i++)
            {
                if (i % 2 == 0)
                {
                    retval += color + result[i] + reset;
                }
                else
                {
                    retval += result[i];
                }
            }
            return retval;
#else
            return message;
#endif
        }
        public static string PercentageCoverageColorLog(string msg, double percentage)
        {
            if (percentage >= 100)
            {
                return AnsiColor.BrightGreen(msg);
            }
            else if (percentage > 95)
            {
                return AnsiColor.Green(msg);
            }
            else if (percentage > 85)
            {
                return AnsiColor.BrightYellow(msg);
            }
            else if (percentage > 73)
            {
                return AnsiColor.Yellow(msg);
            }
            else if (percentage > 65)
            {
                return AnsiColor.BrightOrange(msg);
            }
            else if (percentage > 50)
            {
                return AnsiColor.Orange(msg);
            }
            else if (percentage > 40)
            {
                return AnsiColor.Red(msg);
            }
            else return AnsiColor.BrightRed(msg);
        }
        public static string PercentageFileSizeColorLog(string msg, double percentage)
        {
            if (percentage <= 100)
            {
                return AnsiColor.BrightGreen(msg);
            }
            else if (percentage <= 103)
            {
                return AnsiColor.Green(msg);
            }
            else if (percentage <= 110)
            {
                return AnsiColor.BrightYellow(msg);
            }
            else if (percentage <= 120)
            {
                return AnsiColor.BrightOrange(msg);
            }
            else
            {
                return AnsiColor.BrightRed(msg);
            }
        }

        public static string Black(string message) => ApplyColorToAll(message, black);
        public const string red = "\x1B[31m";
        public static string Red(string message) => ApplyColorToAll(message, red);
        public const string green = "\x1B[32m";
        public static string Green(string message) => ApplyColorToAll(message, green);
        public const string yellow = "\x1B[33m";
        public static string Yellow(string message) => ApplyColorToAll(message, yellow);
        public const string blue = "\x1B[34m";
        public static string Blue(string message) => ApplyColorToAll(message, blue);
        public const string magenta = "\x1B[35m";
        public static string Magenta(string message) => ApplyColorToAll(message, magenta);
        public const string cyan = "\x1B[36m";
        public static string Cyan(string message) => ApplyColorToAll(message, cyan);
        public const string white = "\x1B[37m";
        public static string White(string message) => ApplyColorToAll(message, white);
        public const string brightBlack = "\x1B[90m";
        public static string BrightBlack(string message) => ApplyColorToAll(message, brightBlack);
        public const string brightRed = "\x1B[91m";
        public static string BrightRed(string message) => ApplyColorToAll(message, brightRed);
        public const string brightGreen = "\x1B[92m";
        public static string BrightGreen(string message) => ApplyColorToAll(message, brightGreen);
        public const string brightYellow = "\x1B[93m";
        public static string BrightYellow(string message) => ApplyColorToAll(message, brightYellow);
        public const string brightBlue = "\x1B[94m";
        public static string BrightBlue(string message) => ApplyColorToAll(message, brightBlue);
        public const string brightMagenta = "\x1B[95m";
        public static string BrightMagenta(string message) => ApplyColorToAll(message, brightMagenta);
        public const string brightCyan = "\x1B[96m";
        public static string BrightCyan(string message) => ApplyColorToAll(message, brightCyan);
        public const string brightWhite = "\x1B[97m";
        public static string BrightWhite(string message) => ApplyColorToAll(message, brightWhite);
        public const string backgroundBlack = "\x1B[40m";
        public static string BackgroundBlack(string message) => ApplyColorToAll(message, backgroundBlack);
        public const string backgroundRed = "\x1B[41m";
        public static string BackgroundRed(string message) => ApplyColorToAll(message, backgroundRed);
        public const string backgroundGreen = "\x1B[42m";
        public static string BackgroundGreen(string message) => ApplyColorToAll(message, backgroundGreen);
        public const string backgroundYellow = "\x1B[43m";
        public static string BackgroundYellow(string message) => ApplyColorToAll(message, backgroundYellow);
        public const string backgroundBlue = "\x1B[44m";
        public static string BackgroundBlue(string message) => ApplyColorToAll(message, backgroundBlue);
        public const string backgroundMagenta = "\x1B[45m";
        public static string BackgroundMagenta(string message) => ApplyColorToAll(message, backgroundMagenta);
        public const string backgroundCyan = "\x1B[46m";
        public static string BackgroundCyan(string message) => ApplyColorToAll(message, backgroundCyan);
        public const string backgroundWhite = "\x1B[47m";
        public static string BackgroundWhite(string message) => ApplyColorToAll(message, backgroundWhite);
        public const string backgroundBrightBlack = "\x1B[100m";
        public static string BackgroundBrightBlack(string message) => ApplyColorToAll(message, backgroundBrightBlack);
        public const string backgroundBrightRed = "\x1B[101m";
        public static string BackgroundBrightRed(string message) => ApplyColorToAll(message, backgroundBrightRed);
        public const string backgroundBrightGreen = "\x1B[102m";
        public static string BackgroundBrightGreen(string message) => ApplyColorToAll(message, backgroundBrightGreen);
        public const string backgroundBrightYellow = "\x1B[103m";
        public static string BackgroundBrightYellow(string message) => ApplyColorToAll(message, backgroundBrightYellow);
        public const string backgroundBrightBlue = "\x1B[104m";
        public static string BackgroundBrightBlue(string message) => ApplyColorToAll(message, backgroundBrightBlue);
        public const string backgroundBrightMagenta = "\x1B[105m";
        public static string BackgroundBrightMagenta(string message) => ApplyColorToAll(message, backgroundBrightMagenta);
        public const string backgroundBrightCyan = "\x1B[106m";
        public static string BackgroundBrightCyan(string message) => ApplyColorToAll(message, backgroundBrightCyan);
        public const string backgroundBrightWhite = "\x1B[107m";
        public static string BackgroundBrightWhite(string message) => ApplyColorToAll(message, backgroundBrightWhite);
        public const string orange = "\x1B[38;5;208m";
        public static string Orange(string message) => ApplyColorToAll(message, orange);

        public const string pink = "\x1B[38;5;205m";
        public static string Pink(string message) => ApplyColorToAll(message, pink);

        public const string gray = "\x1B[38;5;240m";
        public static string Gray(string message) => ApplyColorToAll(message, gray);

        public const string brown = "\x1B[38;5;130m";
        public static string Brown(string message) => ApplyColorToAll(message, brown);
        public const string purple = "\x1B[38;5;129m";
        public static string Purple(string message) => ApplyColorToAll(message, purple);

        public const string lilac = "\x1B[38;5;183m";
        public static string Lilac(string message) => ApplyColorToAll(message, lilac);

        public const string neonPink = "\x1B[38;5;207m";
        public static string NeonPink(string message) => ApplyColorToAll(message, neonPink);

        public const string neonRed = "\x1B[38;5;196m";
        public static string NeonRed(string message) => ApplyColorToAll(message, neonRed);
        public const string brightOrange = "\x1B[38;5;202m";
        public static string BrightOrange(string message) => ApplyColorToAll(message, brightOrange);
        public const string fadedOrange = "\x1B[38;5;216m";
        public static string FadedOrange(string message) => ApplyColorToAll(message, fadedOrange);
    }
}
