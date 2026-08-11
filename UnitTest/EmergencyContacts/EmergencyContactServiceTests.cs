using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Contracts;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.EmergencyContacts;

public sealed class EmergencyContactServiceTests
{
    [Fact]
    public async Task RiderCanCreateDraftAndPending()
    {
        var user = CreateUser(UserRole.Rider);
        var contacts = new InMemoryEmergencyContactRepository();
        var service = CreateService(user, contacts);

        CreateEmergencyContactResponse draft = await service.CreateMyContactAsync(user.Id, DraftRequest(), CancellationToken.None);
        contacts.Contacts.Clear();
        CreateEmergencyContactResponse pending = await service.CreateMyContactAsync(user.Id, ValidContinueRequest(), CancellationToken.None);

        draft.Contact.InvitationStatus.Should().Be("Draft");
        pending.Contact.InvitationStatus.Should().Be("Pending");
    }

    [Theory]
    [InlineData(UserRole.Monitor)]
    [InlineData(UserRole.Admin)]
    public async Task NonRiderCannotCreate(UserRole role)
    {
        var user = CreateUser(role);
        var service = CreateService(user, new InMemoryEmergencyContactRepository());

        Func<Task> act = () => service.CreateMyContactAsync(user.Id, DraftRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAppException>();
    }

    [Fact]
    public async Task BasicPlanDoesNotAllowSecondActiveContact()
    {
        var user = CreateUser(UserRole.Rider);
        var service = CreateService(user, new InMemoryEmergencyContactRepository(new EmergencyContact { UserId = user.Id, IsActive = true }));

        Func<Task> act = () => service.CreateMyContactAsync(user.Id, ValidContinueRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<PlanLimitExceededAppException>();
    }

    [Fact]
    public async Task ListReturnsOnlyUserActiveContacts()
    {
        var user = CreateUser(UserRole.Rider);
        var other = CreateUser(UserRole.Rider);
        var own = new EmergencyContact { UserId = user.Id, IsActive = true };
        var service = CreateService(user, new InMemoryEmergencyContactRepository(own, new EmergencyContact { UserId = user.Id, IsActive = false }, new EmergencyContact { UserId = other.Id, IsActive = true }));

        GetEmergencyContactsResponse response = await service.GetMyContactsAsync(user.Id, CancellationToken.None);

        response.Contacts.Should().ContainSingle(contact => contact.Id == own.Id);
    }

    [Fact]
    public async Task CannotUpdateOrInviteOtherUsersContact()
    {
        var user = CreateUser(UserRole.Rider);
        var other = CreateUser(UserRole.Rider);
        var contact = CompleteContact(other.Id);
        var service = CreateService(user, new InMemoryEmergencyContactRepository(contact));

        Func<Task> update = () => service.UpdateMyContactAsync(user.Id, contact.Id, ValidUpdateRequest(), CancellationToken.None);
        Func<Task> invite = () => service.InviteMyContactAsync(user.Id, contact.Id, CancellationToken.None);

        await update.Should().ThrowAsync<NotFoundAppException>();
        await invite.Should().ThrowAsync<NotFoundAppException>();
    }

    [Fact]
    public async Task DeleteRevokesLogically()
    {
        var user = CreateUser(UserRole.Rider);
        var contact = CompleteContact(user.Id);
        var service = CreateService(user, new InMemoryEmergencyContactRepository(contact));

        await service.DeleteMyContactAsync(user.Id, contact.Id, CancellationToken.None);

        contact.IsActive.Should().BeFalse();
        contact.InvitationStatus.Should().Be(EmergencyContactInvitationStatus.Revoked);
        contact.RevokedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task InviteGeneratesCodeAndRegeneratesDifferentCode()
    {
        var user = CreateUser(UserRole.Rider);
        var contact = CompleteContact(user.Id);
        var service = CreateService(user, new InMemoryEmergencyContactRepository(contact), new SequenceCodeGenerator("8X7Q-3M2K-9L6R", "9AAA-8BBB-7CCC"));

        InviteEmergencyContactResponse first = await service.InviteMyContactAsync(user.Id, contact.Id, CancellationToken.None);
        InviteEmergencyContactResponse second = await service.InviteMyContactAsync(user.Id, contact.Id, CancellationToken.None);

        first.Contact.InvitationStatus.Should().Be("Invited");
        first.Contact.LinkingCode.Should().NotBe(second.Contact.LinkingCode);
        contact.LinkingCodeExpiresAtUtc.Should().Be(new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task InviteIncompleteContactReturnsValidationError()
    {
        var user = CreateUser(UserRole.Rider);
        var contact = new EmergencyContact { UserId = user.Id, IsActive = true };
        var service = CreateService(user, new InMemoryEmergencyContactRepository(contact));

        Func<Task> act = () => service.InviteMyContactAsync(user.Id, contact.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationAppException>();
    }

    [Fact]
    public async Task MonitorCanAcceptInvitationByEmailCaseInsensitive()
    {
        User monitor = CreateUser(UserRole.Monitor); monitor.Email = "MARIA@EXAMPLE.COM";
        EmergencyContact contact = InvitedContact("rider", email: "maria@example.com", phoneNumber: "+52 555 555 5555");
        var service = CreateService(monitor, new InMemoryEmergencyContactRepository(contact));

        AcceptEmergencyContactInvitationResponse response = await service.AcceptInvitationAsync(monitor.Id, contact.LinkingCode!, CancellationToken.None);

        response.Contact.InvitationStatus.Should().Be("Linked");
        response.Contact.LinkedUserId.Should().Be(monitor.Id);
        contact.LinkedAtUtc.Should().Be(new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData("+52 555 555 5555")]
    [InlineData("+52-555-555-5555")]
    [InlineData("(+52) 555 555 5555")]
    [InlineData("+525555555555")]
    public async Task MonitorCanAcceptInvitationByNormalizedPhoneNumber(string contactPhoneNumber)
    {
        User monitor = CreateUser(UserRole.Monitor); monitor.Email = "other@example.com"; monitor.PhoneNumber = "+52.555.555.5555";
        EmergencyContact contact = InvitedContact("rider", email: "maria@example.com", phoneNumber: contactPhoneNumber);
        var service = CreateService(monitor, new InMemoryEmergencyContactRepository(contact));

        AcceptEmergencyContactInvitationResponse response = await service.AcceptInvitationAsync(monitor.Id, contact.LinkingCode!, CancellationToken.None);

        response.Contact.InvitationStatus.Should().Be("Linked");
        response.Contact.LinkedUserId.Should().Be(monitor.Id);
    }

    [Theory]
    [InlineData(UserRole.Rider)]
    [InlineData(UserRole.Admin)]
    public async Task NonMonitorCannotAcceptInvitation(UserRole role)
    {
        User user = CreateUser(role);
        var service = CreateService(user, new InMemoryEmergencyContactRepository(InvitedContact("rider")));

        Func<Task> act = () => service.AcceptInvitationAsync(user.Id, "8X7Q-3M2K-9L6R", CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAppException>();
    }

    [Fact]
    public async Task MissingExpiredNotInvitedAndMismatchedInvitationFailControlled()
    {
        User monitor = CreateUser(UserRole.Monitor); monitor.Email = "monitor@example.com"; monitor.PhoneNumber = "+525555555555";
        EmergencyContact expired = InvitedContact("rider", code: "EXPIRED", expiresAtUtc: new DateTimeOffset(2026, 8, 4, 11, 59, 0, TimeSpan.Zero));
        EmergencyContact pending = InvitedContact("rider", code: "PENDING"); pending.InvitationStatus = EmergencyContactInvitationStatus.Pending;
        EmergencyContact mismatch = InvitedContact("rider", code: "MISMATCH", email: "other@example.com", phoneNumber: "+52 111 111 1111");
        var service = CreateService(monitor, new InMemoryEmergencyContactRepository(expired, pending, mismatch));

        await service.Invoking(s => s.AcceptInvitationAsync(monitor.Id, "missing", CancellationToken.None)).Should().ThrowAsync<InvitationNotFoundAppException>();
        await service.Invoking(s => s.AcceptInvitationAsync(monitor.Id, expired.LinkingCode!, CancellationToken.None)).Should().ThrowAsync<InvitationExpiredAppException>();
        await service.Invoking(s => s.AcceptInvitationAsync(monitor.Id, pending.LinkingCode!, CancellationToken.None)).Should().ThrowAsync<InvitationNotInvitedAppException>();
        await service.Invoking(s => s.AcceptInvitationAsync(monitor.Id, mismatch.LinkingCode!, CancellationToken.None)).Should().ThrowAsync<InvitationLinkNotAllowedAppException>();
    }

    [Fact]
    public async Task AlreadyLinkedInvitationIsIdempotentForSameMonitorAndRejectedForAnotherMonitor()
    {
        User monitor = CreateUser(UserRole.Monitor);
        User otherMonitor = CreateUser(UserRole.Monitor);
        EmergencyContact contact = InvitedContact("rider"); contact.InvitationStatus = EmergencyContactInvitationStatus.Linked; contact.LinkedUserId = monitor.Id; contact.LinkedAtUtc = new DateTimeOffset(2026, 8, 4, 11, 0, 0, TimeSpan.Zero);
        var service = CreateService(monitor, new InMemoryEmergencyContactRepository(contact));
        var otherService = CreateService(otherMonitor, new InMemoryEmergencyContactRepository(contact));

        AcceptEmergencyContactInvitationResponse response = await service.AcceptInvitationAsync(monitor.Id, contact.LinkingCode!, CancellationToken.None);
        Func<Task> otherAct = () => otherService.AcceptInvitationAsync(otherMonitor.Id, contact.LinkingCode!, CancellationToken.None);

        response.Contact.InvitationStatus.Should().Be("Linked");
        response.Contact.LinkedUserId.Should().Be(monitor.Id);
        await otherAct.Should().ThrowAsync<InvitationAlreadyLinkedAppException>();
    }

    private static EmergencyContactService CreateService(User user, InMemoryEmergencyContactRepository contacts, ILinkingCodeGenerator? codeGenerator = null) =>
        new(new InMemoryUserRepository(user), contacts, codeGenerator ?? new SequenceCodeGenerator("8X7Q-3M2K-9L6R"), new TestClock());

    private static User CreateUser(UserRole role) => new() { Email = $"{role}@example.com", FullName = "Moto Rider", Role = role, IsActive = true };
    private static CreateEmergencyContactRequest DraftRequest() => new("Maria", null, null, null, null, null, "Draft");
    private static CreateEmergencyContactRequest ValidContinueRequest() => new("Maria Lopez", "Esposa", "+52 5512345678", "maria@example.com", 1, new EmergencyContactPermissionsRequest(true, true, false, false), "Continue");
    private static UpdateEmergencyContactRequest ValidUpdateRequest() => new("Maria Lopez", "Esposa", "+52 5512345678", "maria@example.com", 1, new EmergencyContactPermissionsRequest(true, true, false, false), "Continue");
    private static EmergencyContact CompleteContact(string userId) => new() { UserId = userId, IsActive = true, FullName = "Maria", Relationship = "Esposa", PhoneNumber = "+52 5512345678", Email = "maria@example.com", Priority = 1 };
    private static EmergencyContact InvitedContact(string userId, string code = "8X7Q-3M2K-9L6R", string email = "Monitor@example.com", string phoneNumber = "+52 555 555 5555", DateTimeOffset? expiresAtUtc = null) => new() { UserId = userId, IsActive = true, FullName = "Maria", Relationship = "Esposa", PhoneNumber = phoneNumber, Email = email, Priority = 1, InvitationStatus = EmergencyContactInvitationStatus.Invited, LinkingCode = code, LinkingCodeExpiresAtUtc = expiresAtUtc ?? new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero), InvitedAtUtc = new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero) };

    private sealed class TestClock : IClock { public DateTimeOffset UtcNow { get; } = new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero); }
    private sealed class SequenceCodeGenerator : ILinkingCodeGenerator
    {
        private readonly Queue<string> _codes;
        public SequenceCodeGenerator(params string[] codes) { _codes = new Queue<string>(codes); }
        public string CreateCode() => _codes.Count > 0 ? _codes.Dequeue() : "ZZZZ-YYYY-XXXX";
    }
    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly IReadOnlyList<User> _users;
        public InMemoryUserRepository(params User[] users) { _users = users; }
        public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(_users.FirstOrDefault(user => user.Id == id));
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
        public Task AddAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class InMemoryEmergencyContactRepository : IEmergencyContactRepository
    {
        public List<EmergencyContact> Contacts { get; }
        public InMemoryEmergencyContactRepository(params EmergencyContact[] contacts) { Contacts = contacts.ToList(); }
        public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EmergencyContact>>(Contacts.Where(contact => contact.UserId == userId && contact.IsActive).ToArray());
        public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Contacts.FirstOrDefault(contact => contact.Id == id));
        public Task<EmergencyContact?> GetByLinkingCodeAsync(string linkingCode, CancellationToken cancellationToken) => Task.FromResult(Contacts.FirstOrDefault(contact => contact.LinkingCode == linkingCode));
        public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Contacts.Count(contact => contact.UserId == userId && contact.IsActive));
        public Task AddAsync(EmergencyContact contact, CancellationToken cancellationToken) { Contacts.Add(contact); return Task.CompletedTask; }
        public Task UpdateAsync(EmergencyContact contact, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
