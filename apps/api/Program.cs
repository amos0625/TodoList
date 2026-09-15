using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Todo.Api.Contracts;
using Todo.Api.Data;
using Todo.Api.Middleware;
using Todo.Api.Services;

var builder = WebApplication.CreateBuilder(args);
// Console logging works for local runs and containers without Windows Event Log privileges.
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
var connection = new SqliteConnectionStringBuilder(builder.Configuration.GetConnectionString("TodoDb") ?? "Data Source=data/todo.db");
if (connection.DataSource != ":memory:")
{
    connection.DataSource = Path.GetFullPath(connection.DataSource, builder.Environment.ContentRootPath);
    Directory.CreateDirectory(Path.GetDirectoryName(connection.DataSource)!);
}
builder.Services.AddDbContext<TodoDbContext>(options => options.UseSqlite(connection.ToString()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<TodoService>();
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(ApiErrors.Envelope(context.HttpContext,
        new ApiException(400, "INVALID_JSON", "JSON 格式錯誤或缺少內容")));
});
var app = builder.Build();
if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<TodoDbContext>().Database.MigrateAsync();
    return;
}
app.UseMiddleware<ApiErrors>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapGet("/api/v1/health", async (TodoDbContext db, CancellationToken ct) =>
{
    await db.Todos.AnyAsync(ct);
    return Results.Ok(new { data = new { status = "ok" } });
});
app.MapFallback("/api/{**path}", () => Results.NotFound());
if (Directory.Exists(Path.Combine(app.Environment.ContentRootPath, "wwwroot"))) app.MapFallbackToFile("index.html");
app.Run();

public partial class Program { }
