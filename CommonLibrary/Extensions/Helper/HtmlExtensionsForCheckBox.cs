using System.Linq.Expressions;
using System.Text;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace Dev.CommonLibrary.Extensions.Helper
{
    /// <summary>
    /// チェックボックス表示用の HTML 拡張メソッド。
    /// </summary>
    public static class HtmlExtensionsForCheckBox
    {
        /// <summary>
        /// 選択リストをチェックボックス群として表した HTML 文字列を返す。
        /// </summary>
        public static IHtmlContent CheckBoxForSelectList<TModel, TProperty>(
            this IHtmlHelper<TModel> htmlHelper,
            Expression<Func<TModel, TProperty>> expression,
            IEnumerable<SelectListItem>? listOfValues,
            string clickevent = "",
            string divClass = "",
            object? labelhtmlAttributes = null,
            object? chkhtmlAttributes = null,
            int loopCount = 0)
            where TProperty : List<string>
        {
            var sb = new StringBuilder();
            if (listOfValues == null) return HtmlString.Empty;

            foreach (var item in listOfValues)
            {
                sb.Append(BuildCheckBox(htmlHelper, expression, item, clickevent, divClass, labelhtmlAttributes, chkhtmlAttributes));
                loopCount++;
            }

            return new HtmlString(sb.ToString());
        }

        /// <summary>
        /// 選択リスト項目をチェックボックスとして表した HTML 文字列を返す。
        /// </summary>
        public static IHtmlContent CheckBoxForValue<TModel, TProperty>(
            this IHtmlHelper<TModel> htmlHelper,
            Expression<Func<TModel, TProperty>> expression,
            SelectListItem? values,
            int indexNo,
            string clickevent = "",
            string divClass = "",
            object? labelhtmlAttributes = null,
            object? chkhtmlAttributes = null)
            where TProperty : List<string>
        {
            _ = indexNo;
            return values == null
                ? HtmlString.Empty
                : new HtmlString(BuildCheckBox(htmlHelper, expression, values, clickevent, divClass, labelhtmlAttributes, chkhtmlAttributes));
        }

        private static string BuildCheckBox<TModel, TProperty>(
            IHtmlHelper<TModel> htmlHelper,
            Expression<Func<TModel, TProperty>> expression,
            SelectListItem item,
            string clickEvent,
            string divClass,
            object? labelHtmlAttributes,
            object? checkBoxHtmlAttributes)
            where TProperty : List<string>
        {
            var name = GetExpressionText(htmlHelper, expression);
            var metadata = GetMetadata(htmlHelper, expression);
            var fullName = htmlHelper.ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(name);
            var id = $"{NormalizeId(fullName)}-{item.Value}";

            var checkBox = new TagBuilder("input");
            checkBox.TagRenderMode = TagRenderMode.SelfClosing;
            checkBox.MergeAttribute("id", id);
            checkBox.MergeAttribute("type", "checkbox");
            checkBox.MergeAttribute("name", fullName, replaceExisting: true);
            checkBox.MergeAttribute("value", item.Value);
            checkBox.MergeAttribute("onclick", clickEvent);
            MergeAttributes(checkBox, checkBoxHtmlAttributes);

            if (item.Selected || IsSelected(metadata.Model, item.Value))
            {
                checkBox.MergeAttribute("checked", "checked");
            }

            var hidden = new TagBuilder("input");
            hidden.TagRenderMode = TagRenderMode.SelfClosing;
            hidden.MergeAttribute("type", "hidden");
            hidden.MergeAttribute("name", fullName, replaceExisting: true);

            var label = htmlHelper.Label(id, item.Text, labelHtmlAttributes).ToHtmlString();
            var content = checkBox.ToHtmlString() + hidden.ToHtmlString();

            return $"<div class=\"{divClass}\"> {content} {label} </div>";
        }

        private static bool IsSelected(object? model, string? value)
        {
            return model is IEnumerable<string> selectedValues && selectedValues.Any(x => x == value);
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

        private static void MergeAttributes(TagBuilder builder, object? htmlAttributes)
        {
            if (htmlAttributes == null) return;

            foreach (var attribute in HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes))
            {
                builder.MergeAttribute(attribute.Key.Replace("_", "-"), attribute.Value?.ToString());
            }
        }

        private static string NormalizeId(string value)
        {
            return value.Replace("[", "_").Replace("]", "_").Replace(".", "_");
        }
    }
}
