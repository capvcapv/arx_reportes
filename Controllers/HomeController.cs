using Microsoft.AspNetCore.Mvc;
using ArxReportes.Models;
using ArxReportes.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ArxReportes.Controllers;

[Authorize]
public class HomeController(CatalogStore store) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var data = await store.GetAsync(cancellationToken);
        IEnumerable<ReportDefinition> permittedReports = data.Reports;
        if (!User.IsInRole("Admin"))
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Challenge();
            var user = data.Users.FirstOrDefault(x => x.Id == userId && x.IsActive);
            if (user is null) return Challenge();
            var allowedIds = user.AllowedReportIds.ToHashSet();
            permittedReports = permittedReports.Where(x => allowedIds.Contains(x.Id));
        }

        var reports = permittedReports
            .Where(x => x.IsActive && data.Connections.Any(c => c.Id == x.ConnectionId && c.IsActive))
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .ToList();
        return View(reports);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
