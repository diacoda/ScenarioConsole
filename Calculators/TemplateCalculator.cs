using ScenarioConsole.Utils;

namespace ScenarioConsole.Calculators;

/// <summary>
/// Skeleton you can copy to implement any custom scenario.
/// Demonstrates validated input, looping, and CSV export.
/// </summary>
public sealed class TemplateCalculator : ICalculator
{
    public string Name => "Template Calculator (copy me)";

    public void Run()
    {
        // 1) Read inputs
        double a = ConsoleEx.ReadDouble("Enter 'a' (number)", @default: 10);
        double b = ConsoleEx.ReadDouble("Enter 'b' (number)", @default: 2);
        int n = ConsoleEx.ReadInt("How many rows to generate?", min: 1, max: 10000, @default: 5);

        // 2) Do some work (replace with your logic)
        // Example: compute a simple sequence f(i) = a * i^b
        List<string[]> rows = new()
        {
            new[] { "i", "f(i) = a * i^b" }
        };

        for (int i = 1; i <= n; i++)
        {
            double val = a * Math.Pow(i, b);
            Console.WriteLine($"i={i,4}  f(i)={val,12:N4}");
            rows.Add([i.ToString(), val.ToString("N4")]);
        }

        // 3) Optional export
        Console.WriteLine();
        if (ConsoleEx.ReadYesNo("Export to CSV?", defaultYes: false))
        {
            var file = ConsoleEx.ReadNonEmpty("File name", "template.csv");
            File.WriteAllText(file, ConsoleEx.ToCsv(rows));
            ConsoleEx.WriteSuccess($"Saved: {Path.GetFullPath(file)}");
        }
    }
}