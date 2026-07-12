using System.ComponentModel.DataAnnotations;
using Dev.CommonLibrary.Entity;

namespace Site.Entity
{
    /// <summary>
    /// 言葉エンティティ履歴（履歴テーブル）。
    /// </summary>
    public class WordEntityHistory : WordEntityBase, IEntityHistory
    {
        /// <summary>履歴ID（主キー、自動採番）。</summary>
        [Key]
        public long HistoryId { get; set; }
    }
}
