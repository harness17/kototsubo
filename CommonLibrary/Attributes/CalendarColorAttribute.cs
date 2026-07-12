namespace Dev.CommonLibrary.Attributes;

/// <summary>
/// カレンダー表示色（背景・枠・文字）を enum 値に持たせるための属性。
/// カレンダー・統計など複数画面で同じ色定義を参照するための一元化用。
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class CalendarColorAttribute : Attribute
{
    public CalendarColorAttribute(string backgroundColor, string borderColor, string textColor)
    {
        BackgroundColor = backgroundColor;
        BorderColor = borderColor;
        TextColor = textColor;
    }

    public string BackgroundColor { get; }

    public string BorderColor { get; }

    public string TextColor { get; }
}
