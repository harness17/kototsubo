using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dev.CommonLibrary.Extensions
{
    /// <summary>object に対する汎用拡張メソッド群。</summary>
    public static class ObjectExtensions
    {
        /// <summary>AutoMapper を使ってオブジェクトのシャローコピーを返す。</summary>
        public static T? Clone<T>(this T source) where T : class
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<T, T>(), NullLoggerFactory.Instance);
            return config.CreateMapper().Map<T, T>(source);
        }

        /// <summary>値を文字列に変換する。null の場合は null を返す。format 指定時は IFormattable として書式化する。</summary>
        public static string? ToStringOrDefault(this object? value, string? format = null)
        {
            if (value == null) return null;
            if (format != null && value is IFormattable f) return f.ToString(format, null);
            return value.ToString();
        }

        /// <summary>値を文字列に変換する。null の場合は空文字を返す。</summary>
        public static string ToStringOrEmpty(this object? value, string? format = null)
        {
            return ToStringOrDefault(value, format) ?? string.Empty;
        }
    }
}
