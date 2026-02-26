using ScenarioConsole.Utils;

namespace ScenarioConsole.Calculators;

/// <summary>
/// Models timing decisions FROM TODAY (after a prior drop may already have occurred).
/// Compares value at the trough for:
///   - Sell NOW and rebuy at z%
///   - Sell LATER at a chosen future drop s% and rebuy at z%
///   - LADDER: sell a fixed fraction at each rung (every r% down) until z%, then rebuy once at z%
/// Versus HOLD from today.
/// 
/// Tax model (simplified, individual):
///   - First 'threshold' CAD of annual net gains at 50% inclusion
///   - Remainder at 'inclusionAbove' (default 2/3)
///   - Marginal tax rate input by user (% on ordinary income)
/// Notes:
///   - Ignores AMT and health premium; ignores transaction costs/slippage
///   - Capital losses are not credited (tax floored at 0 for negative gains)
///   - When re-entry at z% happens, no more sells occur thereafter
/// </summary>
public sealed class SellTimingFromHereCalculator : ICalculator
{
    public string Name => "Sell Timing FROM HERE: now / later / ladder (vs Hold)";

    private enum Mode
    {
        SellNow = 1,
        SellLater = 2,
        Ladder = 3
    }

    public void Run()
    {
        // Core inputs
        double mv0 = ConsoleEx.ReadDouble("Current market value (today)", min: 0.01, @default: 1_000_000);
        double acb = ConsoleEx.ReadDouble("Adjusted cost base (ACB)", min: 0.0, @default: 400_000);
        double mtrPct = ConsoleEx.ReadDouble("Marginal tax rate on ordinary income %", min: 0, max: 100, @default: 53.53);
        double threshold = ConsoleEx.ReadDouble("Annual 50% inclusion threshold (CAD)", min: 0, @default: 250_000);
        double inclusionAbove = ConsoleEx.ReadDouble("Inclusion rate above threshold (e.g., 0.5)", min: 0.0, max: 1.0, @default: 1.0 / 2.0);

        // Simulation range
        double xPct = ConsoleEx.ReadDouble("Max further drop from TODAY x% to simulate", min: 0, max: 100, @default: 60);
        double yPct = ConsoleEx.ReadDouble("Step y%", min: 0.1, max: 100, @default: 5);

        // Strategy selection
        ConsoleEx.WriteHeader("Strategy Mode");
        Console.WriteLine("1) Sell NOW and rebuy at z%");
        Console.WriteLine("2) Sell LATER at s% and rebuy at z%");
        Console.WriteLine("3) LADDER: sell frac each rung until z%, then rebuy once at z%");
        int modeInt = ConsoleEx.ReadInt("Choose 1, 2, or 3", min: 1, max: 3, @default: 1);
        var mode = (Mode)modeInt;

        // Common re-entry trigger (all modes)
        double zPct = ConsoleEx.ReadDouble("Re-entry trigger z% (rebuy when price is down z% from today)", min: 0, max: 100, @default: 20);

        // Mode-specific inputs
        double sPct = 0.0;
        double ladderStepPct = 0.0;
        double ladderSellFracPct = 0.0;

        switch (mode)
        {
            case Mode.SellNow:
                // no extra inputs
                break;

            case Mode.SellLater:
                sPct = ConsoleEx.ReadDouble("Sell later at further drop s% from today", min: 0, max: 100, @default: 10);
                break;

            case Mode.Ladder:
                ladderStepPct = ConsoleEx.ReadDouble("Ladder rung step r% (sell every r% drop from today)", min: 0.1, max: 100, @default: 5);
                ladderSellFracPct = ConsoleEx.ReadDouble("Sell fraction per rung (as % of ORIGINAL position, e.g., 20)", min: 0.1, max: 100, @default: 20);
                break;
        }

        // Prepare header
        Console.WriteLine();
        ConsoleEx.WriteHeader("At-Trough Comparison (Strategy − Hold)");
        Console.WriteLine("Positive Δ$ means the chosen strategy is better than simply holding from TODAY.");
        Console.WriteLine($"Inputs: MV={mv0:N2}, ACB={acb:N2}, MTR={mtrPct}%, threshold={threshold:N0}, inclusionAbove={inclusionAbove:P0}, z={zPct}%");
        if (mode == Mode.SellLater) Console.WriteLine($"Mode: Sell at s={sPct}% then rebuy at z={zPct}%");
        if (mode == Mode.Ladder) Console.WriteLine($"Mode: Ladder sells every {ladderStepPct}% by {ladderSellFracPct}% of original, until z={zPct}% (then rebuy once)");

        // Loop through trough depths from 0..x
        for (double dPct = 0; dPct <= xPct + 1e-9; dPct += yPct)
        {
            double d = dPct / 100.0; // trough depth from today
            double holdValAtTrough = mv0 * (1 - d);

            double strategyValAtTrough = mode switch
            {
                Mode.SellNow   => SimSellNow(mv0, acb, mtrPct, threshold, inclusionAbove, zPct / 100.0, d),
                Mode.SellLater => SimSellLater(mv0, acb, mtrPct, threshold, inclusionAbove, sPct / 100.0, zPct / 100.0, d),
                Mode.Ladder    => SimLadder(mv0, acb, mtrPct, threshold, inclusionAbove, ladderStepPct / 100.0, ladderSellFracPct / 100.0, zPct / 100.0, d),
                _ => holdValAtTrough
            };

            double delta = strategyValAtTrough - holdValAtTrough;
            Console.WriteLine($"{dPct,6:N2}%  Δ$ = {delta,12:N2}  {(delta >= 0 ? "✓" : "✗")}");
        }

        Console.WriteLine();
        ConsoleEx.WriteWarning("Notes: Taxes applied on total realized gains by the time of each trough; capital losses ignored; no trading costs/slippage; re-entry stops further ladder sells.");
    }

