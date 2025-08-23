using secrestapi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BC = BCrypt.Net.BCrypt;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "Secure REST API";
        document.Info.Version = "v1";
        document.Info.Description = "A secure REST API with JWT authentication";
        
        // Add JWT security scheme
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter JWT Bearer token"
        };
        
        return Task.CompletedTask;
    });
});


builder.Services.AddDbContext<AppDbContext>(options =>
   options.UseSqlServer( builder.Configuration.GetConnectionString("MyDB")));

// configure jwt
var key = builder.Configuration["AppSettings:Secret"];
var keyBytes = Encoding.ASCII.GetBytes(key ?? "aaaaabbbbbcccccddddd11234df4444sd");
builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(o =>
{
    o.RequireHttpsMetadata = false;
    o.SaveToken = true;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});


var multiPolicyAuthorization = new AuthorizationPolicyBuilder( 
      JwtBearerDefaults.AuthenticationScheme)
  .RequireAuthenticatedUser()
  .Build();

builder.Services.AddAuthorization( o => o.DefaultPolicy = multiPolicyAuthorization);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Secure REST API";
        options.Theme = ScalarTheme.Purple;
        options.ShowSidebar = true;
        options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseHttpsRedirection();

// add these lines
app.UseAuthentication();
app.UseAuthorization();

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


app.MapPost("/register", async (AppDbContext dbContext, ApiUser usr) =>
{
    var user = new ApiUser
    {
        Username = usr.Username,
        Password = BC.HashPassword( usr.Password),
        Email = usr.Email,
        Name = usr.Name
    };
    dbContext.Users.Add(user);
    await dbContext.SaveChangesAsync();
    return Results.Ok();
})
.WithName("RegisterUser")
.WithOpenApi(operation =>
{
    operation.Summary = "Register a new user";
    operation.Description = "Creates a new user account with the provided information";
    return operation;
});

app.MapPost("/login", (AppDbContext dbContext, IConfiguration configuration, UserLogin model) =>
{
    // ambil user
    var usr = dbContext.Users.Where(o => o.Username == model.UserName).FirstOrDefault();
    if (usr != null)
    {
        if (BC.Verify(model.Password, usr.Password))
        {
            var key = configuration.GetValue<string>("AppSettings:Secret");
            var keyBytes = Encoding.ASCII.GetBytes(key ?? "aaaaabbbbbcccccddddd11234df4444sd");

            // generate token + expired
            var expiredAt = DateTime.Now.AddDays(2);
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(
                        new Claim[]
                        {
                                    new Claim(ClaimTypes.Name,usr.Username)
                        }),
                Expires = expiredAt,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(keyBytes),
                    SecurityAlgorithms.HmacSha256Signature)

            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var userToken = new UserToken
            {
                Token = tokenHandler.WriteToken(token),
                ExpiredAt = expiredAt.ToString(),
                Message = ""
            };
            return userToken;
        }
    }

    return new UserToken { Message = "Username or password is invalid" };
})
.WithName("LoginUser")
.WithOpenApi(operation =>
{
    operation.Summary = "User login";
    operation.Description = "Authenticates a user and returns a JWT token if successful";
    return operation;
});


app.MapGet("/profile", [Authorize] async (HttpContext httpContext, AppDbContext dbContext) =>
{
    var username = httpContext.User.Identity?.Name;
    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);
    return user != null ? Results.Ok(new {
        user.Username,
        user.Name,
        user.Email
    }) : Results.NotFound();
})
.WithName("GetProfile")
.WithOpenApi(operation =>
{
    operation.Summary = "Get user profile";
    operation.Description = "Returns the profile information for the authenticated user";
    operation.Security = new List<OpenApiSecurityRequirement>
    {
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        }
    };
    return operation;
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
