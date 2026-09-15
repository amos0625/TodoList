namespace Todo.Api.Contracts;

public record FieldError(string Field, string Message);

public class ApiException(int status, string code, string message, params FieldError[] details) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public FieldError[] Details { get; } = details;
    public static ApiException Invalid(string field, string message) => new(400, "VALIDATION_ERROR", "請檢查輸入內容", new FieldError(field, message));
    public static ApiException Missing() => new(404, "TODO_NOT_FOUND", "找不到此任務");
}
