using Dev.CommonLibrary.Attributes;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Dev.CommonLibrary.Common
{
    /// <summary>
    /// Enum関連Utility
    /// </summary>
    public static class EnumUtility
    {
        /// <summary>文字列値に対応する Enum の DisplayAttribute.Name を返す。属性がない場合は元の値を返す。</summary>
        public static string GetEnumDisplay<T>(string value) where T : struct
        {
            Type type = typeof(T);
            var name = GetEnumName(type, value);
            if (name == null) return string.Empty;
            var fieldInfo = type.GetField(name);
            if (fieldInfo == null) return string.Empty;
            var attrs = fieldInfo.GetCustomAttributes(typeof(DisplayAttribute), false) as DisplayAttribute[];
            if (attrs == null || attrs.Length == 0) return value;
            var attr = attrs[0];
            if (attr.ResourceType != null) return attr.GetName() ?? value;
            return attr.Name ?? value;
        }

        /// <summary>文字列値に対応する Enum の SubValueAttribute 値を返す。属性がない場合は空文字を返す。</summary>
        public static string GetEnumSubValue<T>(string value) where T : struct
        {
            Type type = typeof(T);
            var name = GetEnumName(type, value);
            if (name == null) return string.Empty;
            var fieldInfo = type.GetField(name);
            if (fieldInfo == null) return string.Empty;
            var attrs = fieldInfo.GetCustomAttributes(typeof(SubValueAttribute), false) as SubValueAttribute[];
            if (attrs == null || attrs.Length == 0) return string.Empty;
            return attrs[0].SubValue;
        }

        /// <summary>文字列値に対応する Enum の DisplayAttribute.Order を返す。属性がない場合は 0 を返す。</summary>
        public static int GetEnumDisplayOrder<T>(string value) where T : struct
        {
            Type type = typeof(T);
            var name = GetEnumName(type, value);
            if (name == null) return 0;
            var fieldInfo = type.GetField(name);
            if (fieldInfo == null) return 0;
            var attrs = fieldInfo.GetCustomAttributes(typeof(DisplayAttribute), false) as DisplayAttribute[];
            if (attrs == null || attrs.Length == 0) return 0;
            return attrs[0].Order;
        }

        /// <summary>文字列値に対応する Enum の DescriptionAttribute 値を返す。属性がない場合は Enum 名を返す。</summary>
        public static string GetEnumDescription<T>(string value) where T : struct
        {
            Type type = typeof(T);
            var name = GetEnumName(type, value);
            if (name == null) return string.Empty;
            var field = type.GetField(name);
            if (field == null) return string.Empty;
            var customAttribute = field.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return customAttribute.Length > 0 ? ((DescriptionAttribute)customAttribute[0]).Description : name;
        }

        private static string? GetEnumName(Type type, string value)
        {
            return Enum.GetNames(type)
                .Where(f => f.Equals(value, StringComparison.CurrentCultureIgnoreCase))
                .FirstOrDefault();
        }

        /// <summary>指定した Type・フィールド名の DescriptionAttribute 値を返す。属性がない場合は name を返す。</summary>
        public static string GetDescription(Type T, string name)
        {
            var attributes = (DescriptionAttribute[])T.GetField(name)!
                .GetCustomAttributes(typeof(DescriptionAttribute), false);
            var description = attributes.Select(n => n.Description).FirstOrDefault();
            return string.IsNullOrEmpty(description) ? name : description!;
        }
    }
}
