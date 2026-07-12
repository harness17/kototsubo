using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Dev.CommonLibrary.Pdf;

/// <summary>
/// MVC の Razor View を文字列として描画する。
/// </summary>
public class RazorViewToStringRenderer
{
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IServiceProvider _serviceProvider;

    public RazorViewToStringRenderer(
        IRazorViewEngine viewEngine,
        ITempDataProvider tempDataProvider,
        IServiceProvider serviceProvider)
    {
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 指定した View を Layout 込みで HTML 文字列に変換する。
    /// </summary>
    public async Task<string> RenderAsync(ControllerContext controllerContext, string viewName, object model)
    {
        var viewResult = _viewEngine.FindView(controllerContext, viewName, isMainPage: true);
        if (!viewResult.Success)
        {
            var searched = string.Join(", ", viewResult.SearchedLocations);
            throw new InvalidOperationException($"View '{viewName}' was not found. Searched: {searched}");
        }

        await using var writer = new StringWriter();
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), controllerContext.ModelState)
        {
            Model = model
        };
        var tempData = new TempDataDictionary(controllerContext.HttpContext, _tempDataProvider);
        var viewContext = new ViewContext(
            controllerContext,
            viewResult.View,
            viewData,
            tempData,
            writer,
            new HtmlHelperOptions());

        viewContext.HttpContext.RequestServices = _serviceProvider;
        await viewResult.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}
