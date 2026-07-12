using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Site.Models;
using System.Diagnostics;

namespace Site.Controllers
{
    /// <summary>
    /// ホームコントローラー。ログイン後のトップ画面を提供する。
    /// </summary>
    [Authorize]
    public class HomeController : Controller
    {
        /// <summary>トップ画面。</summary>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>フォールバック用エラー画面。</summary>
        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
