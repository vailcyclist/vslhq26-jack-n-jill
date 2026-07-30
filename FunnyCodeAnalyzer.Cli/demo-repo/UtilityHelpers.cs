namespace DemoRepo;

public static class UtilityHelpers
{
    public static string BuildSummary(string name)
    {
        // TODO: this is intentionally left noisy for the demo.
        return $"Summary for {name}";
    }

    public static void HandleException()
    {
        try
        {
            throw new InvalidOperationException("Demo failure");
        }
        catch (Exception)
        {
        }
    }

    public static void LongMethodExample()
    {
        var total = 0;
        total += 1;
        total += 2;
        total += 3;
        total += 4;
        total += 5;
        total += 6;
        total += 7;
        total += 8;
        total += 9;
        total += 10;
        total += 11;
        total += 12;
        total += 13;
        total += 14;
        total += 15;
        total += 16;
        total += 17;
        total += 18;
        total += 19;
        total += 20;
        total += 21;
        total += 22;
        total += 23;
        total += 24;
        total += 25;
        total += 26;
        total += 27;
        total += 28;
        total += 29;
        total += 30;
        Console.WriteLine(total);
    }
}
