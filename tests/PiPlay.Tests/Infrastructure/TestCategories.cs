namespace PiPlay.Tests;

/// <summary>xUnit trait names so lanes can be filtered: <c>dotnet test --filter Category=Markup</c>.</summary>
public static class TestCategories
{
    public const string Key = "Category";
    public const string Markup = "Markup"; // Layer 1 — XAML parsed as XML, no WPF runtime
    public const string Logic = "Logic";   // Layer 2 — pure services
    public const string Wpf = "Wpf";       // Layer 3 — live WPF on STA
}
