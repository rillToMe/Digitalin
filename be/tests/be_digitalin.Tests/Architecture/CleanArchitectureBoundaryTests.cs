using System.Reflection;
using System.Text.RegularExpressions;
using be_digitalin.Application.Ports;
using be_digitalin.Controllers;
using be_digitalin.Data;
using be_digitalin.Domain;
using be_digitalin.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace be_digitalin.Tests.Architecture;

/// <summary>
/// Verifikasi batas dependensi Clean Architecture antar 4 proyek Modular Monolith,
/// paritas penuh rute HTTP terhadap route-baseline.txt, dan keutuhan 31 migrasi EF Core.
/// </summary>
public class CleanArchitectureBoundaryTests
{
    private static readonly Assembly DomainAssembly = typeof(Pricing).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IApplicationDbContext).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(DigitalinDbContext).Assembly;
    private static readonly Assembly ApiAssembly = typeof(OrderController).Assembly;

    [Fact]
    public void Domain_ShouldHaveNoOutwardProjectOrFrameworkDependencies()
    {
        var referencedNames = DomainAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        Assert.DoesNotContain("be_digitalin.Application", referencedNames);
        Assert.DoesNotContain("be_digitalin.Infrastructure", referencedNames);
        Assert.DoesNotContain("be_digitalin.Api", referencedNames);
        Assert.DoesNotContain(referencedNames, n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedNames, n => n.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedNames, n => n.StartsWith("Google.Apis", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedNames, n => n.StartsWith("AWSSDK", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedNames, n => n.StartsWith("StackExchange.Redis", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Application_ShouldOnlyReferenceDomainAndNotInfrastructureOrApiOrProviderSdks()
    {
        var referencedNames = ApplicationAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        Assert.Contains("be_digitalin.Domain", referencedNames);
        Assert.DoesNotContain("be_digitalin.Infrastructure", referencedNames);
        Assert.DoesNotContain("be_digitalin.Api", referencedNames);
        Assert.DoesNotContain(referencedNames, n => n.StartsWith("Pomelo.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedNames, n => n.StartsWith("Google.Apis", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedNames, n => n.StartsWith("AWSSDK", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedNames, n => n.StartsWith("MailKit", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedNames, n => n.StartsWith("StackExchange.Redis", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Infrastructure_ShouldNotReferenceApi()
    {
        var referencedNames = InfrastructureAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        Assert.Contains("be_digitalin.Domain", referencedNames);
        Assert.Contains("be_digitalin.Application", referencedNames);
        Assert.DoesNotContain("be_digitalin.Api", referencedNames);
    }

    [Fact]
    public void AllFourLayers_AreCompiledIntoDistinctAssemblies()
    {
        var distinct = new HashSet<Assembly>
        {
            DomainAssembly,
            ApplicationAssembly,
            InfrastructureAssembly,
            ApiAssembly
        };

        Assert.Equal(4, distinct.Count);
    }

    [Fact]
    public void Api_ShouldPreserveAllBaselineHttpRoutesAndMethods()
    {
        var discoveredRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var controllerTypes = ApiAssembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t))
            .ToList();

        Assert.Equal(21, controllerTypes.Count);

        foreach (var controller in controllerTypes)
        {
            var routeAttr = controller.GetCustomAttribute<RouteAttribute>();
            var controllerPrefix = routeAttr?.Template?.Trim('/') ?? string.Empty;

            var methods = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                var httpAttrs = method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).ToList();
                foreach (var httpAttr in httpAttrs)
                {
                    var actionTemplate = httpAttr.Template?.Trim('/') ?? string.Empty;
                    var combined = string.IsNullOrEmpty(actionTemplate)
                        ? $"/{controllerPrefix}"
                        : $"/{controllerPrefix}/{actionTemplate}";

                    // Normalisasi constraint parameter rute seperti {id:int} -> {id} agar cocok dengan OpenAPI route baseline
                    combined = Regex.Replace(combined, @"\{([^}:]+):[^}]+\}", "{$1}");
                    combined = "/" + combined.Trim('/');

                    foreach (var httpMethod in httpAttr.HttpMethods)
                    {
                        discoveredRoutes.Add($"{httpMethod.ToUpperInvariant()} {combined}");
                    }
                }
            }
        }

        // Minimal API endpoints di Program.cs
        discoveredRoutes.Add("GET /health");
        discoveredRoutes.Add("GET /ping-db");

        // Cari docs/route-baseline.txt dari direktori solusi
        var baseDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (baseDir != null && !File.Exists(Path.Combine(baseDir.FullName, "docs", "route-baseline.txt")))
        {
            baseDir = baseDir.Parent;
        }

        Assert.NotNull(baseDir);
        var baselinePath = Path.Combine(baseDir!.FullName, "docs", "route-baseline.txt");
        var expectedRoutes = File.ReadAllLines(baselinePath)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith('#'))
            .Select(l => Regex.Replace(l, @"\s+", " "))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(77, expectedRoutes.Count);
        Assert.All(expectedRoutes, expectedRoute => Assert.Contains(expectedRoute, discoveredRoutes));

        // Verifikasi rute tambahan pada controller PaymentMethod & AdminPaymentMethod juga tetap utuh
        Assert.Contains("GET /api/v1/payment-methods", discoveredRoutes);
        Assert.Contains("GET /api/v1/admin/payment-methods", discoveredRoutes);
    }

    [Fact]
    public void Infrastructure_ShouldPreserveAll31EfCoreMigrationsAndModelSnapshot()
    {
        var migrationTypes = InfrastructureAssembly
            .GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && t.GetCustomAttribute<MigrationAttribute>() != null)
            .Select(t => t.GetCustomAttribute<MigrationAttribute>()!.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(31, migrationTypes.Count);
        Assert.Contains("20260614153009_InitialMariaDb", migrationTypes);

        var snapshotType = InfrastructureAssembly
            .GetTypes()
            .SingleOrDefault(t => typeof(ModelSnapshot).IsAssignableFrom(t)
                                  && t.GetCustomAttribute<DbContextAttribute>()?.ContextType == typeof(DigitalinDbContext));
        Assert.NotNull(snapshotType);

        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<DigitalinDbContext>()
            .UseSqlite(connection, b => b.MigrationsAssembly(InfrastructureAssembly.FullName))
            .Options;

        using var db = new DigitalinDbContext(options);
        var discoveredByEf = db.Database.GetMigrations().ToList();
        Assert.Equal(31, discoveredByEf.Count);
        Assert.NotNull(db.Model.FindEntityType(typeof(TopupOrder)));
        Assert.NotNull(db.Model.FindEntityType(typeof(GameProduct)));
        Assert.NotNull(db.Model.FindEntityType(typeof(Voucher)));
    }
}
