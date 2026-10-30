using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.FluxIsland.Tests;

public sealed class CopiedTextClassifierTests
{
    [Theory]
    [InlineData("https://github.com/JapnoorHere/fluxkit", SmartText.Link, "github.com")]
    [InlineData("www.example.org", SmartText.Link, "example.org")]
    [InlineData("someone@example.com", SmartText.Email, "someone@example.com")]
    [InlineData("+91 98765 43210", SmartText.Phone, "+91 98765 43210")]
    [InlineData("(555) 123-4567", SmartText.Phone, "(555) 123-4567")]
    [InlineData("#3a7bff", SmartText.Color, "#3A7BFF")]
    [InlineData("rgb(58, 123, 255)", SmartText.Color, "rgb(58, 123, 255)")]
    public void Recognises_single_values(string text, SmartText kind, string display)
    {
        (SmartText found, string shown, _, _) = CopiedTextClassifier.Classify(text);

        Assert.Equal(kind, found);
        Assert.Equal(display, shown);
    }

    [Theory]
    [InlineData("hello world")]
    [InlineData("1234567")]
    [InlineData("2026")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/notepad.exe")]
    [InlineData("see https://example.com for more")]
    [InlineData("line one\nhttps://example.com")]
    [InlineData("rgb(300, 0, 0)")]
    public void Anything_else_is_plain_text_with_nothing_to_open(string text)
    {
        (SmartText kind, string shown, string? target, _) = CopiedTextClassifier.Classify(text);

        Assert.Equal(SmartText.Plain, kind);
        Assert.Empty(shown);
        Assert.Null(target);
    }

    [Fact]
    public void A_colour_carries_its_rgb_value()
    {
        Assert.Equal(0x3A7BFF, CopiedTextClassifier.Classify("#3A7BFF").Rgb);
        Assert.Equal(0xFFAA00, CopiedTextClassifier.Classify("#fa0").Rgb);
    }

    [Fact]
    public void An_email_opens_as_a_mailto_link()
    {
        Assert.Equal("mailto:a@b.co", CopiedTextClassifier.Classify("a@b.co").Target);
    }
}
