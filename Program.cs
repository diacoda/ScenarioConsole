using ScenarioConsole.Calculators;
using ScenarioConsole.Utils;

namespace ScenarioConsole;

internal static class Program
{
    private static readonly List<ICalculator> Calculators =
    [
        new SellRebuyVsHoldCalculator(),       // existing one
        new SellTimingFromHereCalculator(),    // <-- NEW (from-here timing)
        new TemplateCalculator(),
    ];

    private static void Main()
    {
        ConsoleEx.WriteHeader("Scenario Console");
        Console.WriteLine("Use this app to run quick what-if scenarios with validated inputs.");

        while (true)
        {
            Console.WriteLine();
            ConsoleEx.WriteHeader("Main Menu");
            for (int i = 0; i < Calculators.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {Calculators[i].Name}");
            }
            Console.WriteLine("0. Exit");

            var choice = ConsoleEx.ReadInt("Choose an option", min: 0, max: Calculators.Count);
            if (choice == 0) break;

            var selected = Calculators[choice - 1];
            ConsoleEx.WriteHeader(selected.Name);

            try
            {
                selected.Run();
            }
            catch (OperationCanceledException)
            {
                ConsoleEx.WriteWarning("Operation cancelled.");
            }
            catch (Exception ex)
            {
                ConsoleEx.WriteError($"Unexpected error: {ex.Message}");
            }

            Console.WriteLine();
            if (!ConsoleEx.ReadYesNo("Run another scenario? (y/n)", defaultYes: true))
                break;
        }

        ConsoleEx.WriteSuccess("Goodbye!");
    }
}
