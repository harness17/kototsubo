namespace Dev.CommonLibrary.Entity
{
    /// <summary>
    /// エンティティインターフェース
    /// </summary>
    public interface IEntity
    {
        void SetForCreate();
        void SetForUpdate();
        void SetForLogicalDelete();
    }

    /// <summary>
    /// 履歴エンティティインターフェース
    /// </summary>
    public interface IEntityHistory
    {
        long HistoryId { get; set; }
    }
}
