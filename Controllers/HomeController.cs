using Microsoft.AspNetCore.Mvc;
using ArxReportes.Models;
using ArxReportes.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ArxReportes.Controllers;

[Authorize]
public class HomeController(
    CatalogStore store,
    SqlKpiRunner kpiRunner,
    SqlChartRunner chartRunner,
    IConfiguration configuration) : Controller
{
    public async Task<IActionResult> Index(
        bool refreshKpis = false,
        bool refreshCharts = false,
        CancellationToken cancellationToken = default)
    {
        var data = await store.GetAsync(cancellationToken);
        IEnumerable<ReportDefinition> permittedReports = data.Reports;
        IEnumerable<KpiDefinition> permittedKpis = data.Kpis;
        IEnumerable<ChartDefinition> permittedCharts = data.Charts;
        if (!User.IsInRole("Admin"))
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Challenge();
            var user = data.Users.FirstOrDefault(x => x.Id == userId && x.IsActive);
            if (user is null) return Challenge();
            var allowedReportIds = user.AllowedReportIds.ToHashSet();
            var allowedKpiIds = user.AllowedKpiIds.ToHashSet();
            var allowedChartIds = user.AllowedChartIds.ToHashSet();
            permittedReports = permittedReports.Where(x => allowedReportIds.Contains(x.Id));
            permittedKpis = permittedKpis.Where(x => allowedKpiIds.Contains(x.Id));
            permittedCharts = permittedCharts.Where(x => allowedChartIds.Contains(x.Id));
        }

        var reports = permittedReports
            .Where(x => x.IsActive && data.Connections.Any(c => c.Id == x.ConnectionId && c.IsActive))
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .ToList();

        var activeConnections = data.Connections.Where(x => x.IsActive).ToDictionary(x => x.Id);
        var maximumItems = Math.Clamp(configuration.GetValue("Kpis:MaximumDashboardItems", 12), 1, 30);
        var kpis = permittedKpis
            .Where(x => x.IsActive && activeConnections.ContainsKey(x.ConnectionId))
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .Take(maximumItems)
            .ToList();

        var maximumCharts = Math.Clamp(configuration.GetValue("Charts:MaximumDashboardItems", 8), 1, 20);
        var charts = permittedCharts
            .Where(x => x.IsActive && activeConnections.ContainsKey(x.ConnectionId))
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .Take(maximumCharts)
            .ToList();

        var kpiTask = Task.WhenAll(kpis.Select(kpi =>
            kpiRunner.GetAsync(kpi, activeConnections[kpi.ConnectionId], refreshKpis, cancellationToken)));
        var chartTask = Task.WhenAll(charts.Select(chart =>
            chartRunner.GetAsync(chart, activeConnections[chart.ConnectionId], refreshCharts, cancellationToken)));
        await Task.WhenAll(kpiTask, chartTask);
        var cards = await kpiTask;
        var chartCards = await chartTask;

        var accessibleReportIds = reports.Select(x => x.Id).ToHashSet();
        foreach (var card in cards)
        {
            if (card.Definition.LinkedReportId.HasValue && accessibleReportIds.Contains(card.Definition.LinkedReportId.Value))
                card.AccessibleReportId = card.Definition.LinkedReportId;
        }

        foreach (var card in chartCards)
        {
            if (card.Definition.LinkedReportId.HasValue && accessibleReportIds.Contains(card.Definition.LinkedReportId.Value))
                card.AccessibleReportId = card.Definition.LinkedReportId;
        }

        return View(new DashboardViewModel
        {
            Reports = reports,
            Kpis = [.. cards],
            Charts = [.. chartCards]
        });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
