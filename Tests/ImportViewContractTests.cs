using System.Text.RegularExpressions;
using Xunit;

namespace Tests;

/// <summary>
/// Razor ビュー間で JavaScript リテラルとして共有される契約の整合を検証する。
/// コンパイラが検出できない文字列同士の参照は、ビュー実体から抽出して突き合わせる。
/// </summary>
public class ImportViewContractTests
{
    // TitleSearch.cshtml がページ跨ぎ選択を保存する sessionStorage キーと、
    // IsbnResult.cshtml が登録完了時に削除するキーの一致を検証する。
    // v2 キー移行時にクリア側の更新が漏れ、登録後の再検索で選択が再現される
    // バグが発生したため、キー変更が片側だけにならないことを保証する。
    [Fact]
    public void IsbnResult_RemovesSameSessionStorageKeyAsTitleSearchSaves()
    {
        var viewsDir = FindViewsImportDirectory();
        var titleSearch = File.ReadAllText(Path.Combine(viewsDir, "TitleSearch.cshtml"));
        var isbnResult = File.ReadAllText(Path.Combine(viewsDir, "IsbnResult.cshtml"));

        var saveKey = Regex.Match(titleSearch, @"STORAGE_KEY = '([^']+)'");
        var clearKey = Regex.Match(isbnResult, @"sessionStorage\.removeItem\('([^']+)'\)");

        Assert.True(saveKey.Success,
            "TitleSearch.cshtml に STORAGE_KEY の定義が見つかりません。");
        Assert.True(clearKey.Success,
            "IsbnResult.cshtml に sessionStorage.removeItem が見つかりません。");
        Assert.Equal(saveKey.Groups[1].Value, clearKey.Groups[1].Value);
    }

    private static string FindViewsImportDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Kototsubo", "Views", "Import");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Kototsubo/Views/Import が見つかりません。");
    }
}
