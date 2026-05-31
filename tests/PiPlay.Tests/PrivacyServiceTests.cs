using Microsoft.Web.WebView2.Core;
using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class PrivacyServiceTests
{
    private static bool Has(string s, string sub) =>
        s.Contains(sub, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Reset_wording_says_the_user_stays_signed_in()
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
    }

    [Fact]
    public void Clear_wording_says_the_user_is_signed_out()
    {
        foreach (var s in new[]
                 {
                     PrivacyService.ClearDescription,
                     PrivacyService.ClearConfirmBody,
                     PrivacyService.ClearDoneBody,
                 })
        {
            Assert.True(Has(s, "sign") && Has(s, "out"),
                $"Clear wording should state the user is signed out: '{s}'");
        }
    }

    [Fact]
    public void Reset_and_clear_are_worded_distinctly()
    {
        // REQ-PRIVACY-02: the two actions must be worded separately.
        Assert.NotEqual(PrivacyService.ResetActionLabel, PrivacyService.ClearActionLabel);
        Assert.NotEqual(PrivacyService.ResetDescription, PrivacyService.ClearDescription);
        Assert.NotEqual(PrivacyService.ResetConfirmBody, PrivacyService.ClearConfirmBody);
    }

    [Fact]
    public void Clear_uses_the_all_profile_browsing_data_kind()
    {
        // AllProfile clears cookies + cache + site storage, which logs the user out.
        Assert.Equal(CoreWebView2BrowsingDataKinds.AllProfile, PrivacyService.ClearKinds);
    }
}
