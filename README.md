# Kototsubo（コトツボ）

**本・ゲーム・映画・音楽をまとめて管理する個人向けコレクション管理Webアプリ**

ISBN・JAN・Steamのデータを使った一括登録、検索・並び替え、所有状態や評価の記録、作品に紐づく言葉の保存に対応しています。

---

## ライブデモ

- サイト: https://kototsubo-gfbvfcg3efhyfucx.japanwest-01.azurewebsites.net/
- ログイン: https://kototsubo-gfbvfcg3efhyfucx.japanwest-01.azurewebsites.net/Account/Login
- 実行環境: Azure App Service / Azure SQL Database

| 項目 | 値 |
|---|---|
| メールアドレス | `member1@sample.jp` |
| パスワード | `Member1!` |
| ロール | `Member` |

この資格情報はポートフォリオ環境専用として意図的に公開しています。Memberもコレクションの登録・編集・削除を実行できるため、デモデータは他の閲覧者によって変更される場合があります。このパスワードを他のサービスや環境で使用しないでください。

Adminアカウントは運用者専用です。資格情報はリポジトリやREADMEには保存していません。一般公開のユーザー登録も無効化しています。

---

## 主な機能

| 機能 | 内容 |
|---|---|
| コレクション管理 | 本・ゲーム・映画・音楽を共通の所蔵品として登録・編集・削除 |
| 検索・絞り込み | キーワード、メディア種別、所有状態、出版社、発売日などで検索 |
| 並び替え・ページング | タイトル、作者、発売日などによる一覧の並び替えとページ分割 |
| 所有情報 | 所有・貸出中・売却済み・処分済み・欲しいものを管理 |
| 詳細情報 | 評価、メモ、取得日、カバー画像、ISBN、JAN、ASIN、Steam App IDなどを保存 |
| ISBN一括登録 | ISBNの直接入力・ファイル入力から書誌情報を検索し、確認後に一括登録 |
| Kindleインポート | KindleのCSV/JSONを読み込み、重複確認と書誌補完を行って一括登録 |
| ゲーム一括登録 | JANコードから楽天ブックスAPIでゲーム情報を取得 |
| DVD/Blu-ray一括登録 | JANコードから楽天ブックスAPIで映像作品情報を取得 |
| 音楽CD一括登録 | JANコードから楽天ブックスAPIで音楽情報を取得 |
| Steamインポート | Steamゲーム一覧JSONを読み込み、既存データを確認して一括登録 |
| 言葉の記録 | 作品や出典に紐づく言葉、発言者、場所、コメント、参考URLを保存 |
| 変更履歴 | 所蔵品と言葉の変更履歴を履歴テーブルへ保存 |
| 認証 | ASP.NET Core Identityによるログイン、ロックアウト、Admin / Memberロール |

---

## 外部データ連携

| 用途 | サービス |
|---|---|
| ISBN書誌検索 | 国立国会図書館サーチ（NDL Search）/ openBD |
| ゲーム・DVD・CD検索 | 楽天ブックスAPI |
| Steamタイトル取込 | Steamゲーム一覧JSON |

楽天APIのApplication IDとAccess Keyはソースへ保存せず、環境変数から読み込みます。バックエンドサービスとして利用する場合は、実行環境の送信元IPアドレスを楽天ウェブサービス側で許可する必要があります。

---

## 技術スタック

| レイヤー | 技術 |
|---|---|
| フレームワーク | .NET 10 / ASP.NET Core MVC |
| UI | Razor Views / Bootstrap |
| ORM | Entity Framework Core 10 |
| データベース | SQL Server / Azure SQL Database |
| 認証 | ASP.NET Core Identity |
| オブジェクトマッピング | AutoMapper 16 |
| HTTP連携 | IHttpClientFactory |
| テスト | xUnit / Moq |
| クラウド | Azure App Service（Windows） |

---

## プロジェクト構成

