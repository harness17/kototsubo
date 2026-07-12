using Dev.CommonLibrary.Entity;
using Microsoft.AspNetCore.Identity;
using Site.Common;

namespace Site.Services;

internal static class StartupSeeder
{
    internal static async Task SeedAsync(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        await EnsureRoleAsync(roleManager, ApplicationRoleType.Admin.ToString(), "1");
        await EnsureRoleAsync(roleManager, ApplicationRoleType.Member.ToString(), "2");

        await EnsureBootstrapUserAsync(
            userManager,
            BootstrapUserSettings.FromConfiguration(configuration, "BootstrapAdmin"),
            ApplicationRoleType.Admin.ToString());
        await EnsureBootstrapUserAsync(
            userManager,
            BootstrapUserSettings.FromConfiguration(configuration, "BootstrapMember"),
            ApplicationRoleType.Member.ToString());
    }

    private static async Task EnsureBootstrapUserAsync(
        UserManager<ApplicationUser> userManager,
        BootstrapUserSettings? bootstrap,
        string roleName)
    {
        if (bootstrap == null) return;

        var user = await userManager.FindByEmailAsync(bootstrap.Email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = bootstrap.UserName,
                Email = bootstrap.Email
            };

            var createResult = await userManager.CreateAsync(user, bootstrap.Password);
            EnsureSucceeded(createResult, $"Bootstrap {roleName} の作成");
        }

        // 再起動時にパスワードを上書きせず、指定ロールだけを冪等に保証する。
        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            var roleResult = await userManager.AddToRoleAsync(user, roleName);
            EnsureSucceeded(roleResult, $"Bootstrap {roleName} へのロール付与");
        }
    }

    private static async Task EnsureRoleAsync(
        RoleManager<ApplicationRole> roleManager,
        string roleName,
        string roleId)
    {
        if (await roleManager.RoleExistsAsync(roleName)) return;

        var result = await roleManager.CreateAsync(new ApplicationRole { Id = roleId, Name = roleName });
        EnsureSucceeded(result, $"{roleName} ロールの作成");
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded) return;

        var errorCodes = string.Join(", ", result.Errors.Select(error => error.Code));
        throw new InvalidOperationException($"{operation}に失敗しました。Identity error codes: {errorCodes}");
    }
}

internal sealed record BootstrapUserSettings(string Email, string UserName, string Password)
{
    internal static BootstrapUserSettings? FromConfiguration(
        IConfiguration configuration,
        string sectionName)
    {
        if (!configuration.GetValue<bool>($"{sectionName}:Enabled")) return null;

        var email = configuration[$"{sectionName}:Email"];
        var userName = configuration[$"{sectionName}:UserName"];
        var password = configuration[$"{sectionName}:Password"];

        var missingKeys = new[]
        {
            (Key: $"{sectionName}:Email", Value: email),
            (Key: $"{sectionName}:UserName", Value: userName),
            (Key: $"{sectionName}:Password", Value: password)
        }
        .Where(setting => string.IsNullOrWhiteSpace(setting.Value))
        .Select(setting => setting.Key)
        .ToArray();

        if (missingKeys.Length > 0)
        {
            throw new InvalidOperationException(
                $"{sectionName} is enabled, but required settings are missing: {string.Join(", ", missingKeys)}");
        }

        return new BootstrapUserSettings(email!, userName!, password!);
    }
}