    // -------- Simulation helpers --------

    private static double SimSellNow(
        double mv0, double acb, double mtrPct, double threshold, double inclusionAbove, double z, double d)
    {
        // Sell 100% at depth 0
        var events = new List<SellEvent> { new SellEvent(depth: 0.0, sellOrigFraction: 1.0) };
        return ValueWithEventsAndReentry(mv0, acb, mtrPct, threshold, inclusionAbove, events, z, d);
    }

    private static double SimSellLater(
        double mv0, double acb, double mtrPct, double threshold, double inclusionAbove, double s, double z, double d)
    {
        // Sell 100% at depth s (if reached)
        var events = new List<SellEvent> { new SellEvent(depth: s, sellOrigFraction: 1.0) };
        return ValueWithEventsAndReentry(mv0, acb, mtrPct, threshold, inclusionAbove, events, z, d);
    }

    private static double SimLadder(
        double mv0, double acb, double mtrPct, double threshold, double inclusionAbove,
        double rungStep, double sellFracPerRung, double z, double d)
    {
        var events = new List<SellEvent>();
        // Build ladder rungs at rungStep, 2*rungStep, ..., up to (but not exceeding) z.
        // We only sell UNTIL re-entry; once z is hit, no more sells.
        for (double depth = rungStep; depth <= z + 1e-9; depth += rungStep)
        {
            events.Add(new SellEvent(depth: depth, sellOrigFraction: sellFracPerRung));
        }
        return ValueWithEventsAndReentry(mv0, acb, mtrPct, threshold, inclusionAbove, events, z, d);
    }

    private sealed record SellEvent(double depth, double sellOrigFraction);

    /// <summary>
    /// Simulates sells at specified depths (fractions of ORIGINAL position), then a single re-entry at z if reached,
    /// and returns portfolio value at trough depth d.
    /// - If d &lt; z: no re-entry; value = unsold shares at trough + (grossProceeds − tax).
    /// - If d ≥ z: re-entry at z with after-tax cash from sells at depths ≤ z; then value at trough includes:
    ///       remaining original shares (not sold by z) at trough + reinvested cash grown from z to d.
    /// Taxes applied on total realized gains up to the point considered (≤ d or ≤ z, depending on case).
    /// Capital losses do not create tax refunds (tax floored at zero).
    /// </summary>
    private static double ValueWithEventsAndReentry(
        double mv0, double acb, double mtrPct, double threshold, double inclusionAbove,
        List<SellEvent> sellEvents, double z, double d)
    {
        // Sort events by depth just in case
        sellEvents.Sort((a, b) => a.depth.CompareTo(b.depth));

        double remainingOrig = 1.0; // remaining fraction of ORIGINAL position not sold (up to z)
        double grossProceeds = 0.0;
        double realizedGain = 0.0;

        // Helper to process sells up to a certain depth limit
        void ProcessSellsUpTo(double depthLimit)
        {
            foreach (var ev in sellEvents)
            {
                if (ev.depth > depthLimit + 1e-12) break;
                if (remainingOrig <= 1e-12) break;

                double sellFrac = Math.Min(ev.sellOrigFraction, remainingOrig);
                if (sellFrac <= 0) continue;

                double priceFactor = 1.0 - ev.depth; // price relative to today
                double proceeds = mv0 * sellFrac * priceFactor;
                double allocAcb = acb * sellFrac;
                double gain = proceeds - allocAcb;

                grossProceeds += proceeds;
                realizedGain += gain;
                remainingOrig -= sellFrac;
            }
        }

        // Case 1: trough occurs before re-entry trigger
        if (d < z - 1e-12)
        {
            ProcessSellsUpTo(d);
            double tax = Math.Max(0.0, TaxOnGain(realizedGain, mtrPct, threshold, inclusionAbove));
            double cash = grossProceeds - tax;
            double valueUnsold = mv0 * remainingOrig * (1.0 - d);
            return valueUnsold + cash;
        }

        // Case 2: trough at/after re-entry
        ProcessSellsUpTo(z);
        double taxAtZ = Math.Max(0.0, TaxOnGain(realizedGain, mtrPct, threshold, inclusionAbove));
        double cashForReentry = grossProceeds - taxAtZ;

        // Invest all after-tax cash at z; it then rides to trough d
        double investedFromCashAtTrough = cashForReentry * ((1.0 - d) / (1.0 - z));

        // Remaining original shares (not sold by z) ride to trough as well
        double remainingOriginalAtTrough = mv0 * remainingOrig * (1.0 - d);

        // No further sells after re-entry in this policy
        return remainingOriginalAtTrough + investedFromCashAtTrough;
    }

    private static double TaxOnGain(double gain, double mtrPct, double threshold, double inclusionAbove)
    {
        if (gain <= 1e-12) return 0.0;
        double incl50 = Math.Min(gain, threshold) * 0.5;
        double inclHi  = Math.Max(gain - threshold, 0.0) * inclusionAbove;
        double taxable = incl50 + inclHi;
        return taxable * (mtrPct / 100.0);
    }
}