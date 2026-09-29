using System.Text.Json;
using ArxReportes.Models;
using Microsoft.AspNetCore.DataProtection;

namespace ArxReportes.Services;

public sealed class CatalogStore
{
    private readonly string _filePath;
    private readonly IDataProtector _protector;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public CatalogStore(IWebHostEnvironment environment, IDataProtectionProvider protectionProvider)
    {
        var dataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "catalog.json");
        _protector = protectionProvider.CreateProtector("ArxReportes.SqlPasswords.v1");
    }

    public async Task<CatalogData> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadUnsafeAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveConnectionAsync(ConnectionDefinition value, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var data = await ReadUnsafeAsync(cancellationToken);
            var existing = data.Connections.FirstOrDefault(x => x.Id == value.Id);
            if (!string.IsNullOrWhiteSpace(value.Password))
                value.ProtectedPassword = _protector.Protect(value.Password);
            else if (existing is not null)
                value.ProtectedPassword = existing.ProtectedPassword;

            value.Password = null;
            if (existing is null)
                data.Connections.Add(value);
            else
                data.Connections[data.Connections.IndexOf(existing)] = value;

            await WriteUnsafeAsync(data, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveReportAsync(ReportDefinition value, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var data = await ReadUnsafeAsync(cancellationToken);
            var existing = data.Reports.FirstOrDefault(x => x.Id == value.Id);
            if (existing is null)
                data.Reports.Add(value);
            else
                data.Reports[data.Reports.IndexOf(existing)] = value;
            await WriteUnsafeAsync(data, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> DeleteConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var data = await ReadUnsafeAsync(cancellationToken);
            if (data.Reports.Any(x => x.ConnectionId == id)) return false;
            data.Connections.RemoveAll(x => x.Id == id);
            await WriteUnsafeAsync(data, cancellationToken);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteReportAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var data = await ReadUnsafeAsync(cancellationToken);
            data.Reports.RemoveAll(x => x.Id == id);
            foreach (var user in data.Users)
                user.AllowedReportIds.RemoveAll(reportId => reportId == id);
            await WriteUnsafeAsync(data, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ReorderReportsAsync(IReadOnlyList<Guid> orderedIds, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var data = await ReadUnsafeAsync(cancellationToken);
            var positions = orderedIds
                .Select((id, index) => new { id, index })
                .ToDictionary(x => x.id, x => x.index);
            foreach (var report in data.Reports)
            {
                if (positions.TryGetValue(report.Id, out var position))
                    report.DisplayOrder = position * 10;
            }
            await WriteUnsafeAsync(data, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveUserAsync(UserDefinition value, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var data = await ReadUnsafeAsync(cancellationToken);
            var existing = data.Users.FirstOrDefault(x => x.Id == value.Id);
            if (existing is null)
                data.Users.Add(value);
            else
                data.Users[data.Users.IndexOf(existing)] = value;
            await WriteUnsafeAsync(data, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var data = await ReadUnsafeAsync(cancellationToken);
            data.Users.RemoveAll(x => x.Id == id);
            await WriteUnsafeAsync(data, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public string? UnprotectPassword(ConnectionDefinition connection)
    {
        if (string.IsNullOrWhiteSpace(connection.ProtectedPassword)) return null;
        try { return _protector.Unprotect(connection.ProtectedPassword); }
        catch { throw new InvalidOperationException("No fue posible descifrar la contraseña de esta conexión."); }
    }

    private async Task<CatalogData> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath)) return new CatalogData();
        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<CatalogData>(stream, _jsonOptions, cancellationToken) ?? new CatalogData();
    }

    private async Task WriteUnsafeAsync(CatalogData data, CancellationToken cancellationToken)
    {
        var temporaryPath = _filePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
            await JsonSerializer.SerializeAsync(stream, data, _jsonOptions, cancellationToken);
        File.Move(temporaryPath, _filePath, true);
    }
}
