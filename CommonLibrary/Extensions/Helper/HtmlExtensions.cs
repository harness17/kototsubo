using System.Linq.Expressions;
using System.Net;
using System.Text;
using Dev.CommonLibrary.Common;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace Dev.CommonLibrary.Extensions.Helper
{
    /// <summary>
    /// Razor View で利用する HTML 拡張メソッド。
    /// </summary>
    public static partial class HtmlExtensions
    {
        /// <summary>
        /// テキストを HTML エンコードし、改行コードを br タグに変換して表示する。
        /// </summary>
        public static IHtmlContent FormatNewLines(this IHtmlHelper helper, string? text)
        {
            var encodedText = WebUtility.HtmlEncode(text ?? string.Empty)
                .Replace("\r\n", "<br />")
                .Replace("\r", "<br />")
                .Replace("\n", "<br />");

            return new HtmlString(encodedText);
        }

        /// <summary>
        /// Enum 型を DisplayFor 風の HTML 文字列として返す。
        /// </summary>
        public static IHtmlContent DisplayForEnum<TModel, TProperty>(
            this IHtmlHelper<TModel> htmlHelper,
            Expression<Func<TModel, TProperty>> expression)
        {
            var metadata = GetMetadata(htmlHelper, expression);
            var modelType = Nullable.GetUnderlyingType(metadata.ModelType) ?? metadata.ModelType;
            if (!modelType.IsEnum) return HtmlString.Empty;

            var selectedName = metadata.Model?.ToString();
            var sb = new StringBuilder();

            foreach (var name in Enum.GetNames(modelType))
            {
                var label = string.Empty;
                if (selectedName == name)
                {
                    var description = WebUtility.HtmlEncode(EnumUtility.GetDescription(modelType, name));
                    label = $"<label>{description}</label>";
                }

                sb.Append(CultureInvariant($"<div class=\"radio-inline\"> {label} </div>"));
            }

            return new HtmlString(sb.ToString());
        }

        /// <summary>
        /// サブモデルのフルプロパティ名を保持しつつパーシャルを出力する。
        /// </summary>
        public static IHtmlContent PartialFor<TModel, TProperty>(
            this IHtmlHelper<TModel> helper,
            Expression<Func<TModel, TProperty>> expression,
            string partialViewName)
        {
            var name = GetExpressionText(helper, expression);
            var metadata = GetMetadata(helper, expression);
            var viewData = new ViewDataDictionary(helper.ViewData)
            {
                Model = metadata.Model
            };
            viewData.TemplateInfo.HtmlFieldPrefix = JoinPrefix(helper.ViewData.TemplateInfo.HtmlFieldPrefix, name);

            return helper.PartialAsync(partialViewName, metadata.Model, viewData).GetAwaiter().GetResult();
        }

        /// <summary>
        /// サブモデルのプロパティ名に prefix を付けてパーシャルを出力する。
        /// </summary>
        public static IHtmlContent PartialFor<TModel>(
            this IHtmlHelper<TModel> helper,
            string prefix,
            string partialViewName)
        {
            var viewData = new ViewDataDictionary(helper.ViewData)
            {
                Model = null
            };
            viewData.TemplateInfo.HtmlFieldPrefix = JoinPrefix(helper.ViewData.TemplateInfo.HtmlFieldPrefix, prefix);

            return helper.PartialAsync(partialViewName, null, viewData).GetAwaiter().GetResult();
        }

        private static ModelExplorer GetMetadata<TModel, TProperty>(
            IHtmlHelper<TModel> htmlHelper,
            Expression<Func<TModel, TProperty>> expression)
        {
            return GetModelExpressionProvider(htmlHelper).CreateModelExpression(htmlHelper.ViewData, expression).ModelExplorer;
        }

        private static string GetExpressionText<TModel, TProperty>(
            IHtmlHelper<TModel> htmlHelper,
            Expression<Func<TModel, TProperty>> expression)
        {
            return GetModelExpressionProvider(htmlHelper).GetExpressionText(expression);
        }

        private static ModelExpressionProvider GetModelExpressionProvider(IHtmlHelper htmlHelper)
        {
            return htmlHelper.ViewContext.HttpContext.RequestServices.GetRequiredService<ModelExpressionProvider>();
        }

        private static string JoinPrefix(string currentPrefix, string additionalPrefix)
        {
            return string.IsNullOrEmpty(currentPrefix) ? additionalPrefix : $"{currentPrefix}.{additionalPrefix}";
        }

        private static string CultureInvariant(FormattableString value)
        {
            return FormattableString.Invariant(value);
        }
    }
}
