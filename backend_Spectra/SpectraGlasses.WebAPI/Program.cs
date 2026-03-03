using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Repositories.Basic;
using Repositories.Models;
using Services.GlassesService;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON serialization options with Unicode support
var jsonSerializerOptions = new JsonSerializerOptions
{
    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
};

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        // Enable Unicode support for Vietnamese and other languages
        options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
    });
// Add HttpClient for Firebase authentication
builder.Services.AddHttpClient();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "SpectraGlasses API", Version = "v1" });
    
    // Add JWT authentication support in Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });
    
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
});

// Configure DbContext
builder.Services.AddDbContext<Context>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register repositories
builder.Services.AddScoped<GenericRepository<User>>();
builder.Services.AddScoped<GenericRepository<Frame>>();
builder.Services.AddScoped<GenericRepository<FrameMedium>>();
builder.Services.AddScoped<GenericRepository<LensType>>();
builder.Services.AddScoped<GenericRepository<LensFeature>>();
builder.Services.AddScoped<GenericRepository<Order>>();
builder.Services.AddScoped<GenericRepository<OrderItem>>();
builder.Services.AddScoped<GenericRepository<Preorder>>();
builder.Services.AddScoped<GenericRepository<PreorderItem>>();
builder.Services.AddScoped<GenericRepository<Payment>>();
builder.Services.AddScoped<GenericRepository<Prescription>>();
builder.Services.AddScoped<GenericRepository<ComplaintRequest>>();

// Register services
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IFrameService, FrameService>();
builder.Services.AddScoped<IFrameMediaService, FrameMediaService>();
builder.Services.AddScoped<ILensTypeService, LensTypeService>();
builder.Services.AddScoped<ILensFeatureService, LensFeatureService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPreorderService, PreorderService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPrescriptionService, PrescriptionService>();
builder.Services.AddScoped<IComplaintRequestService, ComplaintRequestService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

builder.Services.AddAuthorization();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://localhost:3000",
                "https://localhost:5173",
                "https://localhost:3000"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
    
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}

// Global exception handler to ensure CORS headers are returned even on errors
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json; charset=utf-8";
        
        var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (error != null)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(error.Error, "An unhandled exception occurred: {Message}", error.Error.Message);
            
            await context.Response.WriteAsJsonAsync(new
            {
                ErrorCode = "INTERNAL_ERROR",
                Message = app.Environment.IsDevelopment() 
                    ? error.Error.Message 
                    : "An internal server error occurred"
            }, jsonSerializerOptions);
        }
    });
});

app.UseHttpsRedirection();

// CORS must be before Authentication/Authorization
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
