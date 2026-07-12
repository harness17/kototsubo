namespace Site.Models
{
    /// <summary>
    /// 一覧画面で共通利用するページャーの表示情報。
    /// </summary>
    public class PagerViewModel
    {
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public string Action { get; set; } = "Index";
        public string AriaLabel { get; set; } = "ページング";
        public Dictionary<string, string> RouteValues { get; set; } = new();
        public int WindowRadius { get; set; } = 2;
    }
}
