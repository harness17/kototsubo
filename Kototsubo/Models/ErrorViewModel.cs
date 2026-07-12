namespace Site.Models
{
    /// <summary>
    /// エラーページ表示用ビューモデル
    /// </summary>
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public int StatusCode { get; set; }
        public string ErrorTitle { get; set; } = "エラーが発生しました";
        public string ErrorMessage { get; set; } = "しばらく時間をおいてから再度お試しください。";
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
