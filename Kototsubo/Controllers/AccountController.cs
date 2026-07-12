using Dev.CommonLibrary.Common;
using Dev.CommonLibrary.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Site.Common;
using Site.Models;

namespace Site.Controllers
{
    /// <summary>
    /// 認証コントローラー（ログイン・登録・パスワード管理）。
    /// </summary>
    [Authorize]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly bool _allowPublicRegistration;
        private readonly Logger _logger = Logger.GetLogger();

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _allowPublicRegistration = configuration.GetValue<bool>("Security:AllowPublicRegistration");
        }

        /// <summary>ログイン画面表示。</summary>
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        /// <summary>ログイン処理。</summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            var result = user == null
                ? Microsoft.AspNetCore.Identity.SignInResult.Failed
                : await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user!, model.RememberMe);
                return RedirectToLocal(returnUrl);
            }

            if (result.IsLockedOut)
            {
                _logger.Warn(new LogModel($"アカウントロック: Email={model.Email}"));
                return View("Lockout");
            }

            ModelState.AddModelError("", "無効なログイン試行です。");
            return View(model);
        }

        /// <summary>新規登録画面表示。</summary>
        [AllowAnonymous]
        public IActionResult Register()
        {
            if (!_allowPublicRegistration) return NotFound();
            return View();
        }

        /// <summary>新規登録処理。</summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!_allowPublicRegistration) return NotFound();

            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = model.UserName, Email = model.Email };
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, ApplicationRoleType.Member.ToString());
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index", "Home");
                }
                AddErrors(result);
            }
            return View(model);
        }

        /// <summary>ログアウト処理。</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogOff()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        /// <summary>IdentityResult のエラーを ModelState に追加する。</summary>
        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
        }

        /// <summary>ローカル URL はリダイレクト、それ以外はホームへ（オープンリダイレクト防止）。</summary>
        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }
    }
}
