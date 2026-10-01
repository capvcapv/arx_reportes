namespace ArxReportes;

using ArxReportes.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using ArxReportes.Models;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllersWithViews();
        builder.Services.AddMemoryCache();
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));
        builder.Services.AddSingleton<CatalogStore>();
        builder.Services.AddScoped<SqlReportRunner>();
        builder.Services.AddScoped<SqlKpiRunner>();
        builder.Services.AddScoped<SqlChartRunner>();
        builder.Services.AddSingleton<ReportKpiCalculator>();
        builder.Services.AddSingleton<IPasswordHasher<UserDefinition>, PasswordHasher<UserDefinition>>();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/acceso";
                options.AccessDeniedPath = "/acceso-denegado";
                options.Cookie.Name = "ArxReportes.Admin";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        // En desarrollo usamos el perfil HTTP para que no sea obligatorio instalar
        // un certificado local. En producción se conserva la redirección a HTTPS.
        if (!app.Environment.IsDevelopment())
            app.UseHttpsRedirection();
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapStaticAssets();
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}")
            .WithStaticAssets();

        app.Run();
    }
}
