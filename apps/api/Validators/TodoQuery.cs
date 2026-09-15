using System.Text;
using Todo.Api.Contracts;

namespace Todo.Api.Validators;

public record TodoQuery(string Q, string Status, string Priority, string SortBy, string Order, int Page, int PageSize)
{
    public static TodoQuery Parse(IQueryCollection query)
    {
        string[] allowed = ["q", "status", "priority", "sortBy", "order", "page", "pageSize"];
        foreach (var (key, values) in query)
            if (!allowed.Contains(key) || values.Count != 1) throw ApiException.Invalid(key, "不合法或重複的查詢參數");
        string Read(string key, string fallback) => query.TryGetValue(key, out var value) ? value.ToString() : fallback;
        string Choice(string key, string fallback, params string[] choices)
        {
            var value = Read(key, fallback);
            return choices.Contains(value) ? value : throw ApiException.Invalid(key, "不合法的選項");
        }
        int Number(string key, int fallback, int max)
        {
            var value = Read(key, fallback.ToString());
            return value.All(char.IsAsciiDigit) && int.TryParse(value, out var n) && n >= 1 && n <= max
                ? n : throw ApiException.Invalid(key, "超出允許的整數範圍");
        }
        var q = Read("q", "").Trim();
        if (q.EnumerateRunes().Count() > 100) throw ApiException.Invalid("q", "搜尋最多 100 字元");
        return new(q, Choice("status", "all", "all", "pending", "completed"),
            Choice("priority", "all", "all", "low", "medium", "high"),
            Choice("sortBy", "createdAt", "createdAt", "dueDate", "priority"),
            Choice("order", "desc", "asc", "desc"), Number("page", 1, int.MaxValue), Number("pageSize", 20, 100));
    }
}
