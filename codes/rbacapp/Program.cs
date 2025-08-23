using rbacapp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BC = BCrypt.Net.BCrypt;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Options;
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
        ValidateAudience = false,
    };
});

var multiPolicyAuthorization = new AuthorizationPolicyBuilder(
   JwtBearerDefaults.AuthenticationScheme )
  .RequireAuthenticatedUser()   
  .Build();
builder.Services.AddAuthorization(o =>
{
    o.DefaultPolicy = multiPolicyAuthorization;
});

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

app.MapGet("/setuproles", async (AppDbContext dbContext) =>
{    
    var roles = dbContext.Roles.ToList();
    if(roles.Count <= 0)
    {
        dbContext.Roles.Add(new Role { Name = "Admin" });
        dbContext.Roles.Add(new Role { Name = "Manager" });
        await dbContext.SaveChangesAsync();
    }    
    return Results.Ok(new { Message = "Roles was created"});
});

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
});

app.MapPost("/login", (AppDbContext dbContext, IConfiguration configuration, UserLogin model) =>
{
    // ambil user
    var usr = dbContext.Users.Where(o => o.Username == model.UserName).FirstOrDefault();
    if (usr != null)
    {
        if (BC.Verify(model.Password, usr.Password))
        {
            List<Claim> claims = new List<Claim>(); 
            // get roles by userid
            var roles = from userRole in dbContext.UserRoles
                        join role in dbContext.Roles on userRole.Role!.Id equals role.Id
                        where userRole.User!.Id == usr.Id
                        select role.Name;

            foreach (var roleName in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, "" + roleName));
            }
            claims.Add(new Claim(ClaimTypes.Name, usr.Username));

            // generate token
            var key = configuration.GetValue<string>("AppSettings:Secret");
            var keyBytes = Encoding.ASCII.GetBytes(key ?? "aaaaabbbbbcccccddddd11234df4444sd");
            var symKey = new SymmetricSecurityKey(keyBytes); // Use a secure key
            var creds = new SigningCredentials( symKey, SecurityAlgorithms.HmacSha256);
            var expiry = DateTime.Now.AddDays(2);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: expiry,
                signingCredentials: creds
            );

            var userToken = new UserToken
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiredAt = expiry.ToString(),
                Message = ""
            };
            return userToken;
        }
    }

    return new UserToken { Message = "Username or password is invalid" };
});

// add role
app.MapGet("/addrole/{username}/role/{rolename}",
    async (AppDbContext db, string username, string rolename) =>
{
    var role = db.Roles.Where(a => a.Name == rolename).FirstOrDefault();
    if (role is null) return Results.NotFound();

    var user = db.Users.Where(a => a.Username == username).FirstOrDefault();
    if (user is null) return Results.NotFound();

    var userRole = new UserRole { Role = role, User = user };
    await db.UserRoles.AddAsync(userRole);

    await db.SaveChangesAsync();

    return Results.Ok($"Role has been added. ID: {userRole.Id}");
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
});


app.MapGet("/admin", [Authorize(Roles = "Admin")]() =>
{
    return Results.Ok(new
    {
      Message="This content is only for admin"
    });
});

app.MapGet("/manager", [Authorize(Roles = "Manager")] () =>
{
    return Results.Ok(new
    {
        Message = "This content is only for manager"
    });
});

app.MapGet("/adminmanager",[Authorize(Roles = "Admin,Manager")]  () =>
{
    return Results.Ok(new
    {
        Message = "This content is only for admin and manager"
    });
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
