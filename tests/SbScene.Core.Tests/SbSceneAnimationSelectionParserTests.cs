using SbScene.Core.Rendering;

namespace SbScene.Core.Tests;

public sealed class SbSceneAnimationSelectionParserTests
{
    [Fact]
    public void ParseExplicitFrameSelections()
    {
        Assert.True(SbSceneAnimationSelectionParser.TryParse("Change_Fashion:0", out var colon));
        Assert.Equal("Change_Fashion", colon.Name);
        Assert.Equal(0, colon.Frame);
        Assert.True(colon.HasExplicitFrame);

        Assert.True(SbSceneAnimationSelectionParser.TryParse("Action_Joy3[12.5]", out var bracketed));
        Assert.Equal("Action_Joy3", bracketed.Name);
        Assert.Equal(12.5, bracketed.Frame);
        Assert.True(bracketed.HasExplicitFrame);
    }

    [Fact]
    public void ParseImplicitFrameSelectionForPngCompatibility()
    {
        Assert.True(SbSceneAnimationSelectionParser.TryParse("Action_Joy3", out var selection));

        Assert.Equal("Action_Joy3", selection.Name);
        Assert.Equal(0, selection.Frame);
        Assert.False(selection.HasExplicitFrame);
    }

    [Fact]
    public void ParseSlotIndexSelection()
    {
        Assert.True(SbSceneAnimationSelectionParser.TryParse("#2[4]", out var selection));

        Assert.Equal("#2", selection.Name);
        Assert.Equal(2, selection.Index);
        Assert.Equal(4, selection.Frame);
        Assert.True(selection.HasExplicitFrame);
    }
}
