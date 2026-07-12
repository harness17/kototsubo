namespace Dev.CommonLibrary.Extensions
{
    /// <summary>IEnumerable に対する汎用拡張メソッド群。</summary>
    public static class IEnumerableExtension
    {
        /// <summary>シーケンスが null または空の場合に true を返す。</summary>
        public static bool IsNullOrEmpty<T>(this IEnumerable<T>? source)
        {
            return source == null || !source.Any();
        }
    }
}
