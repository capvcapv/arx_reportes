using ArxReportes.Models;
using ArxReportes.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArxReportes.Controllers;

[Authorize(Roles = "Admin"), Route("admin/conexiones")]
public sealed class ConnectionsController(CatalogStore store, SqlReportRunner runner) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View((await store.GetAsync(cancellationToken)).Connections.OrderBy(x => x.Name).ToList());

    [HttpGet("nueva")]
    public IActionResult Create() => View("Edit", new ConnectionDefinition());

    [HttpPost("nueva"), ValidateAntiForgeryToken]
    public Task<IActionResult> Create(ConnectionDefinition model, CancellationToken cancellationToken) => Save(model, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var connection = (await store.GetAsync(cancellationToken)).Connections.FirstOrDefault(x => x.Id == id);
        return connection is null ? NotFound() : View(connection);
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(Guid id, ConnectionDefinition model, CancellationToken cancellationToken)
    {
        model.Id = id;
        return Save(model, cancellationToken);
    }

    [HttpPost("{id:guid}/probar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(Guid id, CancellationToken cancellationToken)
    {
        var connection = (await store.GetAsync(cancellationToken)).Connections.FirstOrDefault(x => x.Id == id);
        if (connection is null) return NotFound();
        try
        {
            await runner.TestConnectionAsync(connection, cancellationToken);
            TempData["Success"] = "Conexión realizada correctamente.";
        }
        catch (Exception exception)
        {
            TempData["Error"] = "No fue posible conectar: " + exception.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("listar-bases"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ListDatabases(ConnectionDefinition model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Server))
            return BadRequest(new { success = false, message = "Indica el servidor o instancia." });
        if (!model.IntegratedSecurity && string.IsNullOrWhiteSpace(model.UserName))
            return BadRequest(new { success = false, message = "Indica el usuario SQL." });

        if (!model.IntegratedSecurity && string.IsNullOrWhiteSpace(model.Password))
        {
            var existing = (await store.GetAsync(cancellationToken)).Connections.FirstOrDefault(x => x.Id == model.Id);
            if (existing is null || string.IsNullOrWhiteSpace(existing.ProtectedPassword))
                return BadRequest(new { success = false, message = "Indica la contraseña SQL." });
            model.ProtectedPassword = existing.ProtectedPassword;
        }

        try
        {
            var databases = await runner.GetDatabasesAsync(model, cancellationToken);
            return Json(new { success = true, databases });
        }
        catch (Exception exception)
        {
            return BadRequest(new { success = false, message = "No fue posible consultar las bases: " + exception.Message });
        }
    }

    [HttpPost("{id:guid}/eliminar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!await store.DeleteConnectionAsync(id, cancellationToken))
            TempData["Error"] = "No se puede eliminar: hay reportes que usan esta conexión.";
        else
            TempData["Success"] = "Conexión eliminada.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> Save(ConnectionDefinition model, CancellationToken cancellationToken)
    {
        if (!model.IntegratedSecurity && string.IsNullOrWhiteSpace(model.UserName))
            ModelState.AddModelError(nameof(model.UserName), "Indique el usuario SQL.");

        var existing = (await store.GetAsync(cancellationToken)).Connections.FirstOrDefault(x => x.Id == model.Id);
        if (existing is null && !model.IntegratedSecurity && string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "Indique la contraseña.");

        if (!ModelState.IsValid) return View("Edit", model);
        await store.SaveConnectionAsync(model, cancellationToken);
        TempData["Success"] = "Conexión guardada.";
        return RedirectToAction(nameof(Index));
    }
}
