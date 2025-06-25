using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OrderManagementSystem.API.Data;
using OrderManagementSystem.API.IService;
using OrderManagementSystem.API.Service;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddDbContext<OrderContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OrderManagementSystem",
        Version = "v1",
        Description = "A comprehensive API for handling orderManagementSystem with database logging",
        Contact = new OpenApiContact
        {
            Name = "API Support",
            Email = "support@orderManagementSystem.com"
        }
    });

});

builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddSingleton<IRabbitMQService, RabbitMQService>();
builder.Services.AddHostedService<OrderProcessingService>();

builder.Services.AddControllers();
builder.Services.AddRazorPages();

// Add CORS for API access
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OrderManagementSystem V1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "OrderManagementSystem Documentation";
        c.DefaultModelsExpandDepth(-1);
        c.DisplayRequestDuration();
    });

    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors();

app.MapControllers();
app.MapRazorPages();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<OrderContext>();
    context.Database.EnsureCreated();
}

app.Run();