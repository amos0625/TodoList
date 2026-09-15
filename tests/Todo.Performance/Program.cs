using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Todo.Api.Data;
using Todo.Api.Models;

// Runs only when explicitly invoked. Uses its own temporary SQLite file and real HTTP.
var root = Path.GetFullPath(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
var directory = Path.Combine(Path.GetTempPath(), $"todo-performance-{Guid.NewGuid()}");
Directory.CreateDirectory(directory);
var database = Path.Combine(directory, "benchmark.db");
Process? server = null;
try
{
    var options = new DbContextOptionsBuilder<TodoDbContext>().UseSqlite($"Data Source={database};Pooling=False").Options;
    using (var db = new TodoDbContext(options))
    {
        await db.Database.MigrateAsync();
        db.Todos.AddRange(Enumerable.Range(0, 10000).Select(i => new TodoItem
        {
            Title = $"Task {i:D5}",
            Description = "Benchmark task 中文 %_",
            Priority = i % 3 == 0 ? "high" : i % 3 == 1 ? "medium" : "low",
            Status = i % 2 == 0 ? "pending" : "completed",
            DueDate = i % 4 == 0 ? null : new DateOnly(2026, 9, 20),
            CreatedAt = DateTime.UnixEpoch.AddSeconds(i),
            UpdatedAt = DateTime.UnixEpoch.AddSeconds(i)
        }));
        await db.SaveChangesAsync();
    }
    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
    listener.Start(); var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
    var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_EXE") ?? "dotnet")
    {
        WorkingDirectory = Path.Combine(root, "apps/api"),
        UseShellExecute = false,
        CreateNoWindow = true
    };
    start.ArgumentList.Add(Path.Combine(root, "apps/api/bin/Release/net10.0/Todo.Api.dll"));
    start.Environment["ConnectionStrings__TodoDb"] = $"Data Source={database};Pooling=False";
    start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
    start.Environment["Logging__LogLevel__Default"] = "Warning";
    server = Process.Start(start)!;
    using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(15) };
    var ready = false;
    for (var i = 0; i < 100; i++)
    {
        try { using var response = await client.GetAsync("/api/v1/health"); if (response.IsSuccessStatusCode) { ready = true; break; } } catch (HttpRequestException) { }
        await Task.Delay(100);
    }
    if (!ready) throw new Exception("Benchmark server did not start");
    string[] paths = ["/api/v1/todos", "/api/v1/todos?q=Task&status=pending&sortBy=dueDate", "/api/v1/todos/stats"];
    for (var i = 0; i < 60; i++) { using var warmup = await client.GetAsync(paths[i % 3]); warmup.EnsureSuccessStatusCode(); }
    var samples = new ConcurrentBag<double>(); var errors = 0;
    var timer = Stopwatch.StartNew();
    await Task.WhenAll(Enumerable.Range(0, 10).Select(async worker =>
    {
        var i = worker;
        while (timer.Elapsed < TimeSpan.FromSeconds(60))
        {
            var requestTimer = Stopwatch.StartNew();
            try { using var response = await client.GetAsync(paths[i++ % paths.Length]); if (!response.IsSuccessStatusCode) Interlocked.Increment(ref errors); }
            catch (HttpRequestException) { Interlocked.Increment(ref errors); }
            catch (TaskCanceledException) { Interlocked.Increment(ref errors); }
            samples.Add(requestTimer.Elapsed.TotalMilliseconds);
        }
    }));
    var values = samples.Order().ToArray();
    var p95 = values[(int)Math.Ceiling(values.Length * .95) - 1];
    var result = new
    {
        utc = DateTime.UtcNow,
        os = RuntimeInformation.OSDescription,
        runtime = RuntimeInformation.FrameworkDescription,
        cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
        processors = Environment.ProcessorCount,
        efCore = typeof(DbContext).Assembly.GetName().Version?.ToString(),
        sqlite = SQLitePCL.raw.sqlite3_libversion().utf8_to_string(),
        database = "temporary SQLite, 10000 rows, Pooling=False, default journal settings",
        transport = "Kestrel loopback HTTP",
        concurrentUsers = 10,
        seconds = timer.Elapsed.TotalSeconds,
        requests = values.Length,
        errors,
        p95Milliseconds = p95,
        passed = errors == 0 && p95 < 300
    };
    Directory.CreateDirectory(Path.Combine(root, "artifacts"));
    var report = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(Path.Combine(root, "artifacts/performance.json"), report);
    Console.WriteLine(report);
    return result.passed ? 0 : 1;
}
finally
{
    if (server is { HasExited: false }) { server.Kill(entireProcessTree: true); await server.WaitForExitAsync(); }
    server?.Dispose();
    // Directory was created with a unique fixed prefix by this process under the OS temp folder.
    if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
}
