using Dev.CommonLibrary.Common;
using Dev.CommonLibrary.Repository;
using Microsoft.EntityFrameworkCore;
using Site.Common;
using Site.Entity;

namespace Site.Repository
{
    /// <summary>
    /// 所蔵品リポジトリの検索条件モデル。
    /// </summary>
    public class ItemCondModel : IRepositoryCondModel
    {
        public CommonListPagerModel Pager { get; set; } = new();

        /// <summary>キーワード検索（タイトル・クリエイター・出版社）</summary>
        public string? Keyword { get; set; }

        /// <summary>メディア種別フィルター</summary>
        public MediaType? MediaType { get; set; }

        /// <summary>デジタル版・物理版フィルター（未指定は両方）</summary>
        public bool? IsDigital { get; set; }

        /// <summary>所有ステータスフィルター</summary>
        public OwnershipStatus? OwnershipStatus { get; set; }

        /// <summary>出版社フィルター（部分一致）</summary>
        public string? Publisher { get; set; }

        /// <summary>発売日の開始日（含む）</summary>
        public DateTime? ReleaseDateFrom { get; set; }

        /// <summary>発売日の終了日（含む）</summary>
        public DateTime? ReleaseDateTo { get; set; }
    }

    /// <summary>
    /// 所蔵品リポジトリ（履歴あり）。
    /// </summary>
    public class ItemRepository : RepositoryBase<ItemEntity, ItemEntityHistory, ItemCondModel>
    {
        private const int MaxWordAssociationSearchResults = 20;

        public ItemRepository(DBContext context) : base(context) { }

        /// <summary>言葉登録・編集画面から関連付ける所蔵品を検索する。</summary>
        public IQueryable<ItemEntity> SearchForWordAssociation(string keyword)
        {
            var query = GetBaseQuery();
            if (string.IsNullOrWhiteSpace(keyword))
                return query.Where(_ => false).Take(0);

            var kw = keyword.Trim();
            return query
                .Where(x => x.Title.Contains(kw) ||
                            (x.Creator != null && x.Creator.Contains(kw)))
                .OrderBy(x => x.Title)
                .ThenBy(x => x.Creator)
                .Take(MaxWordAssociationSearchResults);
        }

        /// <summary>検索条件に応じたクエリを構築する。</summary>
        public override IQueryable<ItemEntity> GetBaseQuery(ItemCondModel? cond = null, bool includeDelete = false)
        {
            IQueryable<ItemEntity> query = dbSet.AsQueryable();

            if (!includeDelete)
                query = query.Where(x => !x.DelFlag);

            if (cond == null) return query.OrderByDescending(x => x.CreateDate);

            if (!string.IsNullOrWhiteSpace(cond.Keyword))
            {
                var kw = cond.Keyword.Trim();
                query = query.Where(x =>
                    x.Title.Contains(kw) ||
                    (x.Creator != null && x.Creator.Contains(kw)) ||
                    (x.Publisher != null && x.Publisher.Contains(kw)) ||
                    (x.ISBN != null && x.ISBN.Contains(kw)) ||
                    (x.ASIN != null && x.ASIN.Contains(kw)) ||
                    (x.JANCode != null && x.JANCode.Contains(kw)));
            }

            if (cond.MediaType.HasValue)
                query = query.Where(x => x.MediaType == cond.MediaType.Value);

            if (cond.IsDigital.HasValue)
                query = query.Where(x => x.IsDigital == cond.IsDigital.Value);

            if (cond.OwnershipStatus.HasValue)
                query = query.Where(x => x.OwnershipStatus == cond.OwnershipStatus.Value);

            if (!string.IsNullOrWhiteSpace(cond.Publisher))
            {
                var publisher = cond.Publisher.Trim();
                query = query.Where(x => x.Publisher != null && x.Publisher.Contains(publisher));
            }

            if (cond.ReleaseDateFrom.HasValue)
            {
                var releaseDateFrom = cond.ReleaseDateFrom.Value.Date;
                query = query.Where(x => x.ReleaseDate >= releaseDateFrom);
            }

            if (cond.ReleaseDateTo.HasValue)
            {
                var releaseDateTo = cond.ReleaseDateTo.Value.Date;
                if (releaseDateTo == DateTime.MaxValue.Date)
                    query = query.Where(x => x.ReleaseDate <= releaseDateTo);
                else
                {
                    var releaseDateToExclusive = releaseDateTo.AddDays(1);
                    query = query.Where(x => x.ReleaseDate < releaseDateToExclusive);
                }
            }

            // ソート
            query = cond.Pager.sort?.ToLower() switch
            {
                "title" => cond.Pager.sortdir == "DESC"
                    ? query.OrderByDescending(x => x.Title)
                    : query.OrderBy(x => x.Title),
                "mediatype" => cond.Pager.sortdir == "DESC"
                    ? query.OrderByDescending(x => x.MediaType)
                    : query.OrderBy(x => x.MediaType),
                "creator" => cond.Pager.sortdir == "DESC"
                    ? query.OrderByDescending(x => x.Creator)
                    : query.OrderBy(x => x.Creator),
                "ownershipstatus" => cond.Pager.sortdir == "DESC"
                    ? query.OrderByDescending(x => x.OwnershipStatus)
                    : query.OrderBy(x => x.OwnershipStatus),
                "acquisitiondate" => cond.Pager.sortdir == "DESC"
                    ? query.OrderByDescending(x => x.AcquisitionDate)
                    : query.OrderBy(x => x.AcquisitionDate),
                "createdate" => cond.Pager.sortdir == "DESC"
                    ? query.OrderByDescending(x => x.CreateDate)
                    : query.OrderBy(x => x.CreateDate),
                _ => query.OrderByDescending(x => x.CreateDate),
            };

            return query;
        }
    }
}
