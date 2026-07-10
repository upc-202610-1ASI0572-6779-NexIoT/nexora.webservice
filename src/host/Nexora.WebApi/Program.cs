using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Nexora.Application.Commands.Telemetry;
using Nexora.Application.Services;
using Nexora.Domain.Repositories;
using Nexora.Domain.Services;
using Nexora.Infrastructure.Persistence;
using Nexora.Infrastructure.Repositories;
using Nexora.Shared.Domain.Api;
using Nexora.WebApi.Middleware;
using Stripe;

var builder = WebApplication.CreateBuilder(args);
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
var supportedCultures = new[] { new CultureInfo("es"), new CultureInfo("en") };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders = new[]
    {
        new AcceptLanguageHeaderRequestCultureProvider()
    };
});

// Add services to the container.
builder.Services.AddDbContext<NexoraDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
// Authentication & JWT
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.ASCII.GetBytes(jwtSettings["Key"]!);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});
// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("Content-Disposition", "X-Total-Count");
    });
});
// Repositories & UoW
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddScoped<ITelemetryLogRepository, TelemetryLogRepository>();
builder.Services.AddScoped<IAlertRepository, AlertRepository>();
builder.Services.AddScoped<IPropertyRepository, PropertyRepository>();
builder.Services.AddScoped<IMaintenanceTicketRepository, MaintenanceTicketRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ILandlordRepository, LandlordRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
// Property code generator and backfill
builder.Services.AddScoped<Nexora.Domain.Services.IPropertyCodeGenerator, Nexora.Infrastructure.Services.PropertyCodeGenerator>();
builder.Services.AddScoped<Nexora.Infrastructure.Services.PropertyCodeBackfillService>();
builder.Services.AddScoped<Nexora.WebApi.Seeding.DataSeeder>();
// Use Cases & Processors
builder.Services.AddScoped<ITelemetryProcessor, TelemetryProcessor>();
builder.Services.AddScoped<IReportService, Nexora.Infrastructure.Services.ReportService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISubscriptionPolicy, Nexora.Application.Services.SubscriptionPolicy>();
// MediatR Configuration
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblies(
        typeof(ProcessTelemetryCommand).Assembly,
        typeof(Nexora.Application.Commands.Property.CreatePropertyCommand).Assembly
    ));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    e => e.Key,
                    e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray()
                );

            var response = new ValidationErrorResponse(
                "ValidationFailed",
                "One or more validation errors occurred.",
                errors);

            return new BadRequestObjectResult(response);
        };
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Nexora API",
        Version = "v1",
        Description = "REST API for the Nexora IoT platform. Provides device management, telemetry ingestion, " +
                      "consumption analytics, subscription billing (Stripe), and alert maintenance workflows.",
        Contact = new OpenApiContact { Name = "Nexora Team" }
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. " +
                      "Obtain a token via POST /api/v1/auth/login or /api/v1/auth/register/landlords, " +
                      "then enter: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
    });

    // Include XML comments from all assemblies
    var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml", SearchOption.TopDirectoryOnly);
    foreach (var xmlFile in xmlFiles)
    {
        c.IncludeXmlComments(xmlFile, includeControllerXmlComments: true);
    }
});

var app = builder.Build();
// Apply migrations and run data seeding
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();

    try
    {
        // Apply all pending migrations.
        // The MakePropertyCodeNotNullAndUnique migration includes a SQL backfill for existing rows,
        // so it works correctly whether applied via CLI or at startup.
        if (context.Database.GetPendingMigrations().Any())
        {
            context.Database.Migrate();
        }
        else
        {
            context.Database.EnsureCreated();
        }

        // Safety net: backfill any properties that might still have null codes (idempotent)
        var backfill = scope.ServiceProvider.GetService<Nexora.Infrastructure.Services.PropertyCodeBackfillService>();
        if (backfill != null)
        {
            backfill.EnsurePropertyCodesAsync().GetAwaiter().GetResult();
        }

        // Seed initial data if needed
        var seeder = scope.ServiceProvider.GetService<Nexora.WebApi.Seeding.DataSeeder>();
        if (seeder != null)
        {
            seeder.EnsureSeedDataAsync().GetAwaiter().GetResult();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migration/seeding error: {ex.Message}");
        throw;
    }
}
// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseRequestLocalization();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Nexora API v1");
    c.RoutePrefix = "swagger";
});
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
