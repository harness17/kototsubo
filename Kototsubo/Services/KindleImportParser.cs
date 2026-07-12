using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
using Site.Models;

namespace Site.Services
{
    public class KindleImportParser
    {
        public const int MaxItemCount = 20_000;

        public IReadOnlyList<KindleImportRow> Parse(Stream stream, string extension)
        {
            if (stream == null || !stream.CanRead)
                throw new KindleImportException("ファイルを読み取れませんでした。");

            return extension.ToLowerInvariant() switch
            {
                ".csv" => ParseCsv(stream),
                ".json" => ParseJson(stream),
                _ => throw new KindleImportException("CSV または JSON ファイルを選択してください。")
            };
        }

        private static IReadOnlyList<KindleImportRow> ParseCsv(Stream stream)
        {
            try
            {
                using var parser = new TextFieldParser(stream, Encoding.UTF8, true)
                {
                    TextFieldType = FieldType.Delimited,
                    HasFieldsEnclosedInQuotes = true,
                    TrimWhiteSpace = false
                };
                parser.SetDelimiters(",");

                if (parser.EndOfData)
                    throw new KindleImportException("ファイルが空です。");

                var headers = parser.ReadFields();
                if (headers == null)
                    throw new KindleImportException("CSV ヘッダーを読み取れませんでした。");

                var headerIndexes = headers
                    .Select((name, index) => new
                    {
                        Name = name.Trim().TrimStart('\uFEFF'),
                        Index = index
                    })
                    .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.First().Index, StringComparer.OrdinalIgnoreCase);

                EnsureColumns(headerIndexes, "title", "asin");

                var rows = new List<KindleImportRow>();
                while (!parser.EndOfData)
                {
                    var fields = parser.ReadFields();
                    if (fields == null || fields.All(string.IsNullOrWhiteSpace))
                        continue;

                    var title = GetField(fields, headerIndexes, "title");
                    var asin = GetField(fields, headerIndexes, "asin");
                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(asin))
                        throw new KindleImportException("title と asin は各行で必須です。");

                    rows.Add(new KindleImportRow
                    {
                        Title = title.Trim(),
                        Creator = GetField(fields, headerIndexes, "authors")?.Trim(),
                        ASIN = asin.Trim(),
                        Series = GetField(fields, headerIndexes, "series")?.Trim(),
                        Volume = ParseNullableInt(GetField(fields, headerIndexes, "volume")),
                        AcquiredTime = ParseCsvDate(GetField(fields, headerIndexes, "acquiredTime")),
                        ReadStatus = GetField(fields, headerIndexes, "readStatus")?.Trim()
                    });
                }

                if (rows.Count == 0)
                    throw new KindleImportException("登録対象のデータがありません。");

                ValidateRows(rows);
                return rows;
            }
            catch (KindleImportException)
            {
                throw;
            }
            catch (MalformedLineException)
            {
                throw new KindleImportException("CSV の形式が正しくありません。");
            }
            catch (Exception)
            {
                throw new KindleImportException("CSV を解析できませんでした。");
            }
        }

        private static IReadOnlyList<KindleImportRow> ParseJson(Stream stream)
        {
            try
            {
                using var document = JsonDocument.Parse(stream);
                if (!document.RootElement.TryGetProperty("items", out var items) ||
                    items.ValueKind != JsonValueKind.Array)
                {
                    throw new KindleImportException("JSON に items 配列がありません。");
                }

                var rows = new List<KindleImportRow>();
                foreach (var item in items.EnumerateArray())
                {
                    var title = GetJsonString(item, "title");
                    var asin = GetJsonString(item, "asin");
                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(asin))
                        throw new KindleImportException("title と asin は各項目で必須です。");

                    rows.Add(new KindleImportRow
                    {
                        Title = title.Trim(),
                        Creator = JoinAuthors(item),
                        ASIN = asin.Trim(),
                        Series = GetJsonString(item, "seriesKey")?.Trim(),
                        Volume = GetJsonInt(item, "volume"),
                        AcquiredTime = GetUnixTime(item, "acquiredTime"),
                        ReadStatus = GetJsonString(item, "readStatus")?.Trim(),
                        ThumbnailUrl = GetJsonString(item, "thumbnailUrl")?.Trim()
                    });
                }

                if (rows.Count == 0)
                    throw new KindleImportException("登録対象のデータがありません。");

                ValidateRows(rows);
                return rows;
            }
            catch (KindleImportException)
            {
                throw;
            }
            catch (JsonException)
            {
                throw new KindleImportException("JSON の形式が正しくありません。");
            }
            catch (Exception)
            {
                throw new KindleImportException("JSON を解析できませんでした。");
            }
        }

        private static void EnsureColumns(
            IReadOnlyDictionary<string, int> headers,
            params string[] requiredColumns)
        {
            var missing = requiredColumns.Where(column => !headers.ContainsKey(column)).ToArray();
            if (missing.Length > 0)
            {
                throw new KindleImportException(
                    $"必須カラムがありません: {string.Join(", ", missing)}");
            }
        }

        private static void ValidateRows(IEnumerable<KindleImportRow> rows)
        {
            var rowList = rows as IReadOnlyCollection<KindleImportRow> ?? rows.ToList();
            if (rowList.Count > MaxItemCount)
            {
                throw new KindleImportException(
                    $"一度に登録できる件数は{MaxItemCount:N0}件までです。");
            }

            foreach (var row in rowList)
            {
                if (row.Title!.Length > 500)
                    throw new KindleImportException("title は500文字以下にしてください。");
                if (row.ASIN!.Length > 10)
                    throw new KindleImportException("asin は10文字以下にしてください。");
                if (row.Creator?.Length > 500)
                    throw new KindleImportException("authors は500文字以下にしてください。");
                if (row.ThumbnailUrl?.Length > 1000)
                    throw new KindleImportException("thumbnailUrl は1000文字以下にしてください。");
            }
        }

        private static string? GetField(
            string[] fields,
            IReadOnlyDictionary<string, int> headers,
            string name)
        {
            return headers.TryGetValue(name, out var index) && index < fields.Length
                ? fields[index]
                : null;
        }

        private static int? ParseNullableInt(string? value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
                ? result
                : null;
        }

        private static DateTime? ParseCsvDate(string? value)
        {
            return DateTime.TryParseExact(
                value?.Trim(),
                "yyyy/MM/dd HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var result)
                ? result
                : null;
        }

        private static string? GetJsonString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) &&
                   value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static int? GetJsonInt(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value)) return null;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number;
            if (value.ValueKind == JsonValueKind.String &&
                int.TryParse(value.GetString(), out var stringNumber))
                return stringNumber;
            return null;
        }

        private static DateTime? GetUnixTime(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value) ||
                value.ValueKind != JsonValueKind.Number ||
                !value.TryGetInt64(out var milliseconds))
            {
                return null;
            }

            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).LocalDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        private static string? JoinAuthors(JsonElement item)
        {
            if (!item.TryGetProperty("authors", out var authors) ||
                authors.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var values = authors.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));
            return string.Join(" / ", values);
        }
    }

    public class KindleImportException : Exception
    {
        public KindleImportException(string message) : base(message)
        {
        }
    }
}
