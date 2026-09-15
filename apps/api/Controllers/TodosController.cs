using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Todo.Api.Services;
using Todo.Api.Validators;

namespace Todo.Api.Controllers;

[ApiController]
[Route("api/v1/todos")]
public class TodosController(TodoService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.List(TodoQuery.Parse(Request.Query), ct));

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct) => Ok(new { data = await service.Stats(ct) });

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) => Ok(new { data = await service.Get(id, ct) });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] JsonElement body, CancellationToken ct)
    {
        var todo = await service.Create(TodoInput.Parse(body, false), ct);
        return Created($"/api/v1/todos/{todo.Id}", new { data = todo });
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] JsonElement body, CancellationToken ct) =>
        Ok(new { data = await service.Update(id, TodoInput.Parse(body, true), ct) });

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await service.Delete(id, ct);
        return NoContent();
    }
}
