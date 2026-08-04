using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MovieBooking.Application;
using MovieBooking.Application.Common.Configuration;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Infrastructure;
using MovieBooking.Infrastructure.Persistence;
using MovieBooking.Hubs;
using MovieBooking.Realtime;
using System.Text;

System.AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException($"Configuration section '{JwtOptions.SectionName}' is missing.");
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs/seats"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var realtimeOrigins = builder.Configuration.GetSection("Realtime:AllowedOrigins").Get<string[]>() ?? [];
if (!builder.Environment.IsDevelopment() && realtimeOrigins.Length == 0)
{
    throw new InvalidOperationException("Realtime:AllowedOrigins must contain at least one exact origin outside development.");
}
if (builder.Environment.IsDevelopment() && realtimeOrigins.Length == 0)
{
    realtimeOrigins = ["http://localhost:3000", "http://localhost:5000", "http://localhost:8080"];
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("ApplicationCors", policy =>
    {
        policy
            .WithOrigins(realtimeOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("X-Seat-State-Version");
    });
});

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddScoped<ISeatRealtimePublisher, SignalRSeatRealtimePublisher>();
builder.Services.AddProblemDetails();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    const string securityId = "Bearer";
    options.AddSecurityDefinition(securityId, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(securityId, document)] = []
    });
});

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

if (builder.Configuration.GetValue<bool>("Database:SeedOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await DbSeeder.SeedAsync(db, passwordHasher, builder.Configuration);
}


// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseCors("ApplicationCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<SeatHub>("/hubs/seats");

app.Run();
