using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Contracts;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace IntegrationTest;

public sealed class AuthEndpointsTests
{
    [Fact]
    public async Task RegisterRiderReturnsCreatedWithUserData()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var request = CreateRegisterRequest("rider@example.com", "Rider");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        content.Should().Contain("rider@example.com");
        content.Should().Contain("Rider");
        content.Should().NotContain("PasswordHash");
        content.Should().NotContain("StrongPass1!");
        stores.Users.Users.Where(user => user.Role == UserRole.Rider && user.AcceptedTermsAtUtc.HasValue).Should().ContainSingle();
    }

    [Fact]
    public async Task RegisterMonitorReturnsCreatedWithMonitorRole()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var request = CreateRegisterRequest("monitor@example.com", "Monitor");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        stores.Users.Users.Should().ContainSingle(user => user.Email == "monitor@example.com" && user.Role == UserRole.Monitor);
    }

    [Fact]
    public async Task RegisterConductorMapsToRiderRole()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var request = CreateRegisterRequest("conductor@example.com", "Conductor");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        stores.Users.Users.Should().ContainSingle(user => user.Email == "conductor@example.com" && user.Role == UserRole.Rider);
    }

    [Fact]
    public async Task RegisterWithoutAcceptedTermsReturnsTermsNotAccepted()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var request = CreateRegisterRequest("terms@example.com", "Rider") with { AcceptTerms = false };

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        content.Should().Contain("terms_not_accepted");
    }

    [Fact]
    public async Task RegisterWithDifferentPasswordsReturnsBadRequest()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var request = CreateRegisterRequest("mismatch@example.com", "Rider") with { ConfirmPassword = "Different1!" };

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterDuplicateReturnsConflict()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var request = CreateRegisterRequest("duplicate@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", request);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        content.Should().Contain("user_already_exists");
    }

    [Fact]
    public async Task LoginReturnsAccessAndRefreshTokens()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("login@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(register.Email, register.Password));
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("accessToken");
        content.Should().Contain("refreshToken");
        content.Should().NotContain("PasswordHash");
    }

    [Fact]
    public async Task LoginWithRememberMeExtendsOnlyRefreshTokenExpiration()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("remember@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(register.Email, register.Password, true));
        LoginEnvelope? login = await response.Content.ReadFromJsonAsync<LoginEnvelope>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        login.Should().NotBeNull();
        stores.RefreshTokens.Tokens.Should().ContainSingle();
        stores.RefreshTokens.Tokens[0].ExpiresAtUtc.Should().BeAfter(DateTimeOffset.UtcNow.AddDays(20));
        login!.Data.AccessTokenExpiresAtUtc.Should().BeBefore(DateTimeOffset.UtcNow.AddHours(1));
    }

    [Fact]
    public async Task LoginWithInvalidCredentialsReturnsUnauthorized()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("missing@example.com", "StrongPass1!"));
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        content.Should().Contain("invalid_credentials");
        content.Should().NotContain("missing@example.com");
    }

    [Fact]
    public async Task ForgotPasswordReturnsNoContentForExistingUser()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("forgot-existing@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(register.Email));
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        content.Should().BeEmpty();
        stores.AuthCodes.Codes.Should().ContainSingle(code => code.EmailNormalized == register.Email && code.Purpose == AuthCodePurpose.PasswordReset);
        stores.AuthCodes.Codes.Single().CodeHash.Should().NotBe(stores.Delivery.LastCodeFor(register.Email, AuthCodePurpose.PasswordReset));
    }

    [Fact]
    public async Task ForgotPasswordReturnsNoContentForMissingUser()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest("missing@example.com"));
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        content.Should().BeEmpty();
        stores.AuthCodes.Codes.Should().BeEmpty();
    }

    [Fact]
    public async Task ForgotPasswordWithEmailProviderInvokesEmailSender()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores, useEmailProvider: true);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("forgot-email@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(register.Email));
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        content.Should().BeEmpty();
        stores.EmailSender.Messages.Should().ContainSingle().Which.Subject.Should().Be("MotoSOS - Código para restablecer contraseña");
        stores.AuthCodes.Codes.Single().DeliveryStatus.Should().Be(AuthCodeDeliveryStatus.Delivered);
        content.Should().NotContain(stores.AuthCodes.Codes.Single().CodeHash);
    }

    [Fact]
    public async Task EmailDeliveryFailureKeepsRequestNeutralAndMarksDeliveryFailed()
    {
        var stores = new TestStores { EmailSender = { ShouldFail = true } };
        await using WebApplicationFactory<Program> factory = CreateFactory(stores, useEmailProvider: true);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("forgot-failed@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(register.Email));
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        content.Should().BeEmpty();
        stores.AuthCodes.Codes.Single().DeliveryStatus.Should().Be(AuthCodeDeliveryStatus.Failed);
    }

    [Fact]
    public async Task ResetPasswordChangesPasswordAndRevokesRefreshTokens()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("reset@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(register.Email, register.Password));
        await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(register.Email));
        string code = stores.Delivery.LastCodeFor(register.Email, AuthCodePurpose.PasswordReset);

        HttpResponseMessage reset = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new ResetPasswordRequest(register.Email, code, "NewStrongPass1!"));
        HttpResponseMessage oldLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(register.Email, register.Password));
        HttpResponseMessage newLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(register.Email, "NewStrongPass1!"));

        reset.StatusCode.Should().Be(HttpStatusCode.OK);
        oldLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        newLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        stores.RefreshTokens.Tokens.Where(token => token.RevokedAtUtc is not null).Should().ContainSingle();
        stores.AuthCodes.Codes.Single(authCode => authCode.Purpose == AuthCodePurpose.PasswordReset).Status.Should().Be(AuthCodeStatus.Used);
    }

    [Fact]
    public async Task ResetPasswordRejectsInvalidExpiredAndUsedCodes()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("reset-invalid@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);
        await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(register.Email));

        HttpResponseMessage invalid = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new ResetPasswordRequest(register.Email, "000000", "NewStrongPass1!"));
        stores.AuthCodes.Codes.Single().ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        HttpResponseMessage expired = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new ResetPasswordRequest(register.Email, stores.Delivery.LastCodeFor(register.Email, AuthCodePurpose.PasswordReset), "NewStrongPass1!"));
        stores.AuthCodes.Codes.Single().CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
        await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(register.Email));
        string code = stores.Delivery.LastCodeFor(register.Email, AuthCodePurpose.PasswordReset);
        await client.PostAsJsonAsync("/api/v1/auth/reset-password", new ResetPasswordRequest(register.Email, code, "NewStrongPass1!"));
        HttpResponseMessage reused = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new ResetPasswordRequest(register.Email, code, "AnotherStrong1!"));

        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        expired.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        reused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPasswordFailsAfterMaxAttempts()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("reset-attempts@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);
        await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(register.Email));

        for (int index = 0; index < 5; index++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/reset-password", new ResetPasswordRequest(register.Email, "000000", "NewStrongPass1!"));
        }

        stores.AuthCodes.Codes.Single().Status.Should().Be(AuthCodeStatus.Failed);
    }

    [Fact]
    public async Task RequestAccessCodeReturnsNoContentForExistingUser()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("code-existing@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/request-access-code", new RequestAccessCodeRequest(register.Email));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        stores.AuthCodes.Codes.Should().ContainSingle(code => code.EmailNormalized == register.Email && code.Purpose == AuthCodePurpose.AccessLogin);
    }

    [Fact]
    public async Task RequestAccessCodeReturnsNoContentForMissingUser()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/request-access-code", new RequestAccessCodeRequest("missing-code@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        stores.AuthCodes.Codes.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestAccessCodeWithEmailProviderInvokesEmailSender()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores, useEmailProvider: true);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("access-email@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/request-access-code", new RequestAccessCodeRequest(register.Email));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        stores.EmailSender.Messages.Should().ContainSingle().Which.Subject.Should().Be("MotoSOS - Código de acceso");
        stores.AuthCodes.Codes.Single().DeliveryStatus.Should().Be(AuthCodeDeliveryStatus.Delivered);
    }

    [Fact]
    public async Task LoginWithCodeReturnsTokensWithValidCode()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("code@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);
        await client.PostAsJsonAsync("/api/v1/auth/request-access-code", new RequestAccessCodeRequest(register.Email));
        string code = stores.Delivery.LastCodeFor(register.Email, AuthCodePurpose.AccessLogin);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login-with-code", new LoginWithCodeRequest(register.Email, code));
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("accessToken");
        content.Should().Contain("refreshToken");
        content.Should().NotContain(code);
        stores.AuthCodes.Codes.Single(authCode => authCode.Purpose == AuthCodePurpose.AccessLogin).Status.Should().Be(AuthCodeStatus.Used);
    }

    [Fact]
    public async Task LoginWithCodeRejectsInvalidExpiredInactiveAndReusedCodes()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("code-invalid@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);
        await client.PostAsJsonAsync("/api/v1/auth/request-access-code", new RequestAccessCodeRequest(register.Email));

        HttpResponseMessage invalid = await client.PostAsJsonAsync("/api/v1/auth/login-with-code", new LoginWithCodeRequest(register.Email, "000000"));
        stores.AuthCodes.Codes.Single().ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        HttpResponseMessage expired = await client.PostAsJsonAsync("/api/v1/auth/login-with-code", new LoginWithCodeRequest(register.Email, stores.Delivery.LastCodeFor(register.Email, AuthCodePurpose.AccessLogin)));
        stores.AuthCodes.Codes.Single().CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
        await client.PostAsJsonAsync("/api/v1/auth/request-access-code", new RequestAccessCodeRequest(register.Email));
        string code = stores.Delivery.LastCodeFor(register.Email, AuthCodePurpose.AccessLogin);
        stores.Users.Users.Single(user => user.Email == register.Email).IsActive = false;
        HttpResponseMessage inactive = await client.PostAsJsonAsync("/api/v1/auth/login-with-code", new LoginWithCodeRequest(register.Email, code));
        stores.Users.Users.Single(user => user.Email == register.Email).IsActive = true;
        stores.AuthCodes.Codes.Where(authCode => authCode.Purpose == AuthCodePurpose.AccessLogin).OrderByDescending(authCode => authCode.CreatedAtUtc).First().CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
        await client.PostAsJsonAsync("/api/v1/auth/request-access-code", new RequestAccessCodeRequest(register.Email));
        string reusable = stores.Delivery.LastCodeFor(register.Email, AuthCodePurpose.AccessLogin);
        await client.PostAsJsonAsync("/api/v1/auth/login-with-code", new LoginWithCodeRequest(register.Email, reusable));
        HttpResponseMessage reused = await client.PostAsJsonAsync("/api/v1/auth/login-with-code", new LoginWithCodeRequest(register.Email, reusable));

        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        expired.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        inactive.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        reused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CodesCannotBeUsedForDifferentPurpose()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("purpose@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);
        await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(register.Email));
        string resetCode = stores.Delivery.LastCodeFor(register.Email, AuthCodePurpose.PasswordReset);
        await client.PostAsJsonAsync("/api/v1/auth/request-access-code", new RequestAccessCodeRequest(register.Email));
        string loginCode = stores.Delivery.LastCodeFor(register.Email, AuthCodePurpose.AccessLogin);

        HttpResponseMessage resetAsLogin = await client.PostAsJsonAsync("/api/v1/auth/login-with-code", new LoginWithCodeRequest(register.Email, resetCode));
        HttpResponseMessage loginAsReset = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new ResetPasswordRequest(register.Email, loginCode, "NewStrongPass1!"));

        resetAsLogin.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        loginAsReset.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequestCodeRateLimitReturnsNeutralAndDoesNotGenerateAnotherCode()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("rate@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);

        HttpResponseMessage first = await client.PostAsJsonAsync("/api/v1/auth/request-access-code", new RequestAccessCodeRequest(register.Email));
        HttpResponseMessage second = await client.PostAsJsonAsync("/api/v1/auth/request-access-code", new RequestAccessCodeRequest(register.Email));

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        stores.AuthCodes.Codes.Should().ContainSingle(code => code.Purpose == AuthCodePurpose.AccessLogin);
    }

    [Fact]
    public async Task AuthCodesDisabledKeepsRequestsNeutralAndBlocksCodeConsumption()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores, authCodesEnabled: false);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage forgot = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest("disabled@example.com"));
        HttpResponseMessage request = await client.PostAsJsonAsync("/api/v1/auth/request-access-code", new RequestAccessCodeRequest("disabled@example.com"));
        HttpResponseMessage reset = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new ResetPasswordRequest("disabled@example.com", "123456", "NewStrongPass1!"));
        HttpResponseMessage login = await client.PostAsJsonAsync("/api/v1/auth/login-with-code", new LoginWithCodeRequest("disabled@example.com", "123456"));
        string resetContent = await reset.Content.ReadAsStringAsync();

        forgot.StatusCode.Should().Be(HttpStatusCode.NoContent);
        request.StatusCode.Should().Be(HttpStatusCode.NoContent);
        reset.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        login.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        resetContent.Should().Contain("feature_disabled");
    }

    [Fact]
    public async Task AuthCodesDisabledDoesNotSendEmailWhenEmailProviderIsConfigured()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores, authCodesEnabled: false, useEmailProvider: true);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/request-access-code", new RequestAccessCodeRequest("disabled-email@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        stores.EmailSender.Messages.Should().BeEmpty();
        stores.AuthCodes.Codes.Should().BeEmpty();
    }

    [Fact]
    public async Task CurrentUserReturnsUserWithValidToken()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("me@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);
        HttpResponseMessage loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(register.Email, register.Password));
        LoginEnvelope? login = await loginResponse.Content.ReadFromJsonAsync<LoginEnvelope>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Data.AccessToken);

        HttpResponseMessage response = await client.GetAsync("/api/v1/users/me");
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("me@example.com");
        content.Should().NotContain("PasswordHash");
    }

    [Fact]
    public async Task CurrentUserRequiresAuthentication()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static RegisterRequest CreateRegisterRequest(string email, string accountType)
    {
        return new RegisterRequest(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555 555 5555", accountType, true);
    }

    private static WebApplicationFactory<Program> CreateFactory(TestStores stores, bool authCodesEnabled = true, bool useEmailProvider = false)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "MotoSOS",
                    ["Jwt:Audience"] = "MotoSOS.Clients",
                    ["Jwt:Key"] = new string('I', 48),
                    ["Jwt:AccessTokenMinutes"] = "15",
                    ["Jwt:RefreshTokenDays"] = "7",
                    ["Jwt:RefreshTokenRememberMeDays"] = "30",
                    ["AuthCodes:Enabled"] = authCodesEnabled.ToString(),
                    ["AuthCodes:CodeLength"] = "6",
                    ["AuthCodes:TtlMinutes"] = "10",
                    ["AuthCodes:MaxAttempts"] = "5",
                    ["AuthCodes:RateLimitMinutes"] = "1",
                    ["AuthCodes:Provider"] = useEmailProvider ? "Email" : "Simulated",
                    ["AuthCodes:Email:Enabled"] = "true",
                    ["AuthCodes:Email:FromEmail"] = "noreply@example.com",
                    ["AuthCodes:Email:FromName"] = "MotoSOS",
                    ["AuthCodes:Email:SmtpHost"] = "smtp.example.test",
                    ["AuthCodes:Email:SmtpPort"] = "587",
                    ["AuthCodes:Email:SmtpUsername"] = "smtp-user",
                    ["AuthCodes:Email:SmtpPassword"] = "smtp-secret",
                    ["AuthCodes:Email:UseSsl"] = "true",
                    ["MongoDb:ConnectionString"] = string.Empty,
                    ["MongoDb:DatabaseName"] = "MotoSOS_Test"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IUserRepository>(stores.Users);
                services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens);
                services.AddSingleton<IAuthCodeRepository>(stores.AuthCodes);
                services.AddSingleton<IAuthCodeEmailSender>(stores.EmailSender);
                if (!useEmailProvider)
                {
                    services.AddSingleton<IAuthCodeDeliveryProvider>(stores.Delivery);
                }
            });
        });
    }

    private sealed class TestStores
    {
        public InMemoryUserRepository Users { get; } = new();

        public InMemoryRefreshTokenRepository RefreshTokens { get; } = new();

        public InMemoryAuthCodeRepository AuthCodes { get; } = new();

        public FakeAuthCodeDeliveryProvider Delivery { get; } = new();

        public FakeAuthCodeEmailSender EmailSender { get; } = new();
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        public List<User> Users { get; } = [];

        public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Users.FirstOrDefault(user => user.Id == id));

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(Users.FirstOrDefault(user => string.Equals(user.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
    {
        public List<RefreshToken> Tokens { get; } = [];

        public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(Tokens.FirstOrDefault(token => token.TokenHash == tokenHash));

        public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
        {
            Tokens.Add(refreshToken);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeActiveByUserIdAsync(string userId, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken)
        {
            foreach (RefreshToken token in Tokens.Where(token => token.UserId == userId && token.RevokedAtUtc is null && token.ExpiresAtUtc > revokedAtUtc))
            {
                token.RevokedAtUtc = revokedAtUtc;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryAuthCodeRepository : IAuthCodeRepository
    {
        public List<AuthCode> Codes { get; } = [];

        public Task<AuthCode?> GetLatestByEmailAndPurposeAsync(string emailNormalized, AuthCodePurpose purpose, CancellationToken cancellationToken) =>
            Task.FromResult(Codes.Where(code => code.EmailNormalized == emailNormalized && code.Purpose == purpose).OrderByDescending(code => code.CreatedAtUtc).FirstOrDefault());

        public Task AddAsync(AuthCode authCode, CancellationToken cancellationToken)
        {
            Codes.Add(authCode);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(AuthCode authCode, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeActiveAsync(string emailNormalized, AuthCodePurpose purpose, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken)
        {
            foreach (AuthCode code in Codes.Where(code => code.EmailNormalized == emailNormalized && code.Purpose == purpose && code.Status == AuthCodeStatus.Active))
            {
                code.Status = AuthCodeStatus.Revoked;
                code.RevokedAtUtc = revokedAtUtc;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuthCodeDeliveryProvider : IAuthCodeDeliveryProvider
    {
        private readonly Dictionary<string, string> _codes = [];

        public AuthCodeDeliveryChannel Channel => AuthCodeDeliveryChannel.Simulated;

        public Task<AuthCodeDeliveryStatus> DeliverAsync(string emailNormalized, AuthCodePurpose purpose, string code, CancellationToken cancellationToken)
        {
            _codes[$"{emailNormalized}:{purpose}"] = code;
            return Task.FromResult(AuthCodeDeliveryStatus.Delivered);
        }

        public string LastCodeFor(string email, AuthCodePurpose purpose) => _codes[$"{email.Trim().ToLowerInvariant()}:{purpose}"];
    }

    private sealed class FakeAuthCodeEmailSender : IAuthCodeEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];
        public bool ShouldFail { get; set; }

        public Task SendAsync(string toEmail, string subject, string body, AuthCodeEmailOptions options, CancellationToken cancellationToken)
        {
            if (ShouldFail)
            {
                throw new InvalidOperationException("SMTP failed.");
            }

            Messages.Add(new EmailMessage(toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed record EmailMessage(string ToEmail, string Subject, string Body);

    private sealed record LoginEnvelope(bool Success, LoginData Data);

    private sealed record LoginData(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAtUtc, AuthUserResponse User);
}
