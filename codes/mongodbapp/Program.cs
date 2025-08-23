using mongodbapp.Models;
using MongoDB.Driver;
using MongoDB.Bson;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<MongoDbContext>();

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

// POST: Add a new product
app.MapPost("/products", async (MongoDbContext dbContext, Product product) =>
{
    await dbContext.Products.InsertOneAsync(product);
    return Results.Created($"/products/{product.Id}", product);
});

// GET: Retrieve all products
app.MapGet("/products", async (MongoDbContext dbContext) =>
    await dbContext.Products.Find(product => true).ToListAsync());

// GET: Retrieve a single product by ID
app.MapGet("/products/{id}", async (MongoDbContext dbContext, string id) =>
{
    var product = await dbContext.Products.Find(p => p.Id == id).FirstOrDefaultAsync();
    return product is not null ? Results.Ok(product) : Results.NotFound();
});

// PUT: Update a product
app.MapPut("/products/{id}", async (MongoDbContext dbContext, Product product, string id) =>
{
    var filter = Builders<Product>.Filter.Eq(p => p.Id, id);
    var update = Builders<Product>.Update
        .Set(p => p.Name, product.Name)
        .Set(p => p.Price, product.Price);

    await dbContext.Products.UpdateOneAsync(filter, update);
    return Results.Ok(product);
});

// DELETE: Delete a product
app.MapDelete("/products/{id}", async (MongoDbContext dbContext, string id) =>
{
    var filter = Builders<Product>.Filter.Eq(p => p.Id, id);
    await dbContext.Products.DeleteOneAsync(filter);
    return Results.Ok();
});


app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
