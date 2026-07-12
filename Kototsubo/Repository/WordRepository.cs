using Dev.CommonLibrary.Common;
using Dev.CommonLibrary.Repository;
using Site.Common;
using Site.Entity;

namespace Site.Repository
{
    /// <summary>
    /// 言葉リポジトリの検索条件モデル。
    /// </summary>
    public class WordCondModel : IRepositoryCondModel
    {
        public CommonListPagerModel Pager { get; set; } = new();

        /// <summary>本文・出典・作者・発言者・コメントのキーワード検索。</summary>
        public string? Keyword { get; set; }

        /// <summary>ジャンルフィルター。</summary>
        public WordGenre? Genre { get; set; }

        /// <summary>所蔵品フィルター。</summary>
        public long? ItemId { get; set; }
    }

    /// <summary>
    /// 言葉リポジトリ（履歴あり）。
    /// </summary>
    public class WordRepository : RepositoryBase<WordEntity, WordEntityHistory, WordCondModel>
    {
        public WordRepository(DBContext context) : base(context) { }

        /// <summary>検索条件に応じたクエリを構築する。</summary>
        public override IQueryable<WordEntity> GetBaseQuery(WordCondModel? cond = null, bool includeDelete = false)
        {
            IQueryable<WordEntity> query = dbSet.AsQueryable();

            if (!includeDelete)
                query = query.Where(word => !word.DelFlag);

            if (cond == null)
                return query.OrderByDescending(word => word.UpdateDate);

            if (!string.IsNullOrWhiteSpace(cond.Keyword))
            {
                var keyword = cond.Keyword.Trim();
                query = query.Where(word =>
                    word.Body.Contains(keyword) ||
                    (word.SourceTitle != null && word.SourceTitle.Contains(keyword)) ||
                    (word.Author != null && word.Author.Contains(keyword)) ||
                    (word.Speaker != null && word.Speaker.Contains(keyword)) ||
                    (word.Comment != null && word.Comment.Contains(keyword)));
            }

            if (cond.Genre.HasValue)
                query = query.Where(word => word.Genre == cond.Genre.Value);

            if (cond.ItemId.HasValue)
                query = query.Where(word => word.ItemId == cond.ItemId.Value);

            return cond.Pager.sort?.ToLower() switch
            {
                "body" => cond.Pager.sortdir == "DESC"
                    ? query.OrderByDescending(word => word.Body)
                    : query.OrderBy(word => word.Body),
                "sourcetitle" => cond.Pager.sortdir == "DESC"
                    ? query.OrderByDescending(word => word.SourceTitle)
                    : query.OrderBy(word => word.SourceTitle),
                "author" => cond.Pager.sortdir == "DESC"
                    ? query.OrderByDescending(word => word.Author)
                    : query.OrderBy(word => word.Author),
                "createdate" => cond.Pager.sortdir == "DESC"
                    ? query.OrderByDescending(word => word.CreateDate)
                    : query.OrderBy(word => word.CreateDate),
                _ => query.OrderByDescending(word => word.UpdateDate),
            };
        }
    }
}
