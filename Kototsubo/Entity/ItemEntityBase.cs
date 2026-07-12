using System.ComponentModel.DataAnnotations;
using Dev.CommonLibrary.Entity;
using Site.Common;

namespace Site.Entity
{
    /// <summary>
    /// 所蔵品エンティティの共通プロパティを定義する抽象基底クラス。
    /// ItemEntity（本体）と ItemEntityHistory（履歴）の両方がこれを継承する。
    /// </summary>
    public abstract class ItemEntityBase : SiteEntityBase
    {
        // === 共通プロパティ ===

        /// <summary>タイトル</summary>
        [Required]
        [StringLength(500)]
        public string Title { get; set; } = string.Empty;

        /// <summary>メディア種別</summary>
        [Required]
        public MediaType MediaType { get; set; }

        /// <summary>所有ステータス</summary>
        [Required]
        public OwnershipStatus OwnershipStatus { get; set; } = OwnershipStatus.Owned;

        /// <summary>デジタル版フラグ（Kindle・DL版 = true）</summary>
        public bool IsDigital { get; set; }

        /// <summary>取得日</summary>
        public DateTime? AcquisitionDate { get; set; }

        /// <summary>評価（1–5）</summary>
        public int? Rating { get; set; }

        /// <summary>メモ</summary>
        [StringLength(2000)]
        public string? Memo { get; set; }

        /// <summary>カバー画像URL</summary>
        [StringLength(1000)]
        public string? CoverImageUrl { get; set; }

        // === 識別コード ===

        /// <summary>ISBN（書籍、13桁）</summary>
        [StringLength(13)]
        public string? ISBN { get; set; }

        /// <summary>JANコード（ゲーム・映画・音楽）</summary>
        [StringLength(13)]
        public string? JANCode { get; set; }

        /// <summary>ASIN（Amazon・Kindle）</summary>
        [StringLength(10)]
        public string? ASIN { get; set; }

        /// <summary>Steam アプリ ID</summary>
        [StringLength(20)]
        public string? SteamAppId { get; set; }

        // === クリエイター・出版（メディア共通） ===

        /// <summary>クリエイター（著者 / 開発者 / 監督 / アーティスト）</summary>
        [StringLength(500)]
        public string? Creator { get; set; }

        /// <summary>出版社 / スタジオ / レーベル</summary>
        [StringLength(500)]
        public string? Publisher { get; set; }

        /// <summary>発売日 / 出版日</summary>
        public DateTime? ReleaseDate { get; set; }

        // === メディア固有 ===

        /// <summary>プラットフォーム（ゲーム: PS5, Switch, PC 等）</summary>
        [StringLength(100)]
        public string? Platform { get; set; }

        /// <summary>メディアフォーマット（DVD / Blu-ray / CD 等）</summary>
        [StringLength(100)]
        public string? Format { get; set; }

        /// <summary>ページ数（書籍）</summary>
        public int? PageCount { get; set; }

        /// <summary>ディスク枚数（映画 / 音楽）</summary>
        public int? DiscCount { get; set; }

        /// <summary>上映時間（映画、分単位）</summary>
        public int? Runtime { get; set; }
    }
}
