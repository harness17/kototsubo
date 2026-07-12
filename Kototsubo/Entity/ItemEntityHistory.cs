using System.ComponentModel.DataAnnotations;
using Dev.CommonLibrary.Entity;

namespace Site.Entity
{
    /// <summary>
    /// 所蔵品エンティティ履歴（履歴テーブル）
    /// </summary>
    public class ItemEntityHistory : ItemEntityBase, IEntityHistory
    {
        /// <summary>履歴ID（主キー、自動採番）</summary>
        [Key]
        public long HistoryId { get; set; }
    }
}
