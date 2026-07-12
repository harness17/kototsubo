using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Site.Controllers;
using Site.Services;
using Xunit;

namespace Tests;

public class SecurityAttributeTests
{
    [Fact]
    public void ProductionDefaults_DisablePublicRegistration()
    {
        var settingsPath = Path.Combine(
            FindRepositoryRoot(),
            "Kototsubo",
            "appsettings.json");
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(settingsPath)
            .Build();

        Assert.False(configuration.GetValue<bool>("Security:AllowPublicRegistration"));
    }

    [Fact]
    public void ImportController_RequiresAuthorization()
    {
        Assert.NotNull(Attribute.GetCustomAttribute(
            typeof(ImportController),
            typeof(AuthorizeAttribute)));
    }

    [Theory]
    [InlineData(nameof(ImportController.Kindle))]
    [InlineData(nameof(ImportController.KindleConfirm))]
    [InlineData(nameof(ImportController.Isbn))]
    [InlineData(nameof(ImportController.IsbnConfirm))]
    public void ImportPostActions_ValidateAntiforgeryToken(string actionName)
    {
        var method = typeof(ImportController)
            .GetMethods()
            .Single(x =>
                x.Name == actionName &&
                x.GetCustomAttributes(typeof(HttpPostAttribute), true).Length > 0);

        Assert.NotEmpty(method.GetCustomAttributes(
            typeof(ValidateAntiForgeryTokenAttribute),
            true));
    }

    [Fact]
    public void KindleConfirm_AllowsFormValuesUpToImportLimit()
    {
        var method = typeof(ImportController)
            .GetMethod(nameof(ImportController.KindleConfirm))!;

        var attribute = Assert.Single(method.GetCustomAttributes(
            typeof(RequestFormLimitsAttribute),
            true).Cast<RequestFormLimitsAttribute>());

        Assert.Equal(KindleImportParser.MaxItemCount + 2, attribute.ValueCountLimit);
    }

    [Fact]
    public void IsbnConfirm_AllowsFormValuesUpToImportLimit()
    {
        var method = typeof(ImportController)
            .GetMethod(nameof(ImportController.IsbnConfirm))!;

        var attribute = Assert.Single(method.GetCustomAttributes(
            typeof(RequestFormLimitsAttribute),
            true).Cast<RequestFormLimitsAttribute>());

        Assert.Equal(ImportController.MaxIsbnCount * 2 + 4, attribute.ValueCountLimit);
        Assert.Equal(KindleImportParser.MaxItemCount, ImportController.MaxIsbnCount);
    }

    [Fact]
    public void IsbnPreview_AllowsConfiguredFileSize()
    {
        var method = typeof(ImportController)
            .GetMethods()
            .Single(x =>
                x.Name == nameof(ImportController.Isbn) &&
                x.GetCustomAttributes(typeof(HttpPostAttribute), true).Length > 0);

        var attribute = Assert.Single(method.GetCustomAttributes(
            typeof(RequestFormLimitsAttribute),
            true).Cast<RequestFormLimitsAttribute>());

        Assert.Equal(ImportController.MaxIsbnFileSize + 65536, attribute.MultipartBodyLengthLimit);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(startPath);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Kototsubo.slnx")))
                directory = directory.Parent;

            if (directory != null)
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
