using privdata.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();


// add database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer( builder.Configuration.GetConnectionString("MyDB")));

// Add data protection services
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>();

// add custom data protection provider
builder.Services.AddTransient<IDataProtectionProvider, SqlServerDataProtectionProvider>();

builder.Services.AddTransient<SensitiveDataService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
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

app.MapPost("/employees", (AppDbContext dbContext, SensitiveDataService service, Employee employee) =>
{
    dbContext.Employees.Add(service.EncryptEmployeeData(employee));
    dbContext.SaveChanges();
    return Results.Ok();
});

app.MapGet("/employees", (AppDbContext dbContext, SensitiveDataService service) =>
{
    var employees = dbContext.Employees.AsEnumerable().Select(service.MaskEmployeeData).ToList();
    return Results.Ok(employees);
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
