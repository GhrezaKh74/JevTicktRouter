using System.Text.Json.Serialization;
using JevTicketRouter.Api;
using JevTicketRouter.Api.Endpoints;
using JevTicketRouter.Api.OpenApi;
using JevTicketRouter.Application;
using JevTicketRouter.Application.Tickets;
using JevTicketRouter.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Secrets come from User Secrets in development and from environment variables everywhere else.
// Nothing sensitive is ever committed to appsettings.json.
builder.Configuration.AddEnvironmentVariables();

const string FrontendCorsPolicy = "frontend";

builder.Services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy =>
{
    var origins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? ["http://localhost:5173"];

    policy.WithOrigins(origins)
        .AllowAnyHeader()
        .AllowAnyMethod();
}));

builder.Services
    .AddOptions<TriageOptions>()
    .Bind(builder.Configuration.GetSection(TriageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Enums travel as their names ("TechnicalIssue"), which keeps the API self-describing and lets
    // the React client use string unions directly.
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});

builder.Services.AddExceptionHandler<JevExceptionHandler>();

builder.Services.AddOpenApi(options => options.AddDocumentTransformer<TriageExamplesTransformer>());

var app = builder.Build();

app.Services.LogDecisionEngine();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors(FrontendCorsPolicy);

// The OpenAPI document and its UI are served in every environment: this is a portfolio project and
// the documentation is part of the deliverable. Lock this down before any real deployment.
app.MapOpenApi("/openapi/{documentName}.json");
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "JevTicketRouter API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "JevTicketRouter API";
});

app.MapTicketTriageEndpoints();
app.MapHealthEndpoints();
app.MapBenchmarkEndpoints();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

await app.RunAsync();

/// <summary>Exposed so the integration tests can spin the API up with <c>WebApplicationFactory</c>.</summary>
public partial class Program;
