using System.ComponentModel.DataAnnotations;
using Dev.CommonLibrary.Entity;
using Site.Common;

namespace Site.Entity
{
    /// <summary>
    /// 言葉エンティティの共通プロパティを定義する抽象基底クラス。
    /// </summary>
    public abstract class WordEntityBase : SiteEntityBase
    {
        /// <summary>収集した言葉の本文。</summary>
        [Required]
        [StringLength(4000)]
        public string Body { get; set; } = string.Empty;

        /// <summary>出典元タイトル。</summary>
        [StringLength(500)]
        public string? SourceTitle { get; set; }

        /// <summary>出典の著者・制作者。</summary>
        [StringLength(500)]
        public string? Author { get; set; }

        /// <summary>言葉の発言者。</summary>
        [StringLength(500)]
        public string? Speaker { get; set; }

        /// <summary>言葉に出会った場所。</summary>
        [StringLength(500)]
        public string? Place { get; set; }

        /// <summary>感想・メモ。</summary>
        [StringLength(2000)]
        public string? Comment { get; set; }

        /// <summary>参考リンク。</summary>
        [StringLength(1000)]
        public string? ReferenceUrl { get; set; }

        /// <summary>言葉のジャンル・媒体。</summary>
        public WordGenre? Genre { get; set; }

        /// <summary>関連する所蔵品ID。</summary>
        public long? ItemId { get; set; }

        /// <summary>登録ユーザーID。</summary>
        [StringLength(450)]
        public string? UserId { get; set; }

        /// <summary>公開設定。</summary>
        public bool IsPublic { get; set; }
    }
}
