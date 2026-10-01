using ArxReportes.Models;
using ArxReportes.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ArxReportes.Controllers;

[Authorize(Roles = "Admin"), Route("admin/usuarios")]
public sealed class UsersController(CatalogStore store, IPasswordHasher<UserDefinition> passwordHasher) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var data = await store.GetAsync(cancellationToken);
        return View(data.Users.OrderBy(x => x.DisplayName).ThenBy(x => x.UserName).ToList());
    }

    [HttpGet("nuevo")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View("Edit", await BuildViewModelAsync(new UserEditViewModel { IsActive = true }, cancellationToken));

    [HttpPost("nuevo"), ValidateAntiForgeryToken]
    public Task<IActionResult> Create(UserEditViewModel model, CancellationToken cancellationToken) => Save(model, true, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var user = (await store.GetAsync(cancellationToken)).Users.FirstOrDefault(x => x.Id == id);
        if (user is null) return NotFound();
        var model = new UserEditViewModel
        {
            Id = user.Id,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            IsActive = user.IsActive,
            AllowedReportIds = [.. user.AllowedReportIds],
            AllowedKpiIds = [.. user.AllowedKpiIds],
            AllowedChartIds = [.. user.AllowedChartIds]
        };
        return View(await BuildViewModelAsync(model, cancellationToken));
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(Guid id, UserEditViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        return Save(model, false, cancellationToken);
    }

    [HttpPost("{id:guid}/eliminar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await store.DeleteUserAsync(id, cancellationToken);
        TempData["Success"] = "Usuario eliminado.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> Save(UserEditViewModel model, bool isNew, CancellationToken cancellationToken)
    {
        model.UserName = model.UserName.Trim();
        model.DisplayName = model.DisplayName.Trim();
        var data = await store.GetAsync(cancellationToken);
        var existing = data.Users.FirstOrDefault(x => x.Id == model.Id);

        if (data.Users.Any(x => x.Id != model.Id && string.Equals(x.UserName, model.UserName, StringComparison.OrdinalIgnoreCase)))
            ModelState.AddModelError(nameof(model.UserName), "Ya existe un usuario con este nombre.");
        if (isNew && string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "La contraseña es obligatoria para un usuario nuevo.");
        if (!isNew && existing is null) return NotFound();

        var validReportIds = data.Reports.Select(x => x.Id).ToHashSet();
        var validKpiIds = data.Kpis.Select(x => x.Id).ToHashSet();
        var validChartIds = data.Charts.Select(x => x.Id).ToHashSet();
        model.AllowedReportIds = model.AllowedReportIds.Distinct().Where(validReportIds.Contains).ToList();
        model.AllowedKpiIds = model.AllowedKpiIds.Distinct().Where(validKpiIds.Contains).ToList();
        model.AllowedChartIds = model.AllowedChartIds.Distinct().Where(validChartIds.Contains).ToList();
        if (!ModelState.IsValid)
            return View("Edit", await BuildViewModelAsync(model, cancellationToken));

        var user = existing ?? new UserDefinition { Id = Guid.NewGuid() };
        user.UserName = model.UserName;
        user.DisplayName = model.DisplayName;
        user.IsActive = model.IsActive;
        user.AllowedReportIds = [.. model.AllowedReportIds];
        user.AllowedKpiIds = [.. model.AllowedKpiIds];
        user.AllowedChartIds = [.. model.AllowedChartIds];
        if (!string.IsNullOrWhiteSpace(model.Password))
            user.PasswordHash = passwordHasher.HashPassword(user, model.Password);

        await store.SaveUserAsync(user, cancellationToken);
        TempData["Success"] = isNew ? "Usuario creado." : "Usuario actualizado.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<UserEditViewModel> BuildViewModelAsync(UserEditViewModel model, CancellationToken cancellationToken)
    {
        var data = await store.GetAsync(cancellationToken);
        model.Reports = data.Reports
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .Select(x => new ReportPermissionItem(x.Id, x.Name, x.IsActive))
            .ToList();
        model.Kpis = data.Kpis
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .Select(x => new KpiPermissionItem(x.Id, x.Name, x.IsActive))
            .ToList();
        model.Charts = data.Charts
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .Select(x => new ChartPermissionItem(x.Id, x.Name, x.IsActive))
            .ToList();
        return model;
    }
}
