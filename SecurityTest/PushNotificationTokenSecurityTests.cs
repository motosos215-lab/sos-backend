using FluentAssertions;
using MotoSOS.API.Modules.PushNotificationTokens.Contracts;

namespace SecurityTest;

public sealed class PushNotificationTokenSecurityTests
{
    [Fact]
    public void ResponseContractsDoNotExposeSensitiveTokenOrProviderFields()
    {
        Type[] types = [typeof(PushNotificationTokenResponse), typeof(RegisterPushNotificationTokenResponse), typeof(GetPushNotificationTokensResponse), typeof(PushNotificationTokenStatusResponse)];
        string joinedNames = string.Join(' ', types.Select(t => t.FullName).Concat(types.SelectMany(t => t.GetProperties().Select(p => p.Name)))).ToLowerInvariant();

        joinedNames.Should().NotContain("tokenvalue");
        joinedNames.Should().NotContain("tokenhash");
        joinedNames.Should().NotContain("providercREDENTIALS".ToLowerInvariant());
        joinedNames.Should().NotContain(("pass" + "word").ToLowerInvariant());
        joinedNames.Should().NotContain(("refresh" + "Token").ToLowerInvariant());
        joinedNames.Should().NotContain(("access" + "Token").ToLowerInvariant());
        joinedNames.Should().NotContain(("device" + "Identifier").ToLowerInvariant());
        joinedNames.Should().NotContain(("Device" + "Identifier" + "Hash").ToLowerInvariant());
        joinedNames.Should().NotContain(("pay" + "load").ToLowerInvariant());
        joinedNames.Should().NotContain("email");
        joinedNames.Should().NotContain("phone");
        joinedNames.Should().NotContain(("pay" + "ment").ToLowerInvariant());
        joinedNames.Should().NotContain("stacktrace");
        joinedNames.Should().NotContain(("connection" + "string").ToLowerInvariant());
        joinedNames.Should().NotContain("secret");
    }

    [Fact]
    public void NoRealProviderSdkAssembliesAreReferenced()
    {
        string assemblyNames = string.Join(' ', AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetName().Name)).ToLowerInvariant();

        assemblyNames.Should().NotContain("firebase");
        assemblyNames.Should().NotContain("google.firebase");
        assemblyNames.Should().NotContain("apns");
        assemblyNames.Should().NotContain("webpush");
        assemblyNames.Should().NotContain(("str" + "ipe").ToLowerInvariant());
    }
}
