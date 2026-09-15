using System.Globalization;
using System.Text;
using System.Text.Json;
using Todo.Api.Contracts;
using Todo.Api.Models;

namespace Todo.Api.Validators;

// JSON properties are retained so PATCH can distinguish absent fields from explicit null.
public sealed class TodoInput
{
    private readonly Dictionary<string, JsonElement> fields = new(StringComparer.Ordinal);
    public static TodoInput Parse(JsonElement json, bool patch)
    {
        if (json.ValueKind != JsonValueKind.Object) throw ApiException.Invalid("body", "必須為 JSON 物件");
        var input = new TodoInput();
        foreach (var p in json.EnumerateObject())
        {
            if (!(p.Name is "title" or "description" or "priority" or "dueDate" || patch && p.Name == "status"))
                throw ApiException.Invalid(p.Name, "不允許的欄位");
            if (!input.fields.TryAdd(p.Name, p.Value)) throw ApiException.Invalid(p.Name, "欄位不可重複");
        }
        if (patch && input.fields.Count == 0) throw ApiException.Invalid("body", "至少提供一個欄位");
        if (!patch && !input.fields.ContainsKey("title")) throw ApiException.Invalid("title", "標題不可空白");
        // Validate all properties before applying changes to a tracked entity.
        input.Apply(new TodoItem(), DateTime.UnixEpoch);
        return input;
    }

    public void Apply(TodoItem todo, DateTime now)
    {
        foreach (var (name, value) in fields)
        {
            if (name == "dueDate" && value.ValueKind == JsonValueKind.Null) { todo.DueDate = null; continue; }
            if (value.ValueKind != JsonValueKind.String) throw ApiException.Invalid(name, "必須為字串");
            var text = value.GetString()!;
            switch (name)
            {
                case "title":
                    text = text.Trim();
                    if (text.EnumerateRunes().Count() is < 1 or > 120) throw ApiException.Invalid(name, "標題須為 1～120 字元");
                    todo.Title = text;
                    break;
                case "description":
                    if (text.EnumerateRunes().Count() > 2000) throw ApiException.Invalid(name, "備註最多 2,000 字元");
                    todo.Description = text;
                    break;
                case "priority":
                    if (text is not ("low" or "medium" or "high")) throw ApiException.Invalid(name, "優先級不合法");
                    todo.Priority = text;
                    break;
                case "status":
                    if (text is not ("pending" or "completed")) throw ApiException.Invalid(name, "狀態不合法");
                    if (text != todo.Status) todo.CompletedAt = text == "completed" ? now : null;
                    todo.Status = text;
                    break;
                case "dueDate":
                    if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                        throw ApiException.Invalid(name, "請提供 YYYY-MM-DD 格式的有效日期");
                    todo.DueDate = date;
                    break;
            }
        }
    }
}
