using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class PrivacyServiceTests
{
    private static bool Has(string s, string sub) =>
        s.Contains(sub, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Reset_and_clear_wording_distinguish_login_impact_and_title_intent()
    {
        foreach (var s in new[]
                 {
                     PrivacyService.ResetDescription,
                     PrivacyService.ResetConfirmBody,
                     PrivacyService.ResetDoneBody,
                 })
        {
            Assert.True(Has(s, "signed in"), $"Reset wording should reassure login is kept: '{s}'");
            Assert.False(Has(s, "sign out") || Has(s, "signed out"),
                $"Reset wording must NOT imply logout: '{s}'");
        }

        foreach (var s in new[]
                 {
                     PrivacyService.ClearDescription,
                     PrivacyService.ClearConfirmBody,
                     PrivacyService.ClearDoneBody,
                 })
        {
            // Assert the PHRASE, not "sign" and "out" as independent tokens (which a reassuring
            // "stay signed in ... log out of other apps" string could satisfy).
            Assert.True(Has(s, "signed out") || Has(s, "signs you out") || Has(s, "sign you out"),
                $"Clear wording should state the user is signed out (as a phrase): '{s}'");
        }

        // Result/status notices must read as outcomes, not the confirmation question, so a user
        // on a privacy action is never left unsure whether their data was actually cleared.
        foreach (var title in new[] { PrivacyService.ClearResultTitle, PrivacyService.ClearDoneTitle })
        {
            Assert.False(title.TrimEnd().EndsWith("?"),
                $"Clear result/status title should be a statement, not a question: '{title}'");
        }

        // The confirmation prompt, by contrast, is allowed (and expected) to be a question.
        Assert.EndsWith("?", PrivacyService.ClearConfirmTitle);
    }
}
