using Dev.CommonLibrary.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Site.Common
{
    /// <summary>アプリケーションロール定義。</summary>
    public enum ApplicationRoleType
    {
        [SubValue("1")]
        [Display(Name = "管理者", Order = 1)]
        Admin = 1,
        [SubValue("2")]
        [Display(Name = "一般", Order = 2)]
        Member = 2,
    }

    /// <summary>メディア種別。</summary>
    public enum MediaType
    {
        [Display(Name = "本")]
        Book = 1,
        [Display(Name = "ゲーム")]
        Game = 2,
        [Display(Name = "映画")]
        Movie = 3,
        [Display(Name = "音楽")]
        Music = 4,
    }

    /// <summary>所有ステータス。</summary>
    public enum OwnershipStatus
    {
        [Display(Name = "所有")]
        Owned = 1,
        [Display(Name = "貸出中")]
        Lent = 2,
        [Display(Name = "売却済み")]
        Sold = 3,
        [Display(Name = "処分済み")]
        Disposed = 4,
        [Display(Name = "欲しいもの")]
        Wishlist = 5,
    }

    /// <summary>言葉のジャンル・媒体。</summary>
    public enum WordGenre
    {
        [Display(Name = "本")]
        Book = 1,
        [Display(Name = "映画")]
        Movie = 2,
        [Display(Name = "音楽")]
        Music = 3,
        [Display(Name = "ゲーム")]
        Game = 4,
        [Display(Name = "会話")]
        Conversation = 5,
        [Display(Name = "Web")]
        Web = 6,
        [Display(Name = "テレビ")]
        TV = 7,
        [Display(Name = "看板・掲示")]
        Signage = 8,
        [Display(Name = "その他")]
        Other = 99,
    }
}
