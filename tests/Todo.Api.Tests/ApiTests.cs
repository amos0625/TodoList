using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Todo.Api.Contracts;
using Todo.Api.Data;
using Todo.Api.Models;
using Todo.Api.Validators;
using Xunit;

namespace Todo.Api.Tests;

public sealed class TestApi(string? databasePath = null, bool preserve = false, TimeProvider? clock = null) : WebApplicationFactory<Program>
{
    public string DatabasePath { get; } = databasePath ?? Path.Combine(Path.GetTempPath(), $"todo-tests-{Guid.NewGuid()}.db");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TodoDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<TodoDbContext>>();
            services.AddDbContext<TodoDbContext>(o => o.UseSqlite($"Data Source={DatabasePath};Pooling=False"));
            if (clock != null) { services.RemoveAll<TimeProvider>(); services.AddSingleton(clock); }
        });
    }
    public HttpClient Ready()
    {
        var client = CreateClient();
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TodoDbContext>().Database.Migrate();
        return client;
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && !preserve)
            foreach (var path in new[] { DatabasePath, DatabasePath + "-wal", DatabasePath + "-shm" })
                if (File.Exists(path)) File.Delete(path);
    }
}

public class ApiTests
{
    private sealed class BrokenClock : TimeProvider { public override DateTimeOffset GetUtcNow() => throw new InvalidOperationException("private detail"); }
    private static async Task<JsonElement> Json(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    private static StringContent Body(string json, string type = "application/json") => new(json, Encoding.UTF8, type);

    [Fact]
    public async Task CrudPreservesFieldsAndCompletionTimestamp()
    {
        using var app = new TestApi(); using var client = app.Ready();
        var created = await client.PostAsJsonAsync("/api/v1/todos", new { title = "  首頁  ", description = "備註", dueDate = "2026-09-20" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var uri = created.Headers.Location!;
        var todo = (await Json(created)).GetProperty("data");
        Assert.Equal("首頁", todo.GetProperty("title").GetString());
        Assert.Equal("medium", todo.GetProperty("priority").GetString());
        Assert.EndsWith("Z", todo.GetProperty("createdAt").GetString());
        var first = (await Json(await client.PatchAsJsonAsync(uri, new { status = "completed" }))).GetProperty("data");
        var second = (await Json(await client.PatchAsJsonAsync(uri, new { status = "completed" }))).GetProperty("data");
        Assert.Equal(first.GetProperty("completedAt").GetString(), second.GetProperty("completedAt").GetString());
        var restored = (await Json(await client.PatchAsync(uri, Body("{\"status\":\"pending\",\"dueDate\":null,\"description\":\"\"}")))).GetProperty("data");
        Assert.Equal(JsonValueKind.Null, restored.GetProperty("completedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, restored.GetProperty("dueDate").ValueKind);
        Assert.Equal("首頁", restored.GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(uri)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(uri)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(uri)).StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"title\":\"  \"}")]
    [InlineData("{\"title\":12}")]
    [InlineData("{\"title\":\"ok\",\"status\":\"completed\"}")]
    [InlineData("{\"title\":\"ok\",\"priority\":\"HIGH\"}")]
    [InlineData("{\"title\":\"ok\",\"dueDate\":\"2025-02-29\"}")]
    [InlineData("{\"title\":\"ok\",\"description\":null}")]
    [InlineData("{\"title\":\"ok\",\"title\":\"again\"}")]
    [InlineData("[]")]
    public async Task InvalidBodiesDoNotWrite(string body)
    {
        using var app = new TestApi(); using var client = app.Ready();
        var response = await client.PostAsync("/api/v1/todos", Body(body));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await Json(response)).GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(0, (await Json(await client.GetAsync("/api/v1/todos/stats"))).GetProperty("data").GetProperty("total").GetInt32());
    }

    [Theory]
    [InlineData("page=-1")]
    [InlineData("pageSize=101")]
    [InlineData("status=unknown")]
    [InlineData("foo=bar")]
    [InlineData("page=1&page=2")]
    public async Task InvalidQueriesReturn400(string query)
    {
        using var app = new TestApi(); using var client = app.Ready();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/todos?" + query)).StatusCode);
    }

    [Fact]
    public async Task LiteralSearchFilteringAndStablePaging()
    {
        using var app = new TestApi(); using var client = app.Ready();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
            for (var i = 0; i < 45; i++) db.Todos.Add(new TodoItem { Title = i % 2 == 0 ? "Alpha %_中文" : "Other", Priority = i % 3 == 0 ? "high" : "low", Status = i % 2 == 0 ? "pending" : "completed", CreatedAt = DateTime.UnixEpoch, UpdatedAt = DateTime.UnixEpoch, DueDate = i % 2 == 0 ? new DateOnly(2026, 9, 20) : null });
            db.SaveChanges();
        }
        var filtered = await Json(await client.GetAsync("/api/v1/todos?q=alpha%20%25_中文&priority=high&status=pending"));
        Assert.Equal(8, filtered.GetProperty("meta").GetProperty("total").GetInt32());
        var ids = new List<string>();
        for (var page = 1; page <= 3; page++)
        {
            var list = await Json(await client.GetAsync($"/api/v1/todos?page={page}"));
            ids.AddRange(list.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetString()!));
        }
        Assert.Equal(45, ids.Distinct().Count());
        Assert.Empty((await Json(await client.GetAsync("/api/v1/todos?page=2147483647&pageSize=100"))).GetProperty("data").EnumerateArray());
        foreach (var order in new[] { "asc", "desc" })
        {
            var sorted = (await Json(await client.GetAsync($"/api/v1/todos?sortBy=dueDate&order={order}&pageSize=100"))).GetProperty("data").EnumerateArray().ToArray();
            Assert.NotEqual(JsonValueKind.Null, sorted[0].GetProperty("dueDate").ValueKind);
            Assert.Equal(JsonValueKind.Null, sorted[^1].GetProperty("dueDate").ValueKind);
        }
        var stats = (await Json(await client.GetAsync("/api/v1/todos/stats"))).GetProperty("data");
        Assert.Equal(45, stats.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task ProtocolAndMissingResourceErrors()
    {
        using var app = new TestApi(); using var client = app.Ready();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/health")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/todos/not-a-guid")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/todos/{Guid.NewGuid()}")).StatusCode);
        var malformed = await client.PostAsync("/api/v1/todos", Body("{"));
        Assert.Equal("INVALID_JSON", (await Json(malformed)).GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await client.PostAsync("/api/v1/todos", Body("{}", "text/plain"))).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync("/api/v1/todos", Body(new string(' ', 33000)))).StatusCode);
    }

