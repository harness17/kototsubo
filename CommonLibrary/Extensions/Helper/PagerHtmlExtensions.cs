using System.Globalization;
using System.Text;
using Dev.CommonLibrary.Common;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Dev.CommonLibrary.Extensions.Helper
{
    /// <summary>
    /// ASP.NET Core 向けページャー HTML 拡張メソッド。
    /// </summary>
    public static class PagerHtmlExtensions
    {
        /// <summary>
        /// ページャー生成モード。
        /// </summary>
        [Flags]
        public enum PagerModes
        {
            /// <summary>先頭・最後リンクを表示する。</summary>
            FirstLast = 1,

            /// <summary>前へ・次へリンクを表示する。</summary>
            NextPrevious = 2,

            /// <summary>ページ番号リンクを表示する。</summary>
            Numeric = 4
        }

        /// <summary>
        /// ページャー用の ul/li HTML を生成する。
        /// </summary>
        public static IHtmlContent PagerList(
            this IHtmlHelper htmlHelper,
            int currentPage,
            int totalPages,
            Func<int, string> pageUrlFactory,
            PagerModes mode = PagerModes.NextPrevious | PagerModes.Numeric,
            PagerTextModel? pagerText = null,
            int numericLinksCount = 5,
            string? ulClass = null)
        {
            _ = htmlHelper;
            pagerText ??= new PagerTextModel();
            totalPages = Math.Max(totalPages, 1);
            currentPage = Math.Clamp(currentPage, 1, totalPages);

            var zeroBasedPage = currentPage - 1;
            var lastPage = totalPages - 1;
            var ul = new TagBuilder("ul");
            if (!string.IsNullOrWhiteSpace(ulClass)) ul.AddCssClass(ulClass);

            var items = new List<TagBuilder>();

            if (ModeEnabled(mode, PagerModes.FirstLast))
            {
                items.Add(CreatePageItem(pageUrlFactory, 0, pagerText.firstText, zeroBasedPage == 0));
            }

            if (ModeEnabled(mode, PagerModes.NextPrevious))
            {
                var previous = zeroBasedPage == 0 ? 0 : zeroBasedPage - 1;
                items.Add(CreatePageItem(pageUrlFactory, previous, pagerText.previousText, zeroBasedPage == 0));
            }

            if (ModeEnabled(mode, PagerModes.Numeric) && totalPages > 1)
            {
                var last = zeroBasedPage + (numericLinksCount / 2);
                var first = last - numericLinksCount + 1;
                if (last > lastPage)
                {
                    first -= last - lastPage;
                    last = lastPage;
                }

                if (first < 0)
                {
                    last = Math.Min(last + (0 - first), lastPage);
                    first = 0;
                }

                for (var i = first; i <= last; i++)
                {
                    items.Add(CreatePageItem(pageUrlFactory, i, (i + 1).ToString(CultureInfo.InvariantCulture), disabled: false, active: i == zeroBasedPage));
                }
            }

            if (ModeEnabled(mode, PagerModes.NextPrevious))
            {
                var next = zeroBasedPage == lastPage ? lastPage : zeroBasedPage + 1;
                items.Add(CreatePageItem(pageUrlFactory, next, pagerText.nextText, zeroBasedPage == lastPage));
            }

            if (ModeEnabled(mode, PagerModes.FirstLast))
            {
                items.Add(CreatePageItem(pageUrlFactory, lastPage, pagerText.lastText, zeroBasedPage == lastPage));
            }

            ul.InnerHtml.AppendHtml(string.Join("", items.Select(x => x.ToHtmlString())));
            return ul;
        }

        private static TagBuilder CreatePageItem(
            Func<int, string> pageUrlFactory,
            int zeroBasedPage,
            string text,
            bool disabled,
            bool active = false)
        {
            var li = new TagBuilder("li");
            li.AddCssClass("page-item");
            if (disabled) li.AddCssClass("disabled");
            if (active) li.AddCssClass("active");

            TagBuilder link;
            if (active)
            {
                link = new TagBuilder("span");
                link.MergeAttribute("aria-current", "page");
            }
            else
            {
                link = new TagBuilder("a");
                link.MergeAttribute("href", pageUrlFactory(zeroBasedPage + 1));
            }

            link.AddCssClass("page-link");
            if (disabled)
            {
                link.MergeAttribute("tabindex", "-1");
                link.MergeAttribute("aria-disabled", "true");
            }

            link.InnerHtml.Append(text);
            li.InnerHtml.AppendHtml(link);
            return li;
        }

        private static bool ModeEnabled(PagerModes mode, PagerModes modeCheck)
        {
            return (mode & modeCheck) == modeCheck;
        }
    }
}
