// Whether an UNSCANNED upload is stored or refused. The decision used to ride on
// the UploadFileCommand, and every call site outside the generic file endpoint
// hard-coded FailClosed:false - the avatar, the ID document, the VIP photo, the
// media-gallery image, the speaker presentation, the Media Library asset. A
// clamd restart, a scan timeout or UploadScanning:Enabled=false therefore meant
// those files were written unscanned in Production while
// UploadScanning:FailClosed said the opposite. The centralized service now
// derives it, so no caller can opt out.
using SIMF.Infrastructure.Files;
using Xunit;

namespace SIMF.Api.Tests.Files;

[Trait(TestAreas.TraitName, TestAreas.Files)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class UploadScanFailClosedTests
{
    [Fact]
    public void Production_fails_closed_even_when_the_caller_asked_not_to()
    {
        Assert.True(StoredFileService.ResolveFailClosed(
            requestedByCaller: false, isProduction: true, optionFailClosed: true));
    }

    [Fact]
    public void Production_honours_an_explicit_fail_open_setting()
    {
        // The option is the operator's deliberate choice, and it is the only way
        // to turn this off.
        Assert.False(StoredFileService.ResolveFailClosed(
            requestedByCaller: false, isProduction: true, optionFailClosed: false));
    }

    [Fact]
    public void Outside_production_an_unscanned_upload_still_passes()
    {
        // Dev and test hosts run without a scanner; failing closed there would
        // break every seeded upload.
        Assert.False(StoredFileService.ResolveFailClosed(
            requestedByCaller: false, isProduction: false, optionFailClosed: true));
    }

    [Fact]
    public void A_caller_can_force_it_on_anywhere()
    {
        Assert.True(StoredFileService.ResolveFailClosed(
            requestedByCaller: true, isProduction: false, optionFailClosed: false));
    }
}
