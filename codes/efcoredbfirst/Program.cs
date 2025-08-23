using efcoredbfirst.Models;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();


// Add services to the container.
builder.Services.AddDbContext<EfCoreLabContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MyDB")));

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

app.MapGet("/products", async (EfCoreLabContext dbContext) =>
    await dbContext.Products.ToListAsync());

app.MapGet("/products/{id}", async (EfCoreLabContext dbContext, int id) =>
{
    var product = await dbContext.Products.Where(p => p.Id == id).FirstOrDefaultAsync();
    return Results.Ok(product);
});


app.MapPost("/products", async (EfCoreLabContext dbContext, Product product) =>
{
    dbContext.Products.Add(product);
    await dbContext.SaveChangesAsync();
    return Results.Created($"/products/{product.Id}", product);
});

app.MapPut("/products/{id}", async (EfCoreLabContext dbContext, Product product, int id) =>
{
    var p = await dbContext.Products.Where(p => p.Id == id).FirstOrDefaultAsync();
    if(p != null)
    {
        p.Price = product.Price;
        if(!string.IsNullOrEmpty(product.Name))
            p.Name = product.Name;
        
        dbContext.Products.Update(p);
        await dbContext.SaveChangesAsync();
    }
    return Results.Ok(p);
});

app.MapDelete("/products/{id}", async (EfCoreLabContext dbContext, int id) =>
{
    var product = await dbContext.Products.Where(p => p.Id == id).FirstOrDefaultAsync();
    if (product != null)
    {
        dbContext.Products.Remove(product);
        await dbContext.SaveChangesAsync();
    }
    return Results.Ok(product);
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
