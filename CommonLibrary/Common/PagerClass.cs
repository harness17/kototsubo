namespace Dev.CommonLibrary.Common
{
    /// <summary>
    /// ページング条件モデル
    /// </summary>
    public class CommonListPagerModel
    {
        public int page { get; set; }
        public string sort { get; set; }
        public string sortdir { get; set; }
        public int recoedNumber { get; set; }
        public PagerTextModel PagerTextModel { get; set; } = new();

        public CommonListPagerModel(int page = 1, string sort = "", string sortdir = "ASC", int recoedNumber = 10)
        {
            this.page = page;
            this.sort = sort;
            this.sortdir = sortdir;
            this.recoedNumber = recoedNumber;
        }
    }

    /// <summary>
    /// 検索結果サマリーモデル
    /// </summary>
    public class CommonListSummaryModel
    {
        public int CurrentPage { get; set; }
        public int TotalRecords { get; set; }
        public int FirstRecord { get; set; }
        public int EndRecord { get; set; }
        public string Summary { get; set; }

        public CommonListSummaryModel(int currentPage, int totalRecords, int firstRecord, int endRecord, string summary)
        {
            CurrentPage = currentPage;
            TotalRecords = totalRecords;
            FirstRecord = firstRecord;
            EndRecord = endRecord;
            Summary = summary;
        }
    }

    /// <summary>
    /// ページャーリンクに表示する文言を保持するモデル。
    /// </summary>
    public class PagerTextModel
    {
        public string firstText { get; set; } = "最初";
        public string previousText { get; set; } = "前へ";
        public string nextText { get; set; } = "次へ";
        public string lastText { get; set; } = "最後";
    }
}
