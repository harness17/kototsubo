using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Dev.CommonLibrary.Common
{
    /// <summary>
    /// ログ出力クラス
    /// </summary>
    public class Logger
    {
        private static Logger _instance = new Logger();
        private ILogger? _logger;

        private Logger() { }

        /// <summary>シングルトンインスタンスを返す。</summary>
        public static Logger GetLogger() => _instance;

        /// <summary>使用する ILogger を設定する。Program.cs の DI 設定で呼ぶ。</summary>
        public void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>Debug レベルでログを出力する。</summary>
        public void Debug(ILogModel model)
        {
            if (model.Message != null)
                _logger?.LogDebug(model.Message);
        }

        /// <summary>Information レベルでログを出力する。</summary>
        public void Info(ILogModel model)
        {
            if (model.Message != null)
                _logger?.LogInformation(model.Message);
        }

        /// <summary>Warning レベルでログを出力する。</summary>
        public void Warn(ILogModel model)
        {
            if (model.Message != null)
                _logger?.LogWarning(model.Message);
        }

        /// <summary>Warning レベルで例外付きのログを出力する。</summary>
        public void Warn(ILogModel model, Exception ex)
        {
            if (model.Message != null)
                _logger?.LogWarning(ex, model.Message);
        }

        /// <summary>Error レベルでログを出力する。</summary>
        public void Error(ILogModel model)
        {
            if (model.Message != null)
                _logger?.LogError(model.Message);
        }

        /// <summary>Error レベルで例外付きのログを出力する。</summary>
        public void Error(ILogModel model, Exception ex)
        {
            _logger?.LogError(ex, model.Message ?? "");
        }
    }

    /// <summary>
    /// ログ出力用インターフェース
    /// </summary>
    public interface ILogModel
    {
        string? Message { get; }
    }

    /// <summary>
    /// ログ出力用クラス
    /// </summary>
    public class LogModel : ILogModel
    {
        protected const string logFormat = "{0}\t{1}\t{2}";
        protected string FileName { get; private set; }
        protected string Method { get; private set; }
        protected string? Msg { get; set; }

        public LogModel(string msg, [CallerFilePath] string sourceFilePath = "", [CallerMemberName] string method = "")
            : this(sourceFilePath, method)
        {
            this.Msg = msg;
        }

        protected LogModel([CallerFilePath] string sourceFilePath = "", [CallerMemberName] string method = "")
        {
            this.FileName = Path.GetFileName(sourceFilePath);
            this.Method = method;
        }

        public virtual string? Message => string.Format(logFormat, FileName, Method, Msg);
    }
}
