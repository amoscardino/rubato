using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Rubato.Data;
using Rubato.Pages;
using Rubato.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<RubatoDataContext>(options =>
{
    var dataPath = builder.Configuration.GetValue<string>("DataPath") ?? "Database";
    var dbPath = Path.Combine(dataPath, "Rubato.db");

    if (!Directory.Exists(dataPath))
        Directory.CreateDirectory(dataPath);

    options.UseSqlite($"Data Source={dbPath};");
});

builder.Services.AddDataProtection().PersistKeysToDbContext<RubatoDataContext>();

builder.Services.AddTransient<DayService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RubatoDataContext>();
    db.Database.Migrate();
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
