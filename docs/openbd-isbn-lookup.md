# openBD ISBN 検索 — 取得可能情報リファレンス

## API 概要

- **エンドポイント**: `https://api.openbd.jp/v1/get?isbn={ISBN}`
- **認証**: 不要（無料・クォータ制限緩い）
- **バッチ取得**: カンマ区切りで最大100件まで一括取得可能
- **対象**: ISBN（13桁/10桁）を持つ**日本国内の書籍**のみ
- **公式ドキュメント**: https://openbd.jp/

## 取得可能フィールドと Kototsubo での利用状況

### summary（基本情報）— 全書籍でほぼ取得可能

| openBD フィールド | 内容 | Kototsubo マッピング先 | 備考 |
|---|---|---|---|
| `summary.isbn` | ISBN-13 | `ISBN` | 必ず返る |
| `summary.title` | タイトル | `Title` | 必ず返る |
| `summary.author` | 著者名 | `Creator` | カンマ区切り、生年付きの場合あり |
| `summary.publisher` | 出版社 | `Publisher` | 必ず返る |
| `summary.pubdate` | 出版年月 | `ReleaseDate` | `YYYYMM` 形式（日は含まない） |
| `summary.cover` | 書影URL | `CoverImageUrl` | **空文字の場合が多い**（出版社によって提供なし） |
| `summary.volume` | 巻数 | — | 未使用 |
| `summary.series` | シリーズ名 | — | 未使用（例: 「新潮文庫」「ジャンプ・コミックス」） |

### onix.DescriptiveDetail（詳細情報）— 一部書籍でのみ取得可能

| openBD フィールド | 内容 | Kototsubo マッピング先 | 備考 |
|---|---|---|---|
| `Extent[].ExtentValue` | ページ数 | `PageCount` | **取得できない書籍が多い**。Extentセクション自体が存在しないケースが大半 |
| `TitleDetail.TitleElement.TitleText.content` | 正式タイトル | — | summary.title とほぼ同じ |
| `Contributor[].PersonName.content` | 著者名（個別） | — | 役割別に分かれる場合がある |
| `Collection.TitleDetail.TitleElement[].TitleText.content` | シリーズ名 | — | summary.series とほぼ同じ |

### onix.PublishingDetail（出版情報）

| openBD フィールド | 内容 | Kototsubo マッピング先 | 備考 |
|---|---|---|---|
| `Imprint.ImprintName` | 版元名 | — | summary.publisher と同じことが多い |
| `PublishingDate[].Date` | 出版年月 | — | `YYYYMM` 形式 |

### onix.ProductSupply（流通情報）

| openBD フィールド | 内容 | Kototsubo マッピング先 | 備考 |
|---|---|---|---|
| `Price[].PriceAmount` | 税抜価格 | — | 未使用 |
| `Price[].CurrencyCode` | 通貨コード | — | 常に `JPY` |
| `ProductAvailability` | 入手可否 | — | `99` = 不明、`21` = 入手可能 |

### hanmoto（版元ドットコム情報）— 一部書籍のみ

| openBD フィールド | 内容 | Kototsubo マッピング先 | 備考 |
|---|---|---|---|
| `reviews[]` | 書評一覧 | — | 未使用。新聞書評など |
| `dateshuppan` | 出版日 | — | `YYYY-MM` 形式 |
| `datecreated` | データ作成日 | — | openBD 側の管理情報 |

## 取得できない情報

| 情報 | 理由 |
|---|---|
| **書影（カバー画像）** | 出版社の多くがopenBDに書影を提供していない。`summary.cover` が空文字のケースが大半 |
| **ページ数** | `onix.DescriptiveDetail.Extent` が存在しない書籍が多い。特にコミックは非対応率が高い |
| **あらすじ・内容紹介** | `CollateralDetail` が空のケースが大半 |
| **JANコード** | openBD は ISBN 専用。ゲーム・映画・音楽の JAN 検索は不可 |
| **Kindle 専用 ASIN** | `B0` で始まる ASIN は ISBN と無関係なため openBD では検索不可 |

## ASIN と ISBN の関係

| ASIN 形式 | ISBN 互換 | openBD 検索 |
|---|---|---|
| 10桁数字（末尾 X 含む） | **ISBN-10 と同一** → ISBN-13 に変換可能 | 可能 |
| `B0` で始まる英数字 | Kindle 専用。ISBN なし | **不可** |

ISBN-10 互換の ASIN は `OpenBDLookupService.NormalizeIsbn13()` で ISBN-13 に変換して検索する。

## レスポンス例

```
GET https://api.openbd.jp/v1/get?isbn=9784088801377
```

```json
[
  {
    "summary": {
      "isbn": "9784088801377",
      "title": "暗殺教室 10 (泥棒の時間)",
      "author": "松井,優征,1979-",
      "publisher": "集英社",
      "pubdate": "201407",
      "cover": "",
      "volume": "",
      "series": "ジャンプ・コミックス"
    },
    "onix": {
      "DescriptiveDetail": {
        "TitleDetail": { "..." : "..." },
        "Contributor": [
          {
            "PersonName": { "content": "松井, 優征, 1979-" }
          }
        ]
      },
      "PublishingDetail": {
        "PublishingDate": [{ "Date": "201407" }]
      },
      "ProductSupply": {
        "SupplyDetail": {
          "Price": [{ "PriceAmount": "400", "CurrencyCode": "JPY" }]
        }
      }
    }
  }
]
```

## 代替 API（将来候補）

| API | 対象 | 書影 | ページ数 | コスト |
|---|---|---|---|---|
| 国立国会図書館サーチ | 日本語書籍全般 | なし | あり（一部） | 無料 |
| Google Books API | 英語圏に強い | あり | あり | 無料（日次1,000件） |
| 楽天ブックス API | 書籍+CD+DVD+ゲーム | あり | なし | 無料（アプリID必要） |
| Amazon PA-API 5.0 | 最も網羅的 | あり | あり | 売上発生が必要 |
