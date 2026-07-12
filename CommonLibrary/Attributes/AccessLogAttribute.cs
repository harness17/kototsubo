using Dev.CommonLibrary.Common;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Dev.CommonLibrary.Attributes
{
    /// <summary>
    /// アクセスログ出力属性
    /// </summary>
    public class AccessLogAttribute : Attribute, IActionFilter
    {
        private readonly Logger _logger = Logger.GetLogger();

        /// <summary>アクション実行前にアクセスログを出力する。</summary>
        public void OnActionExecuting(ActionExecutingContext context)
        {
            _logger.Info(CreateLogModel(context));
        }

        /// <summary>アクション実行後の処理（現在は何もしない）。</summary>
        public void OnActionExecuted(ActionExecutedContext context) { }

        /// <summary>アクセスログ出力用モデルを生成する。サブクラスでオーバーライドしてログ内容をカスタマイズできる。</summary>
        public virtual ILogModel CreateLogModel(ActionExecutingContext context,
            [CallerFilePath] string sourceFilePath = "",
            [CallerMemberName] string method = "")
        {
            return new AccessLogModel(context, sourceFilePath, method);
        }
    }

    /// <summary>
    /// コントローラー名属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ControllerAttribute : Attribute
    {
        public string Name { get; set; } = "";
    }

    /// <summary>
    /// アクション属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ActionAttribute : Attribute
    {
        public string Name { get; set; } = "";
        public bool ShowLog { get; set; } = true;
    }

    /// <summary>
    /// アクセスログ出力用モデル
    /// </summary>
    public class AccessLogModel : LogModel, ILogModel
    {
        private readonly ActionExecutingContext _context;

        public AccessLogModel(ActionExecutingContext context, string sourceFilePath = "", string method = "")
            : base(sourceFilePath, method)
        {
            _context = context;
        }

        public override string? Message
        {
            get
            {
                var attrAct = _context.ActionDescriptor.EndpointMetadata
                    .OfType<ActionAttribute>().FirstOrDefault();

                if (attrAct != null && attrAct.ShowLog == false)
                    return null;

                var attrCon = _context.ActionDescriptor.EndpointMetadata
                    .OfType<ControllerAttribute>().FirstOrDefault();

                this.Msg = JsonSerializer.Serialize(new
                {
                    Name = _context.HttpContext.User?.Identity?.Name,
                    Controller = _context.RouteData.Values["controller"]?.ToString(),
                    ControllerName = attrCon?.Name,
                    Action = _context.RouteData.Values["action"]?.ToString(),
                    ActionName = attrAct?.Name,
                    IPAddress = _context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = _context.HttpContext.Request.Headers["User-Agent"].ToString(),
                    Method = _context.HttpContext.Request.Method
                });
                return base.Message;
            }
        }
    }
}
