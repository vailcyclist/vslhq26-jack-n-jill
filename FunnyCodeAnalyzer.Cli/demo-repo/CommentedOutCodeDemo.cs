namespace DemoRepo;

public static class CommentedOutCodeDemo
{
    public static void Run()
    {
        // Console.WriteLine("This was commented out instead of deleted.");
        // if (true) { return; }
        // var result = CalculateValue();
        // result += 42;
        System.Console.WriteLine("Demo code path");
    }

    private static int CalculateValue()
    {
        return 5;
    }
}