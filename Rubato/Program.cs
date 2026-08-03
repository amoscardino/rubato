using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Rubato.Data;
using Rubato.Pages;
using Rubato.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// A factory rather than AddDbContext: a scoped context would live for the whole Blazor circuit,
// which means every component in the session sharing one context (and colliding on it whenever
// two operations overlap). Services create a short-lived context per operation instead.
builder.Services.AddDbContextFactory<RubatoDataContext>(options =>
{
    var dataPath = builder.Configuration.GetValue<string>("DataPath") ?? "Database";
    var dbPath = Path.Combine(dataPath, "Rubato.db");

    if (!Directory.Exists(dataPath))
        Directory.CreateDirectory(dataPath);

    options.UseSqlite($"Data Source={dbPath};");
});

// Data Protection and the startup migration resolve the context from a scope, so keep a scoped
// registration alongside the factory. Both create their own scope per operation.
builder.Services.AddScoped<RubatoDataContext>(services =>
    services.GetRequiredService<IDbContextFactory<RubatoDataContext>>().CreateDbContext());

builder.Services.AddDataProtection().PersistKeysToDbContext<RubatoDataContext>();

builder.Services.AddTransient<EntryService>();
builder.Services.AddTransient<ProjectService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RubatoDataContext>();
    db.Database.Migrate();

    // Stored durations are a copy of a value derived from the free-text time field, so rows written
    // by an older parser can hold hours that no longer follow from their time text. Nothing else
    // recomputes them until that row is edited again, so square them up here.
    var entryService = scope.ServiceProvider.GetRequiredService<EntryService>();
    var reconciled = await entryService.ReconcileDurationsAsync();

    if (reconciled > 0)
    {
        app.Logger.LogInformation("Recomputed {Count} stored entry duration(s).", reconciled);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
