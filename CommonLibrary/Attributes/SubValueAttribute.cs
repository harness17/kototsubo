using System;

namespace Dev.CommonLibrary.Attributes
{
    /// <summary>
    /// Enum補足値属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class SubValueAttribute : Attribute
    {
        public string SubValue { get; }
        public SubValueAttribute(string subValue)
        {
            SubValue = subValue;
        }
    }
}
