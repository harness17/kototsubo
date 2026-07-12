using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Site.Common;
using Site.Controllers;
using Site.Models;
using Xunit;

namespace Tests;

public class SteamImportTests
{
    [Fact]
    public void ParseJson_ValidSteamJson_ReadsGamesArray()
    {
        var result = SteamImportController.ParseJson("""
            {
              "steamId": "76561198070177241",
              "gameCount": 2,
              "games": [
                { "appid": 10, "name": "Counter-Strike", "playtime_forever": 0 },
                {
                  "appid": 20,
                  "name": "Team Fortress Classic",
                  "playtime_forever": 451,
                  "coverImageUrl": "https://example.test/steam/20/header.jpg"
                }
              ]
            }
            """);

        Assert.Equal("76561198070177241", result.SteamId);
        Assert.Equal(2, result.TotalGames);
        Assert.Collection(
            result.Rows,
            row =>
            {
                Assert.Equal("10", row.AppId);
                Assert.Equal("Counter-Strike", row.Name);
                Assert.Equal(0, row.PlaytimeMinutes);
            },
            row =>
            {
                Assert.Equal("20", row.AppId);
                Assert.Equal("Team Fortress Classic", row.Name);
                Assert.Equal(451, row.PlaytimeMinutes);
                Assert.Equal("https://example.test/steam/20/header.jpg", row.CoverImageUrl);
            });
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{ "steamId": "1" }""")]
    public void ParseJson_InvalidJson_ThrowsUserFacingError(string json)
    {
        var exception = Assert.Throws<InvalidDataException>(
            () => SteamImportController.ParseJson(json));

        Assert.NotEmpty(exception.Message);
    }

    [Fact]
    public void ParseJson_CurrentSteamExportRowsWithoutCoverUrls_LeavesCoverImageUrlsNull()
    {
        var result = SteamImportController.ParseJson("""
            {
              "steamId": "76561198070177241",
              "gameCount": 392,
              "games": [
                {
                  "appid": 3764200,
                  "name": "Resident Evil Requiem",
                  "playtime_forever": 0,
                  "img_icon_url": "382b0b74c3154fae52a33be0108e1748f87a9478"
                },
                {
                  "appid": 3816930,
                  "name": "Grind Survivors",
                  "playtime_forever": 528,
                  "img_icon_url": "07a9c5ef0b79307d6de49e4ce6b6f2a4c2d85d45"
                },
                {
                  "appid": 4005090,
                  "name": "Dungeon Antiqua 2",
                  "playtime_forever": 59,
                  "img_icon_url": "a38ae176c3bd27d2a2b16830e6ef3931263e9c0a"
                },
                {
                  "appid": 4402650,
                  "name": "METRO QUESTER",
                  "playtime_forever": 0,
                  "img_icon_url": "4f919afeaa20c705995e61d1773dd5cfcd856083"
                }
              ]
            }
            """);

        Assert.Collection(
            result.Rows,
            row => Assert.Null(row.CoverImageUrl),
            row => Assert.Null(row.CoverImageUrl),
            row => Assert.Null(row.CoverImageUrl),
            row => Assert.Null(row.CoverImageUrl));
    }

    [Fact]
    public void ToEntity_SteamImport_MapsGameFields()
    {
        var entity = SteamImportController.ToEntity(new SteamImportSnapshotRow
        {
            AppId = "20",
            Name = "Team Fortress Classic",
            PlaytimeMinutes = 451,
            CoverImageUrl = "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/20/abc/header.jpg"
        });

        Assert.Equal("20", entity.SteamAppId);
        Assert.Equal("Team Fortress Classic", entity.Title);
        Assert.Equal(MediaType.Game, entity.MediaType);
        Assert.Equal("PC (Steam)", entity.Platform);
        Assert.True(entity.IsDigital);
        Assert.Equal(OwnershipStatus.Owned, entity.OwnershipStatus);
        Assert.Equal(DateTime.Today, entity.AcquisitionDate);
        Assert.Equal(
            "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/20/abc/header.jpg",
            entity.CoverImageUrl);
    }

    [Fact]
    public void ToEntity_NoCoverImageResolved_LeavesCoverImageUrlNull()
    {
        var entity = SteamImportController.ToEntity(new SteamImportSnapshotRow
        {
            AppId = "4402650",
            Name = "METRO QUESTER",
            PlaytimeMinutes = 0,
            CoverImageUrl = null
        });

        Assert.Null(entity.CoverImageUrl);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(451, "Steam プレイ時間: 7.5 時間")]
    public void BuildMemo_ConvertsPlaytimeMinutesToHours(int minutes, string? expected)
    {
        Assert.Equal(expected, SteamImportController.BuildMemo(minutes));
    }

    [Fact]
    public void GetValidationError_TitleTooLong_ReturnsError()
    {
        var error = SteamImportController.GetValidationError(new SteamImportSnapshotRow
        {
            AppId = "10",
            Name = new string('a', 501)
        });

        Assert.Equal("タイトルが登録可能な長さを超えています。", error);
    }

    [Theory]
    [InlineData("10", true)]
    [InlineData("", false)]
    [InlineData("abc", false)]
    [InlineData("123abc", false)]
    public void IsValidAppIdFormat_ValidatesDigitsOnly(string appId, bool expected)
    {
        Assert.Equal(expected, SteamImportController.IsValidAppIdFormat(appId));
    }

    [Fact]
    public void SelectCoverImageUrl_JsonCoverUrlExists_UsesJsonUrlBeforeResolvedUrl()
    {
        var row = new SteamImportRow
        {
            AppId = "10",
            CoverImageUrl = "https://example.test/from-json.jpg"
        };
        var resolved = new Dictionary<string, string?>
        {
            ["10"] = "https://example.test/from-api.jpg"
        };

        var url = SteamImportController.SelectCoverImageUrl(row, resolved);

        Assert.Equal("https://example.test/from-json.jpg", url);
    }

    [Fact]
    public void SelectCoverImageUrl_ResolvedCoverUrlExists_UsesResolvedUrl()
    {
        var row = new SteamImportRow { AppId = "10" };
        var resolved = new Dictionary<string, string?>
        {
            ["10"] = "https://cdn.akamai.steamstatic.com/steam/apps/10/library_hero.jpg"
        };

        var url = SteamImportController.SelectCoverImageUrl(row, resolved);

        Assert.Equal(
            "https://cdn.akamai.steamstatic.com/steam/apps/10/library_hero.jpg",
            url);
    }

    [Fact]
    public void SelectCoverImageUrl_NoJsonOrResolvedCover_ReturnsNull()
    {
        var row = new SteamImportRow { AppId = "4402650" };
        var resolved = new Dictionary<string, string?>
        {
            ["4402650"] = null
        };

        var url = SteamImportController.SelectCoverImageUrl(row, resolved);

        Assert.Null(url);
    }

    [Fact]
    public void GetValidationError_CoverImageUrlTooLong_ReturnsError()
    {
        var error = SteamImportController.GetValidationError(new SteamImportSnapshotRow
        {
            AppId = "10",
            Name = "Counter-Strike",
            CoverImageUrl = new string('a', 1001)
        });

        Assert.Equal("カバー画像URLが登録可能な長さを超えています。", error);
    }

    [Fact]
    public void GetValidationError_NoCoverImageUrl_IsValid()
    {
        var error = SteamImportController.GetValidationError(new SteamImportSnapshotRow
        {
            AppId = "4402650",
            Name = "METRO QUESTER",
            CoverImageUrl = null
        });

        Assert.Null(error);
    }

    [Fact]
    public void MarkDuplicates_FlagsExistingAndInputDuplicateAppIds()
    {
        var rows = new List<SteamImportRow>
        {
            new() { AppId = "10", Name = "Existing" },
            new() { AppId = "20", Name = "First" },
            new() { AppId = "20", Name = "Second" }
        };

        SteamImportController.MarkDuplicates(
            rows,
            new HashSet<string>(["10"], StringComparer.OrdinalIgnoreCase));

        Assert.True(rows[0].IsDuplicate);
        Assert.False(rows[1].IsDuplicate);
        Assert.True(rows[2].IsDuplicate);
    }

    [Fact]
    public void SteamImportController_RequiresAuthorization()
    {
        Assert.NotNull(Attribute.GetCustomAttribute(
            typeof(SteamImportController),
            typeof(AuthorizeAttribute)));
    }

    [Theory]
    [InlineData(nameof(SteamImportController.Index))]
    [InlineData(nameof(SteamImportController.Confirm))]
    public void SteamImportPostActions_ValidateAntiforgeryToken(string actionName)
    {
        var method = typeof(SteamImportController)
            .GetMethods()
            .Single(x =>
                x.Name == actionName &&
                x.GetCustomAttributes(typeof(HttpPostAttribute), true).Length > 0);

        Assert.NotEmpty(method.GetCustomAttributes(
            typeof(ValidateAntiForgeryTokenAttribute),
            true));
    }

    [Fact]
    public void SteamImportPreview_AllowsConfiguredFileSize()
    {
        var method = typeof(SteamImportController)
            .GetMethods()
            .Single(x =>
                x.Name == nameof(SteamImportController.Index) &&
                x.GetCustomAttributes(typeof(HttpPostAttribute), true).Length > 0);

        var attribute = Assert.Single(method.GetCustomAttributes(
            typeof(RequestFormLimitsAttribute),
            true).Cast<RequestFormLimitsAttribute>());

        Assert.Equal(SteamImportController.MaxFileSize + 65536, attribute.MultipartBodyLengthLimit);
    }

    [Fact]
    public void SteamImportConfirm_AllowsFormValuesUpToImportLimit()
    {
        var method = typeof(SteamImportController)
            .GetMethod(nameof(SteamImportController.Confirm))!;

        var attribute = Assert.Single(method.GetCustomAttributes(
            typeof(RequestFormLimitsAttribute),
            true).Cast<RequestFormLimitsAttribute>());

        Assert.Equal(SteamImportController.MaxGameCount + 2, attribute.ValueCountLimit);
    }

    [Fact]
    public void ItemViewModel_SteamAppId_AllowsTwentyCharacters()
    {
        var model = new ItemViewModel
        {
            Title = "Game",
            MediaType = MediaType.Game,
            SteamAppId = new string('1', 20)
        };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);

        Assert.True(isValid);
    }

    [Fact]
    public async Task ReadSteamJsonFileAsync_ValidJsonFile_ReturnsContent()
    {
        var file = CreateFile("steam.json", Encoding.UTF8.GetBytes("""{ "games": [] }"""));

        var result = await SteamImportController.ReadSteamJsonFileAsync(file);

        Assert.Equal("""{ "games": [] }""", result);
    }

    private static FormFile CreateFile(string fileName, byte[] bytes)
    {
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", fileName);
    }
}
