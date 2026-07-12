namespace Dev.CommonLibrary.Batch
{
    /// <summary>
    /// バッチインターフェース
    /// </summary>
    public interface IBatch
    {
        /// <summary>バッチのメイン処理を実行する。</summary>
        void Exec();
        /// <summary>Exec で発生した例外をハンドリングする。</summary>
        void ExceptionHandler(Exception ex);
    }
}
