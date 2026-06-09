using SbScene.Core.Rendering;

namespace SbScene.Core.Tests;

public sealed class SbSceneCharacterAnimationDefaultsTests
{
    [Fact]
    public void BuildSelectionsReturnsExpectedFixedCharacterSlots()
    {
        var selections = SbSceneCharacterAnimationDefaults.BuildSelections();

        Assert.Equal(
            ["Change_Fashion", "Change_Position", "Change_Accessory", "Action_Wait1", "Mouth_Wait1"],
            selections.Select(static selection => selection.Name).ToArray());
        Assert.All(selections, selection =>
        {
            Assert.Equal(0, selection.Frame);
            Assert.True(selection.HasExplicitFrame);
        });
    }
}
