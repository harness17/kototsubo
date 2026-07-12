using Dev.CommonLibrary.Entity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Site.Common;
using Site.Services;
using Xunit;

namespace Tests;

public class StartupSeederTests
{
    [Fact]
    public async Task SeedAsync_WhenBootstrapIsDisabled_DoesNotCreateUser()
    {
        var roleManager = CreateRoleManagerWithExistingRoles();
        var userManager = CreateUserManager();

        await StartupSeeder.SeedAsync(
            roleManager.Object,
            userManager.Object,
            BuildConfiguration());

        userManager.Verify(manager => manager.FindByEmailAsync(It.IsAny<string>()), Times.Never);
        userManager.Verify(
            manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task SeedAsync_WhenRequiredSettingIsMissing_ThrowsWithoutIncludingPassword()
    {
        var roleManager = CreateRoleManagerWithExistingRoles();
        var userManager = CreateUserManager();
        const string password = "Secret-Should-Not-Appear1!";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            StartupSeeder.SeedAsync(
                roleManager.Object,
                userManager.Object,
                BuildConfiguration(
                    ("BootstrapAdmin:Enabled", "true"),
                    ("BootstrapAdmin:Email", "admin@example.test"),
                    ("BootstrapAdmin:Password", password))));

        Assert.Contains("BootstrapAdmin:UserName", exception.Message);
        Assert.DoesNotContain(password, exception.Message);
    }

    [Fact]
    public async Task SeedAsync_WhenUserDoesNotExist_CreatesAdminFromConfiguration()
    {
        var roleManager = CreateRoleManagerWithExistingRoles();
        var userManager = CreateUserManager();
        userManager.Setup(manager => manager.FindByEmailAsync("admin@example.test"))
            .ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(manager => manager.CreateAsync(
                It.Is<ApplicationUser>(user =>
                    user.Email == "admin@example.test" && user.UserName == "azure-admin"),
                "ValidPassword1!"))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.IsInRoleAsync(
                It.IsAny<ApplicationUser>(),
                ApplicationRoleType.Admin.ToString()))
            .ReturnsAsync(false);
        userManager.Setup(manager => manager.AddToRoleAsync(
                It.IsAny<ApplicationUser>(),
                ApplicationRoleType.Admin.ToString()))
            .ReturnsAsync(IdentityResult.Success);

        await StartupSeeder.SeedAsync(
            roleManager.Object,
            userManager.Object,
            BuildEnabledConfiguration());

        userManager.Verify(manager => manager.CreateAsync(
            It.IsAny<ApplicationUser>(), "ValidPassword1!"), Times.Once);
        userManager.Verify(manager => manager.AddToRoleAsync(
            It.IsAny<ApplicationUser>(), ApplicationRoleType.Admin.ToString()), Times.Once);
    }

    [Fact]
    public async Task SeedAsync_WhenUserAlreadyExists_DoesNotRecreateOrResetPassword()
    {
        var roleManager = CreateRoleManagerWithExistingRoles();
        var userManager = CreateUserManager();
        var existingUser = new ApplicationUser
        {
            Email = "admin@example.test",
            UserName = "azure-admin"
        };
        userManager.Setup(manager => manager.FindByEmailAsync("admin@example.test"))
            .ReturnsAsync(existingUser);
        userManager.Setup(manager => manager.IsInRoleAsync(
                existingUser,
                ApplicationRoleType.Admin.ToString()))
            .ReturnsAsync(true);

        await StartupSeeder.SeedAsync(
            roleManager.Object,
            userManager.Object,
            BuildEnabledConfiguration());

        userManager.Verify(
            manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
        userManager.Verify(
            manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
        userManager.Verify(
            manager => manager.ResetPasswordAsync(
                It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task SeedAsync_WhenBootstrapMemberIsEnabled_CreatesMemberAccount()
    {
        var roleManager = CreateRoleManagerWithExistingRoles();
        var userManager = CreateUserManager();
        userManager.Setup(manager => manager.FindByEmailAsync("portfolio@example.test"))
            .ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(manager => manager.CreateAsync(
                It.Is<ApplicationUser>(user =>
                    user.Email == "portfolio@example.test" && user.UserName == "portfolio-user"),
                "PortfolioPassword1!"))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.IsInRoleAsync(
                It.IsAny<ApplicationUser>(),
                ApplicationRoleType.Member.ToString()))
            .ReturnsAsync(false);
        userManager.Setup(manager => manager.AddToRoleAsync(
                It.IsAny<ApplicationUser>(),
                ApplicationRoleType.Member.ToString()))
            .ReturnsAsync(IdentityResult.Success);

        await StartupSeeder.SeedAsync(
            roleManager.Object,
            userManager.Object,
            BuildConfiguration(
                ("BootstrapMember:Enabled", "true"),
                ("BootstrapMember:Email", "portfolio@example.test"),
                ("BootstrapMember:UserName", "portfolio-user"),
                ("BootstrapMember:Password", "PortfolioPassword1!")));

        userManager.Verify(manager => manager.CreateAsync(
            It.IsAny<ApplicationUser>(), "PortfolioPassword1!"), Times.Once);
        userManager.Verify(manager => manager.AddToRoleAsync(
            It.IsAny<ApplicationUser>(), ApplicationRoleType.Member.ToString()), Times.Once);
    }

    private static IConfiguration BuildEnabledConfiguration() => BuildConfiguration(
        ("BootstrapAdmin:Enabled", "true"),
        ("BootstrapAdmin:Email", "admin@example.test"),
        ("BootstrapAdmin:UserName", "azure-admin"),
        ("BootstrapAdmin:Password", "ValidPassword1!"));

    private static IConfiguration BuildConfiguration(
        params (string Key, string Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(setting => setting.Key, setting => (string?)setting.Value))
            .Build();

    private static Mock<RoleManager<ApplicationRole>> CreateRoleManagerWithExistingRoles()
    {
        var manager = new Mock<RoleManager<ApplicationRole>>(
            Mock.Of<IRoleStore<ApplicationRole>>(),
            Array.Empty<IRoleValidator<ApplicationRole>>(),
            Mock.Of<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Mock.Of<ILogger<RoleManager<ApplicationRole>>>());
        manager.Setup(roleManager => roleManager.RoleExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(true);
        return manager;
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManager() =>
        new(
            Mock.Of<IUserStore<ApplicationUser>>(),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            Mock.Of<IServiceProvider>(),
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());
}
