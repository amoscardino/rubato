using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Rubato.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<RubatoDataContext>(options =>
{
    var dataPath = builder.Configuration.GetValue<string>("DataPath") ?? "Database";
    var dbPath = Path.Combine(dataPath, "Rubato.db");

    if (!Directory.Exists(dataPath))
        Directory.CreateDirectory(dataPath);

    options.UseSqlite($"Data Source={dbPath};");
});

builder.Services.AddDataProtection().PersistKeysToDbContext<RubatoDataContext>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RubatoDataContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error/Index");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
