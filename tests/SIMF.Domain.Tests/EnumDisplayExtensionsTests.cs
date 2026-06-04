using System.Globalization;
using SIMF.Common.Enums;
using Xunit;

namespace SIMF.Domain.Tests;

/// <summary>
/// D-110: end-to-end verification that the <see cref="EnumDisplayExtensions.GetDisplayName"/>
/// helper actually pulls the bilingual resx strings — proves the [Display]
/// attribute + ResourceManager wiring + the embedded .resx files are all
/// reachable from a clean assembly load (no Blazor / no HttpContext).
/// </summary>
public sealed class EnumDisplayExtensionsTests
{
    [Fact]
    public void English_culture_returns_english_label()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            Assert.Equal("Approved", AccountState.Approved.GetDisplayName());
            Assert.Equal("Visitor", UserType.Visitor.GetDisplayName());
            Assert.Equal("Account approved", NotificationKind.AccountApproved.GetDisplayName());
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Fact]
    public void Arabic_culture_returns_arabic_label()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("ar");
            Assert.Equal("معتمد", AccountState.Approved.GetDisplayName());
            Assert.Equal("زائر", UserType.Visitor.GetDisplayName());
            Assert.Equal("تم اعتماد الحساب", NotificationKind.AccountApproved.GetDisplayName());
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
}
