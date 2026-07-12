using System.ComponentModel.DataAnnotations;
using Dev.CommonLibrary.Common;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Site.Common;

namespace Site.Models
{
    /// <summary>
    /// 言葉一覧画面のビューモデル。
    /// </summary>
    public class WordListViewModel
    {
        public List<WordViewModel> Items { get; set; } = new();
        public WordSearchViewModel Search { get; set; } = new();
        public long? ItemId { get; set; }
        public string? ItemTitle { get; set; }
        public CommonListPagerModel Pager { get; set; } = new();
        public CommonListSummaryModel? Summary { get; set; }
    }

    /// <summary>
    /// 言葉一覧の検索条件ビューモデル。
    /// </summary>
    public class WordSearchViewModel
    {
        [Display(Name = "キーワード")]
        public string? Keyword { get; set; }

        [Display(Name = "ジャンル")]
        public WordGenre? Genre { get; set; }
    }

    /// <summary>
    /// 言葉の表示・編集用ビューモデル。
    /// </summary>
    public class WordViewModel
    {
        public long Id { get; set; }

        [Required(ErrorMessage = "言葉は必須です")]
        [StringLength(4000)]
        [Display(Name = "言葉")]
        public string Body { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "出典元タイトル")]
        public string? SourceTitle { get; set; }

        [StringLength(500)]
        [Display(Name = "作者")]
        public string? Author { get; set; }

        [StringLength(500)]
        [Display(Name = "発言者")]
        public string? Speaker { get; set; }

        [StringLength(500)]
        [Display(Name = "場所")]
        public string? Place { get; set; }

        [StringLength(2000)]
        [Display(Name = "コメント")]
        public string? Comment { get; set; }

        [StringLength(1000)]
        [Display(Name = "参考URL")]
        public string? ReferenceUrl { get; set; }

        [Display(Name = "ジャンル")]
        public WordGenre? Genre { get; set; }

        public long? ItemId { get; set; }

        [BindNever]
        public string? UserId { get; set; }

        [Display(Name = "公開する")]
        public bool IsPublic { get; set; }

        [BindNever]
        public string? ItemTitle { get; set; }

        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }
    }
}
