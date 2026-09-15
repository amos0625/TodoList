using Microsoft.EntityFrameworkCore;
using Todo.Api.Contracts;
using Todo.Api.Data;
using Todo.Api.Models;
using Todo.Api.Validators;

namespace Todo.Api.Services;

public class TodoService(TodoDbContext db, TimeProvider clock)
{
    public async Task<object> List(TodoQuery filter, CancellationToken ct)
    {
        var query = db.Todos.AsNoTracking();
        if (filter.Status != "all") query = query.Where(x => x.Status == filter.Status);
        if (filter.Priority != "all") query = query.Where(x => x.Priority == filter.Priority);
        if (filter.Q.Length > 0)
        {
            var pattern = "%" + filter.Q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(x => EF.Functions.Like(x.Title, pattern, "\\") || EF.Functions.Like(x.Description, pattern, "\\"));
        }
        var total = await query.CountAsync(ct);
        var desc = filter.Order == "desc";
        var ordered = filter.SortBy switch
        {
            "dueDate" => desc ? query.OrderBy(x => x.DueDate == null).ThenByDescending(x => x.DueDate) : query.OrderBy(x => x.DueDate == null).ThenBy(x => x.DueDate),
            "priority" => desc ? query.OrderByDescending(x => x.Priority == "high" ? 2 : x.Priority == "medium" ? 1 : 0) : query.OrderBy(x => x.Priority == "high" ? 2 : x.Priority == "medium" ? 1 : 0),
            _ => desc ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt)
        };
        var offset = ((long)filter.Page - 1) * filter.PageSize;
        var data = offset >= total ? new List<TodoItem>() : await ordered.ThenBy(x => x.Id).Skip((int)offset).Take(filter.PageSize).ToListAsync(ct);
        return new { data, meta = new { filter.Page, filter.PageSize, total, totalPages = (int)Math.Ceiling(total / (double)filter.PageSize) } };
    }

    public async Task<object> Stats(CancellationToken ct)
    {
        var counts = await db.Todos.GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var pending = counts.Where(x => x.Status == "pending").Sum(x => x.Count);
        var completed = counts.Where(x => x.Status == "completed").Sum(x => x.Count);
        return new { total = pending + completed, pending, completed };
    }

    public async Task<TodoItem> Get(string id, CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "D", out var key)) throw ApiException.Invalid("id", "任務 ID 格式不合法");
        return await db.Todos.SingleOrDefaultAsync(x => x.Id == key, ct) ?? throw ApiException.Missing();
    }

    public async Task<TodoItem> Create(TodoInput input, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var todo = new TodoItem { CreatedAt = now, UpdatedAt = now };
        input.Apply(todo, now);
        db.Todos.Add(todo);
        await db.SaveChangesAsync(ct);
        return todo;
    }

    public async Task<TodoItem> Update(string id, TodoInput input, CancellationToken ct)
    {
        var todo = await Get(id, ct);
        input.Apply(todo, clock.GetUtcNow().UtcDateTime);
        if (db.ChangeTracker.HasChanges()) todo.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return todo;
    }

    public async Task Delete(string id, CancellationToken ct)
    {
        db.Todos.Remove(await Get(id, ct));
        await db.SaveChangesAsync(ct);
    }
}
