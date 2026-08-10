using FluentAssertions;
using MotoSOS.API.Modules.PushNotificationTokens.Application;

namespace UnitTest.PushNotificationTokens;

public sealed class PushNotificationTokenPreviewerTests
{
    [Fact]
    public void PreviewMasksTokenAndHandlesShortTokensSafely()
    {
        var previewer = new PushNotificationTokenPreviewer();
        string token = "abcdef1234567890wxyz";

        string preview = previewer.CreatePreview(token);

        preview.Should().Be("abcdef****wxyz");
        preview.Should().NotBe(token);
        previewer.CreatePreview("short").Should().Be("****");
    }
}
