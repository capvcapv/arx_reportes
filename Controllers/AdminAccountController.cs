using System.Security.Claims;
using ArxReportes.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArxReportes.Controllers;

[Route("admin")]
public sealed class AdminAccountController(IConfiguration configuration) : Controller
{
    [AllowAnonymous, HttpGet("acceso")]
    public IActionResult Login(string? returnUrl = null) => View(new AdminLoginViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous, HttpPost("acceso"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(AdminLoginViewModel model)
    {
        var configuredPassword = configuration["Admin:Password"];
        if (string.IsNullOrWhiteSpace(configuredPassword))
        {
            ModelState.AddModelError("", "No se ha configurado Admin:Password en el servidor.");
            return View(model);
        }
        if (!string.Equals(model.Password, configuredPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError("", "La clave no es correcta.");
            return View(model);
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "Administrador"),
            new Claim(ClaimTypes.Role, "Admin")
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : "/admin/reportes");
    }

    [Authorize(Roles = "Admin"), HttpPost("salir"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }
}
