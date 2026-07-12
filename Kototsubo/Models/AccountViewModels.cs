using System.ComponentModel.DataAnnotations;

namespace Site.Models
{
    /// <summary>ログインフォーム用 ViewModel。</summary>
    public class LoginViewModel
    {
        [Required]
        [Display(Name = "電子メール")]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "パスワード")]
        public string Password { get; set; } = "";

        [Display(Name = "このアカウントを記憶する")]
        public bool RememberMe { get; set; }
    }

    /// <summary>新規ユーザー登録フォーム用 ViewModel。</summary>
    public class RegisterViewModel
    {
        [Required]
        [StringLength(100, ErrorMessage = "{0} は {1} 文字以内で入力してください。")]
        [Display(Name = "ユーザー名")]
        public string UserName { get; set; } = "";

        [Required]
        [EmailAddress]
        [Display(Name = "電子メール")]
        public string Email { get; set; } = "";

        [Required]
        [StringLength(100, ErrorMessage = "{0} の長さは {2} 文字以上である必要があります。", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "パスワード")]
        public string Password { get; set; } = "";

        [DataType(DataType.Password)]
        [Display(Name = "パスワードの確認入力")]
        [Compare("Password", ErrorMessage = "パスワードと確認のパスワードが一致しません。")]
        public string ConfirmPassword { get; set; } = "";
    }

    /// <summary>パスワードリセット申請フォーム用 ViewModel。</summary>
    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "電子メール")]
        public string Email { get; set; } = "";
    }

    /// <summary>パスワードリセットフォーム用 ViewModel。</summary>
    public class ResetPasswordViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "電子メール")]
        public string Email { get; set; } = "";

        [Required]
        [StringLength(100, ErrorMessage = "{0} の長さは {2} 文字以上である必要があります。", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "新しいパスワード")]
        public string Password { get; set; } = "";

        [DataType(DataType.Password)]
        [Display(Name = "パスワードの確認入力")]
        [Compare("Password", ErrorMessage = "パスワードと確認のパスワードが一致しません。")]
        public string ConfirmPassword { get; set; } = "";

        [Required]
        public string Code { get; set; } = "";
    }
}
