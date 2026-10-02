using NotificationForwarder.Api.Common.DependencyInjection;
using NotificationForwarder.Application;
using NotificationForwarder.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Intake hardening: small bodies, shallow JSON, no duplicate keys, no error echoes.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024);
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.AllowInputFormatterExceptionMessages = false;
    options.JsonSerializerOptions.MaxDepth = 8;
    options.JsonSerializerOptions.AllowDuplicateProperties = false;
});
builder.Services.AddProblemDetails();
builder.Services.AddApiHealthChecks();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.MapApiHealthChecks();
app.MapControllers();

await app.RunAsync();

// Exposes the entry point to WebApplicationFactory in the test projects.
public partial class Program;
