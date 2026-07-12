using Dev.CommonLibrary.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Dev.CommonLibrary.Extensions
{
    /// <summary>Enum 値に付加された DisplayAttribute / SubValueAttribute を取得する拡張メソッド群。</summary>
    public static class EnumExtensions
    {
        /// <summary>DisplayAttribute の Name 値を返す。属性がない場合は Enum 名を返す。</summary>
        public static string? DisplayName(this Enum value)
        {
            var type = value.GetType();
            var field = type.GetField(value.ToString());
            if (field == null) return value.ToString();
            var attr = field.GetCustomAttributes(typeof(DisplayAttribute), false) as DisplayAttribute[];
            if (attr == null || attr.Length == 0) return value.ToString();
            return attr[0].ResourceType != null ? attr[0].GetName() : attr[0].Name;
        }

        /// <summary>DisplayAttribute の Description 値を返す。属性がない場合は null を返す。</summary>
        public static string? DisplayDescription(this Enum value)
        {
            var type = value.GetType();
            var field = type.GetField(value.ToString());
            if (field == null) return null;
            var attr = field.GetCustomAttributes(typeof(DisplayAttribute), false) as DisplayAttribute[];
            if (attr == null || attr.Length == 0) return null;
            return attr[0].Description;
        }

        /// <summary>SubValueAttribute に設定されたサブ値を指定型 T に変換して返す。属性がない場合は default を返す。</summary>
        public static T? ToSubValue<T>(this Enum value)
        {
            var type = value.GetType();
            var field = type.GetField(value.ToString());
            if (field == null) return default;
            var attrs = field.GetCustomAttributes(typeof(SubValueAttribute), false) as SubValueAttribute[];
            if (attrs == null || attrs.Length == 0) return default;
            return (T)Convert.ChangeType(attrs[0].SubValue, typeof(T));
        }
    }
}
