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
    /// ラジオボタン表示用の HTML 拡張メソッド。
    /// </summary>
    public static class HtmlExtensionsForRadioButton
    {
        /// <summary>
        /// Enum 型をラジオボタン群として表した HTML 文字列を返す。
        /// </summary>
        public static IHtmlContent RadioButtonForEnum<TModel, TProperty>(
            this IHtmlHelper<TModel> htmlHelper,
            Expression<Func<TModel, TProperty>> expression,
            string prefix = "",
            string clickevent = "",
            bool orderDecFlag = false)
        {
            var metadata = GetMetadata(htmlHelper, expression);
            var modelType = Nullable.GetUnderlyingType(metadata.ModelType) ?? metadata.ModelType;
            if (!modelType.IsEnum) return HtmlString.Empty;

            var expressionName = GetExpressionText(htmlHelper, expression);
            var fullName = htmlHelper.ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(expressionName);
            var idFullName = NormalizeId(fullName);
            var names = Enum.GetNames(modelType);
            if (orderDecFlag) names = names.Reverse().ToArray();

            var sb = new StringBuilder();
            foreach (var name in names)
            {
                var id = $"{idFullName}_{name}";
                if (!string.IsNullOrEmpty(prefix)) id = $"{prefix}_{id}";

                var htmlAttributes = new Dictionary<string, object?>
                {
                    ["id"] = id,
                    ["onclick"] = clickevent
                };

                if (metadata.Model?.ToString() == name)
                {
                    htmlAttributes["checked"] = "checked";
                }

                var radio = htmlHelper.RadioButtonFor(expression, name, htmlAttributes).ToHtmlString();
                var description = WebUtility.HtmlEncode(EnumUtility.GetDescription(modelType, name));
                var label = $"<label for=\"{id}\">{description}</label>";
                sb.Append($"<div class=\"radio-inline\"> {radio} {label} </div>");
            }

            return new HtmlString(sb.ToString());
        }

        /// <summary>
        /// 選択リストをラジオボタン群として表した HTML 文字列を返す。
        /// </summary>
        public static IHtmlContent RadioButtonForSelectList<TModel, TProperty>(
            this IHtmlHelper<TModel> htmlHelper,
            Expression<Func<TModel, TProperty>> expression,
            IEnumerable<SelectListItem>? listOfValues,
            string clickevent = "",
            string divClass = "",
            object? labelhtmlAttributes = null,
            IDictionary<string, object?>? buttonhtmlAttributes = null)
        {
            if (listOfValues == null) return HtmlString.Empty;

            var name = GetExpressionText(htmlHelper, expression);
            var fullName = htmlHelper.ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(name);
            var idFullName = NormalizeId(fullName);
            var metadata = GetMetadata(htmlHelper, expression);
            var sb = new StringBuilder();

            foreach (var item in listOfValues)
            {
                var id = $"{idFullName}_{item.Value}";
                var attributes = buttonhtmlAttributes == null
                    ? new Dictionary<string, object?>()
                    : new Dictionary<string, object?>(buttonhtmlAttributes);

                attributes["id"] = id;
                if (!attributes.ContainsKey("onclick")) attributes["onclick"] = clickevent;
                attributes.Remove("checked");

                if ((metadata.Model != null && $"{idFullName}_{metadata.Model}" == id) || (metadata.Model == null && item.Selected))
                {
                    attributes["checked"] = "checked";
                }

                var radio = htmlHelper.RadioButtonFor(expression, item.Value, attributes).ToHtmlString();
                var label = htmlHelper.Label(id, item.Text, labelhtmlAttributes).ToHtmlString();
                sb.Append($"<div class=\"{divClass}\"> {radio} {label} </div>");
            }

            return new HtmlString(sb.ToString());
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

        private static string NormalizeId(string value)
        {
            return value.Replace("[", "_").Replace("]", "_").Replace(".", "_");
        }
    }
}
