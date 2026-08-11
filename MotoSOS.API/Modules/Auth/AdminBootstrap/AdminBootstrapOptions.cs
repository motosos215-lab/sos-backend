namespace MotoSOS.API.Modules.Auth.AdminBootstrap;

public sealed class AdminBootstrapOptions
{
    public const string SectionName = "AdminBootstrap";

    public bool Enabled { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? FullName { get; set; }
    public bool RunOnlyWhenNoAdminsExist { get; set; } = true;
}
