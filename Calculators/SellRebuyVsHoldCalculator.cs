using ScenarioConsole.Utils;

namespace ScenarioConsole.Calculators;

/// <summary>
/// Compares “Sell now & rebuy at z% down” vs “Hold” at the crash trough.
/// Shows the break-even drop (tax ÷ current value) and a grid of outcomes.
/// Tax model supports the Canadian post–June 25, 2024 rules for individuals:
///   - First $250k of gains at 50% inclusion; amount above at 2/3 inclusion.
/// You can swap in your marginal tax rate.
/// </summary>
public sealed class SellRebuyVsHoldCalculator : ICalculator
{
    public string Name => "Sell & Rebuy vs Hold (with capital-gains tax)";

    public void Run()
    {
        // Inputs
        double mv0 = ConsoleEx.ReadDouble("Current market value (e.g., 1000000)", min: 0.01, @default: 1_000_000);
        double acb = ConsoleEx.ReadDouble("Adjusted cost base (e.g., 400000)", min: 0.0, @default: 400_000);
        double mtrPct = ConsoleEx.ReadDouble("Marginal tax rate on ordinary income % (e.g., 53.53)", min: 0, max: 100, @default: 53.53);
        double threshold = ConsoleEx.ReadDouble("Annual 50% inclusion threshold for individuals (CAD)", min: 0, @default: 250_000);
        double inclusionAbove = ConsoleEx.ReadDouble("Inclusion rate above threshold (e.g., 0.6667)", min: 0.0, max: 1.0, @default: 2.0/3.0);
        double zPct = ConsoleEx.ReadDouble("Buyback trigger z% (e.g., 20 for 20%)", min: 0, max: 100, @default: 20);
        double xPct = ConsoleEx.ReadDouble("Max crash x% to simulate (e.g., 60)", min: 0, max: 100, @default: 60);
        double yPct = ConsoleEx.ReadDouble("Step y% (e.g., 5)", min: 0.1, max: 100, @default: 5);

        // Core math
        double gain = Math.Max(mv0 - acb, 0);
        double taxable50 = Math.Min(gain, threshold) * 0.5;
        double taxable66 = Math.Max(gain - threshold, 0) * inclusionAbove;
        double taxable = taxable50 + taxable66;

        double tax = taxable * (mtrPct / 100.0);
        double afterTaxCash = mv0 - tax;
        double breakEvenDrop = tax / mv0; // fraction

        Console.WriteLine();
        ConsoleEx.WriteHeader("Summary");
        Console.WriteLine($"Gain:                {gain:N2}");
        Console.WriteLine($"Taxable (first 250k @50%):  {taxable50:N2}");
        Console.WriteLine($"Taxable (above @{inclusionAbove:P0}):  {taxable66:N2}");
        Console.WriteLine($"Total taxable:       {taxable:N2}");
        Console.WriteLine($"Tax (MTR {mtrPct}%): {tax:N2}");
        Console.WriteLine($"After-tax cash:      {afterTaxCash:N2}");
        Console.WriteLine($"Break-even drop:     {breakEvenDrop:P4} (≈ {breakEvenDrop*100:N2}%)");

        var rows = new List<string[]>();
        rows.Add(["Drop%", "Δ$ at trough (Sell&Rebuy − Hold)"]);

        double z = zPct / 100.0;
        for (double dPct = 0; dPct <= xPct + 1e-9; dPct += yPct)
        {
            double d = dPct / 100.0;
            double holdVal = mv0 * (1 - d);

            double stratVal;
            if (d >= z)
            {
                // You re-enter at z, then ride to the trough d.
                stratVal = afterTaxCash * (1 - d) / (1 - z);
            }
            else
            {
                // Never hits your trigger; you sit in cash at the trough.
                stratVal = afterTaxCash;
            }

            double delta = stratVal - holdVal;
            Console.WriteLine($"{dPct,6:N2}%  Δ$ = {delta,12:N2}  {(delta >= 0 ? "✓" : "✗")}");
            rows.Add([dPct.ToString("N2"), delta.ToString("N2")]);
        }

        Console.WriteLine();
        if (ConsoleEx.ReadYesNo("Export grid to CSV?", defaultYes: false))
        {
            var file = ConsoleEx.ReadNonEmpty("File name", "output.csv");
            File.WriteAllText(file, ConsoleEx.ToCsv(rows));
            ConsoleEx.WriteSuccess($"Saved: {Path.GetFullPath(file)}");
        }

        Console.WriteLine();
        ConsoleEx.WriteWarning("Note: This is a simplified model. It ignores AMT, the Ontario Health Premium, trading costs/spread, and sequence risk after re-entry. Adjust MTR/thresholds to your situation.");
    }
}
