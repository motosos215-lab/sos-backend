using FluentAssertions;
using Microsoft.Extensions.Options;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Domain;

namespace UnitTest.Auth;

public sealed class AuthCodeEmailProviderTests
{
    [Fact]
    public async Task ResolverUsesSimulatedProviderWhenConfigured()
    {
        var sender = new Sender();
        var logger = new Logger<AuthCodeDeliveryProviderResolver>();
        var resolver = CreateResolver(new AuthCodeOptions { Provider = "Simulated" }, sender, logger);

        AuthCodeDeliveryStatus status = await resolver.DeliverAsync("rider@example.com", AuthCodePurpose.AccessLogin, "123456", CancellationToken.None);

        status.Should().Be(AuthCodeDeliveryStatus.Delivered);
        resolver.Channel.Should().Be(AuthCodeDeliveryChannel.Simulated);
        sender.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolverUsesEmailProviderWhenConfigured()
    {
        var sender = new Sender();
        var resolver = CreateResolver(ValidEmailOptions(), sender);

        AuthCodeDeliveryStatus status = await resolver.DeliverAsync("rider@example.com", AuthCodePurpose.AccessLogin, "123456", CancellationToken.None);

        status.Should().Be(AuthCodeDeliveryStatus.Delivered);
        resolver.Channel.Should().Be(AuthCodeDeliveryChannel.Email);
        sender.Messages.Should().ContainSingle();
    }

    [Theory]
    [InlineData("host")]
    [InlineData("from")]
    [InlineData("username")]
    [InlineData("password")]
    public async Task ResolverReturnsFailedForIncompleteEmailConfiguration(string missing)
    {
        AuthCodeOptions options = ValidEmailOptions();
        if (missing == "host") options.Email.SmtpHost = string.Empty;
        if (missing == "from") options.Email.FromEmail = string.Empty;
        if (missing == "username") options.Email.SmtpUsername = string.Empty;
        if (missing == "password") options.Email.SmtpPassword = string.Empty;
        var sender = new Sender();

        AuthCodeDeliveryStatus status = await CreateResolver(options, sender).DeliverAsync("rider@example.com", AuthCodePurpose.PasswordReset, "123456", CancellationToken.None);

        status.Should().Be(AuthCodeDeliveryStatus.Failed);
        sender.Messages.Should().BeEmpty();
    }

    [Theory]
    [InlineData(AuthCodePurpose.PasswordReset, "MotoSOS - Código para restablecer contraseña")]
    [InlineData(AuthCodePurpose.AccessLogin, "MotoSOS - Código de acceso")]
    public async Task EmailProviderUsesSubjectForPurpose(AuthCodePurpose purpose, string expectedSubject)
    {
        var sender = new Sender();
        var provider = new EmailAuthCodeDeliveryProvider(sender, Options.Create(ValidEmailOptions()), new Logger<EmailAuthCodeDeliveryProvider>());

        AuthCodeDeliveryStatus status = await provider.DeliverAsync("rider@example.com", purpose, "123456", CancellationToken.None);

        status.Should().Be(AuthCodeDeliveryStatus.Delivered);
        sender.Messages.Should().ContainSingle().Which.Subject.Should().Be(expectedSubject);
        sender.Messages.Single().Body.Should().Contain("123456");
    }

    [Fact]
    public async Task EmailProviderReturnsFailedWhenSenderFails()
    {
        var sender = new Sender { ShouldFail = true };
        var provider = new EmailAuthCodeDeliveryProvider(sender, Options.Create(ValidEmailOptions()), new Logger<EmailAuthCodeDeliveryProvider>());

        AuthCodeDeliveryStatus status = await provider.DeliverAsync("rider@example.com", AuthCodePurpose.AccessLogin, "123456", CancellationToken.None);

        status.Should().Be(AuthCodeDeliveryStatus.Failed);
    }

    [Fact]
    public async Task ProvidersDoNotLogCodeEmailOrSmtpSettings()
    {
        var sender = new Sender { ShouldFail = true };
        var emailLogger = new Logger<EmailAuthCodeDeliveryProvider>();
        var simulatedLogger = new Logger<SimulatedAuthCodeDeliveryProvider>();
        var email = new EmailAuthCodeDeliveryProvider(sender, Options.Create(ValidEmailOptions()), emailLogger);
        var simulated = new SimulatedAuthCodeDeliveryProvider(simulatedLogger);

        await email.DeliverAsync("rider@example.com", AuthCodePurpose.AccessLogin, "123456", CancellationToken.None);
        await simulated.DeliverAsync("rider@example.com", AuthCodePurpose.PasswordReset, "123456", CancellationToken.None);

        string logs = string.Join('\n', emailLogger.Messages.Concat(simulatedLogger.Messages));
        logs.Should().NotContain("123456");
        logs.Should().NotContain("rider@example.com");
        logs.Should().NotContain("AccessLogin");
        logs.Should().NotContain("PasswordReset");
        logs.Should().NotContain("smtp.example.test");
        logs.Should().NotContain("smtp-user");
        logs.Should().NotContain("smtp-secret");
    }

    private static AuthCodeDeliveryProviderResolver CreateResolver(AuthCodeOptions options, Sender sender, Logger<AuthCodeDeliveryProviderResolver>? logger = null)
    {
        return new AuthCodeDeliveryProviderResolver(
            new SimulatedAuthCodeDeliveryProvider(new Logger<SimulatedAuthCodeDeliveryProvider>()),
            new EmailAuthCodeDeliveryProvider(sender, Options.Create(options), new Logger<EmailAuthCodeDeliveryProvider>()),
            Options.Create(options),
            new AuthCodeEmailOptionsValidator(),
            logger ?? new Logger<AuthCodeDeliveryProviderResolver>());
    }

    private static AuthCodeOptions ValidEmailOptions() => new()
    {
        Provider = "Email",
        TtlMinutes = 10,
        Email = new AuthCodeEmailOptions
        {
            Enabled = true,
            FromEmail = "noreply@example.com",
            FromName = "MotoSOS",
            SmtpHost = "smtp.example.test",
            SmtpPort = 587,
            SmtpUsername = "smtp-user",
            SmtpPassword = "smtp-secret",
            UseSsl = true
        }
    };

    private sealed class Sender : IAuthCodeEmailSender
    {
        public List<Message> Messages { get; } = [];
        public bool ShouldFail { get; set; }

        public Task SendAsync(string toEmail, string subject, string body, AuthCodeEmailOptions options, CancellationToken cancellationToken)
        {
            if (ShouldFail)
            {
                throw new InvalidOperationException("SMTP failed.");
            }

            Messages.Add(new Message(toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed record Message(string ToEmail, string Subject, string Body);

    private sealed class Logger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