    [Fact]
    public void UnicodeBoundariesAndPatchPresence()
    {
        var title = string.Concat(Enumerable.Repeat("😀", 120));
        var todo = new TodoItem { Description = "preserve", DueDate = new DateOnly(2026, 1, 1) };
        TodoInput.Parse(JsonSerializer.SerializeToElement(new { title }), true).Apply(todo, DateTime.UnixEpoch);
        Assert.Equal(title, todo.Title);
        Assert.Equal("preserve", todo.Description);
        Assert.NotNull(todo.DueDate);
        Assert.Throws<ApiException>(() => TodoInput.Parse(JsonSerializer.SerializeToElement(new { title = title + "😀" }), false));
        Assert.Throws<ApiException>(() => TodoInput.Parse(JsonSerializer.SerializeToElement(new { description = new string('a', 2001) }), true));
        Assert.Throws<ApiException>(() => TodoInput.Parse(JsonSerializer.SerializeToElement(new { }), true));
    }

    [Fact]
    public async Task RestartAndBackupRestoreKeepData()
    {
        var path = Path.Combine(Path.GetTempPath(), $"todo-restart-{Guid.NewGuid()}.db");
        var backup = Path.Combine(Path.GetTempPath(), $"todo-backup-{Guid.NewGuid()}.db");
        try
        {
            using (var first = new TestApi(path, true))
            using (var client = first.Ready())
                Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/todos", new { title = "持久化資料" })).StatusCode);
            // All connections are disposed before copying the SQLite file.
            File.Copy(path, backup);
            foreach (var source in new[] { path, backup })
            {
                using var restarted = new TestApi(source, true); using var client = restarted.Ready();
                var list = (await Json(await client.GetAsync("/api/v1/todos"))).GetProperty("data");
                Assert.Equal("持久化資料", list[0].GetProperty("title").GetString());
            }
        }
        finally
        {
            foreach (var file in new[] { path, backup })
                foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(file + suffix)) File.Delete(file + suffix);
        }
    }

    [Fact]
    public async Task InfrastructureErrorsDoNotExposeDetails()
    {
        using var app = new TestApi(); using var client = app.Ready();
        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<TodoDbContext>().Database.ExecuteSqlRawAsync("DROP TABLE Todos");
        var unavailable = await client.GetAsync("/api/v1/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        var body = await unavailable.Content.ReadAsStringAsync();
        Assert.DoesNotContain(app.DatabasePath, body);
        Assert.Contains("requestId", body);
        using var broken = new TestApi(clock: new BrokenClock()); using var brokenClient = broken.Ready();
        var internalError = await brokenClient.PostAsJsonAsync("/api/v1/todos", new { title = "error" });
        Assert.Equal(HttpStatusCode.InternalServerError, internalError.StatusCode);
        Assert.DoesNotContain("private detail", await internalError.Content.ReadAsStringAsync());
    }
}
