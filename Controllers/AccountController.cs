using System.Security.Claims;
using ArxReportes.Models;
using ArxReportes.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ArxReportes.Controllers;

public sealed class AccountController(CatalogStore store, IPasswordHasher<UserDefinition> passwordHasher) : Controller
{
    [AllowAnonymous, HttpGet("/acceso")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction(nameof(HomeController.Index), "Home");
        return View(new UserLoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost("/acceso"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(UserLoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        var data = await store.GetAsync(cancellationToken);
        var user = data.Users.FirstOrDefault(x => x.IsActive &&
            string.Equals(x.UserName, model.UserName.Trim(), StringComparison.OrdinalIgnoreCase));
        var verification = user is null
            ? PasswordVerificationResult.Failed
            : passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
        if (user is null || verification == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("", "Usuario o contraseña incorrectos.");
            return View(model);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, model.Password);
            await store.SaveUserAsync(user, cancellationToken);
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, "User")
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : "/");
    }

    [Authorize, HttpPost("/salir"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous, HttpGet("/acceso-denegado")]
    public IActionResult AccessDenied() => View();
}