```text
Kototsubo/
├── Kototsubo/          # ASP.NET Core MVCアプリ
│   ├── Controllers/    # 画面・インポート処理
│   ├── Models/         # ViewModel
│   ├── Services/       # 外部API・パーサー・初期ユーザー作成
│   ├── Repository/     # データアクセス
│   ├── Migrations/     # EF Coreマイグレーション
│   └── Views/          # Razor Views
├── CommonLibrary/      # エンティティと共通処理
├── Tests/              # xUnitテスト
└── scripts/            # 発行物・実行時ディレクトリ検証
```

---

## ローカルセットアップ

### 前提条件

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server、SQL Server Express、またはLocalDB

### 起動手順

```powershell
# 1. クローン
git clone https://github.com/harness17/kototsubo.git
cd kototsubo

# 2. 接続文字列をUser Secretsへ登録
dotnet user-secrets init --project Kototsubo
dotnet user-secrets set "ConnectionStrings:SiteConnection" "Server=.\SQLEXPRESS;Database=KototsuboDB;Integrated Security=True;TrustServerCertificate=True;" --project Kototsubo

# 3. 起動
dotnet run --project Kototsubo
```

アプリ起動時にEF Coreマイグレーションが適用され、`Admin` と `Member` ロールが作成されます。接続文字列は環境変数 `ConnectionStrings__SiteConnection` でも指定できます。

### 初回ユーザー作成

固定ユーザーや固定パスワードはソースに含めていません。初回だけ一時環境変数を設定してユーザーを作成します。

Adminの場合:

```text
BootstrapAdmin__Enabled=true
BootstrapAdmin__Email=<管理者メールアドレス>
BootstrapAdmin__UserName=<管理者ユーザー名>
BootstrapAdmin__Password=<強力なパスワード>
```

Memberの場合:

```text
BootstrapMember__Enabled=true
BootstrapMember__Email=<一般ユーザーメールアドレス>
BootstrapMember__UserName=<一般ユーザー名>
BootstrapMember__Password=<強力なパスワード>
```

設定後にアプリを一度起動し、ログインできることを確認したら、対応する `BootstrapAdmin__*` または `BootstrapMember__*` をすべて削除して再起動します。再実行しても既存ユーザーのパスワードは変更されず、重複ユーザーも作成されません。

---

## Azure App Service設定

Azure環境では次のアプリ設定を使用します。

```text
ASPNETCORE_ENVIRONMENT=Azure
ConnectionStrings__SiteConnection=<Azure SQL接続文字列>
ExternalApis__RakutenApplicationId=<楽天Application ID>
ExternalApis__RakutenAccessKey=<楽天Access Key>
Security__AllowPublicRegistration=false
```

- Azure用ビルド構成は `Azure` です。
- 32bit App Serviceプランでは、自己完結型 `win-x86` として発行します。
- `appsettings.Azure.json`、発行プロファイル、Data Protection鍵、アップロード済みインポートデータはGit管理しません。
- 公開登録は既定で無効です。固定資格情報をソースやマイグレーションへ追加しないでください。

---

## セキュリティ上の前提

- Cookie、セッション、Antiforgery Cookieは本番環境でHTTPSを要求します。
- ログイン失敗5回で5分間ロックアウトします。
- 外部APIキーとDB接続情報は環境変数またはUser Secretsで管理します。
- インポート一時ファイルはセッション所有権を検証し、ユーザー入力パスを直接使用しません。
- 現在は単一テナントのコレクションモデルです。ユーザーごとのデータ分離は保証していません。
- AdminとMemberはどちらもコレクションのCRUD操作が可能です。

---

## ビルドとテスト

```powershell
dotnet build Kototsubo.slnx
dotnet test Tests/Tests.csproj
```

発行時の実行ディレクトリ構成も確認する場合:

```powershell
pwsh -File scripts/verify-runtime-directories.ps1
```

---

## 開発・公開ルール

- 正規ブランチは `main` のみです。
- ステージ対象はファイル単位で明示し、秘密情報やローカル設定を含めません。
- 公開前にビルド、テスト、実行時ディレクトリ、環境変数、APIキー、接続情報を確認します。

---

## 作者

[harness17](https://github.com/harness17)
