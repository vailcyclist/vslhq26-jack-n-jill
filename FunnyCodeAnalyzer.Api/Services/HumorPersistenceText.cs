namespace FunnyCodeAnalyzer.Api.Services;

internal static class HumorPersistenceText
{
    public static string Apply(string baseText, int occurrenceCount)
    {
        if (string.IsNullOrWhiteSpace(baseText))
        {
            baseText = "The analyzer found something worth fixing.";
        }

        if (occurrenceCount <= 1)
        {
            return baseText;
        }

        var persistenceLine = occurrenceCount switch
        {
            <= 3 => $"Friendly reminder: this is the {ToOrdinal(occurrenceCount)} time this issue has shown up for you.",
            <= 6 => $"Persistent reminder: this issue has appeared {occurrenceCount} times for your token.",
            _ => $"Insistent reminder: this issue has now appeared {occurrenceCount} times. It is officially a recurring character."
        };

        return string.Join(Environment.NewLine, new[] { baseText, persistenceLine });
    }

    private static string ToOrdinal(int value)
    {
        var absValue = Math.Abs(value);
        var rem100 = absValue % 100;
        if (rem100 is >= 11 and <= 13)
        {
            return value + "th";
        }

        return (absValue % 10) switch
        {
            1 => value + "st",
            2 => value + "nd",
            3 => value + "rd",
            _ => value + "th"
        };
    }
}
