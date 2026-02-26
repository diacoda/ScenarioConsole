using System.Globalization;
using System.Text;

namespace ScenarioConsole.Utils;

internal static class ConsoleEx
{
    public static void WriteHeader(string text)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n=== {text} ===");
        Console.ResetColor();
    }

    public static void WriteSuccess(string text)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static void WriteWarning(string text)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static void WriteError(string text)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static int ReadInt(string prompt, int? min = null, int? max = null, int? @default = null)
    {
        while (true)
        {
            var suffix = @default.HasValue ? $" [default {@default}]" : "";
            Console.Write($"{prompt}{suffix}: ");
            var input = Console.ReadLine();

            if (IsCancel(input)) throw new OperationCanceledException();
            if (string.IsNullOrWhiteSpace(input) && @default.HasValue) return @default.Value;

            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val))
            {
                if (min.HasValue && val < min.Value) { WriteWarning($"Must be >= {min}."); continue; }
                if (max.HasValue && val > max.Value) { WriteWarning($"Must be <= {max}."); continue; }
                return val;
            }
            WriteWarning("Please enter a valid integer (or 'q' to cancel).");
        }
    }

    public static double ReadDouble(string prompt, double? min = null, double? max = null, double? @default = null)
    {
        while (true)
        {
            var suffix = @default.HasValue ? $" [default {@default}]" : "";
            Console.Write($"{prompt}{suffix}: ");
            var input = Console.ReadLine();

            if (IsCancel(input)) throw new OperationCanceledException();
            if (string.IsNullOrWhiteSpace(input) && @default.HasValue) return @default.Value;

            if (double.TryParse(input, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var val))
            {
                if (min.HasValue && val < min.Value) { WriteWarning($"Must be >= {min}."); continue; }
                if (max.HasValue && val > max.Value) { WriteWarning($"Must be <= {max}."); continue; }
                return val;
            }
            WriteWarning("Please enter a valid number (or 'q' to cancel).");
        }
    }

    public static bool ReadYesNo(string prompt, bool defaultYes)
    {
        var hint = defaultYes ? "[Y/n]" : "[y/N]";
        while (true)
        {
            Console.Write($"{prompt} {hint}: ");
            var input = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
            if (IsCancel(input)) throw new OperationCanceledException();
            if (string.IsNullOrEmpty(input)) return defaultYes;

            if (input is "y" or "yes") return true;
            if (input is "n" or "no") return false;
            WriteWarning("Please answer y or n (or 'q' to cancel).");
        }
    }

    public static string ReadNonEmpty(string prompt, string? @default = null)
    {
        while (true)
        {
            var suffix = @default is not null ? $" [default '{@default}']" : "";
            Console.Write($"{prompt}{suffix}: ");
            var input = Console.ReadLine();

            if (IsCancel(input)) throw new OperationCanceledException();
            if (string.IsNullOrWhiteSpace(input))
            {
                if (@default is not null) return @default;
                WriteWarning("Please enter a value (or 'q' to cancel).");
                continue;
            }
            return input.Trim();
        }
    }

    public static string ToCsv(IEnumerable<string[]> rows)
    {
        static string Quote(string s)
        {
            if (s.Contains('"') || s.Contains(',') || s.Contains('\n'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        var sb = new StringBuilder();
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", row.Select(Quote)));
        }
        return sb.ToString();
    }

    private static bool IsCancel(string? input) =>
        string.Equals(input?.Trim(), "q", StringComparison.OrdinalIgnoreCase);
}