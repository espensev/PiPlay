using System.Xml.Linq;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Markup)]
public class ContextMenuStyleTests
{
    private static XElement Style(string key) =>
        XamlTestFiles.Load("Theme/ControlStyles.xaml")
            .Descendants(XamlTestFiles.Pres + "Style")
            .Single(e => e.Attribute(XamlTestFiles.X + "Key")?.Value == key);

    private static string? SetterValue(XElement style, string property) =>
        style.Elements(XamlTestFiles.Pres + "Setter")
            .Single(e => e.Attribute("Property")?.Value == property)
            .Attribute("Value")?.Value;

    [Fact]
    public void Dark_context_menu_uses_the_dark_surface_and_real_menu_items()
    {
        var style = Style("DarkContextMenu");

        Assert.Equal("ContextMenu", style.Attribute("TargetType")?.Value);
        Assert.Equal("{DynamicResource SurfaceBase}", SetterValue(style, "Background"));
        Assert.Equal("{DynamicResource BorderSubtle}", SetterValue(style, "BorderBrush"));
        Assert.Equal("{DynamicResource BorderThicknessDefault}", SetterValue(style, "BorderThickness"));
        Assert.Equal("{StaticResource DarkMenuItem}", SetterValue(style, "ItemContainerStyle"));
        Assert.Equal("False", SetterValue(style, "HasDropShadow"));

        var template = style.Descendants(XamlTestFiles.Pres + "ControlTemplate").Single();
        Assert.Equal("ContextMenu", template.Attribute("TargetType")?.Value);
        Assert.Equal("{DynamicResource RadiusPopup}",
            template.Descendants(XamlTestFiles.Pres + "Border").Single().Attribute("CornerRadius")?.Value);
        Assert.Equal("Cycle", template.Descendants(XamlTestFiles.Pres + "ItemsPresenter").Single()
            .Attribute("KeyboardNavigation.DirectionalNavigation")?.Value);
    }

    [Fact]
    public void Dark_menu_item_preserves_access_keys_hover_and_disabled_feedback()
    {
        var style = Style("DarkMenuItem");

        Assert.Equal("MenuItem", style.Attribute("TargetType")?.Value);
        Assert.Equal("{DynamicResource TextPrimary}", SetterValue(style, "Foreground"));
        Assert.Equal("{DynamicResource DensityMenuItemPadding}", SetterValue(style, "Padding"));

        var template = style.Descendants(XamlTestFiles.Pres + "ControlTemplate").Single();
        Assert.Contains(template.Descendants(XamlTestFiles.Pres + "ContentPresenter"),
            e => e.Attribute("ContentSource")?.Value == "Header"
                 && e.Attribute("RecognizesAccessKey")?.Value == "True");

        var triggers = template.Descendants(XamlTestFiles.Pres + "Trigger").ToList();
        Assert.Contains(triggers,
            e => e.Attribute("Property")?.Value == "IsHighlighted" && e.Attribute("Value")?.Value == "True");
        Assert.Contains(triggers,
            e => e.Attribute("Property")?.Value == "IsKeyboardFocusWithin" && e.Attribute("Value")?.Value == "True");
        Assert.Contains(triggers,
            e => e.Attribute("Property")?.Value == "IsEnabled" && e.Attribute("Value")?.Value == "False");
    }
}
