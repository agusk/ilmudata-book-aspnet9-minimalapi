var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

var calculatorApi = app.MapGroup("/api/calculator");

calculatorApi.MapPost("/add", (Numeric numbers) => 
{
    var result = numbers.Number1 + numbers.Number2;
    return Results.Ok(new Numeric(numbers.Number1, numbers.Number2, result));
});

calculatorApi.MapPost("/subtract", (Numeric numbers) =>
{
    var result = numbers.Number1 - numbers.Number2;
    return Results.Ok(new Numeric(numbers.Number1, numbers.Number2, result));
});

calculatorApi.MapPost("/multiply", (Numeric numbers) =>
{
    var result = numbers.Number1 * numbers.Number2;
    return Results.Ok(new Numeric(numbers.Number1, numbers.Number2, result));
});

calculatorApi.MapPost("/divide", (Numeric numbers) =>
{
    if (numbers.Number2 == 0)
    {
        return Results.BadRequest("Cannot divide by zero");
    }
    var result = numbers.Number1 / numbers.Number2;
    return Results.Ok( new Numeric(numbers.Number1, numbers.Number2, result));
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record Numeric(double Number1, double Number2, double Result = 0);