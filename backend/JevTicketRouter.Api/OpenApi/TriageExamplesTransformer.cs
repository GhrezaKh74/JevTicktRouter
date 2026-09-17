using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace JevTicketRouter.Api.OpenApi;

/// <summary>
/// Adds API metadata and worked request/response examples to the generated OpenAPI document, so the
/// Swagger UI is usable without reading the source.
/// </summary>
public sealed class TriageExamplesTransformer : IOpenApiDocumentTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Info = new OpenApiInfo
        {
            Title = "JevTicketRouter API",
            Version = "v1",
            Description =
                "Structured support-ticket triage. A ticket is evaluated by TypeSafe Jev in a single "
                + "batched call using the Choice, Score, and Noul primitives; deterministic .NET rules "
                + "then decide the final routing. Every response reports whether a value came from the "
                + "model or from a rule.\n\n"
                + "When no TYPESAFE_API_KEY is configured the API runs in Mock mode and returns "
                + "deterministic sample answers.",
            Contact = new OpenApiContact
            {
                Name = "JevTicketRouter",
                Url = new Uri("https://github.com/GhrezaKh74/JevTicktRouter"),
            },
            License = new OpenApiLicense
            {
                Name = "MIT",
                Url = new Uri("https://opensource.org/licenses/MIT"),
            },
        };

        ApplyTriageExamples(document);

        return Task.CompletedTask;
    }

    private static void ApplyTriageExamples(OpenApiDocument document)
    {
        if (document.Paths is null
            || !document.Paths.TryGetValue("/api/tickets/triage", out var pathItem)
            || pathItem.Operations is null
            || !pathItem.Operations.TryGetValue(HttpMethod.Post, out var operation))
        {
            return;
        }

        if (operation.RequestBody?.Content?.TryGetValue("application/json", out var requestMedia) == true)
        {
            requestMedia.Examples = new Dictionary<string, IOpenApiExample>
            {
                ["englishAccessRequest"] = new OpenApiExample
                {
                    Summary = "English access request",
                    Description = "A routine onboarding request that routes cleanly to Identity & Access.",
                    Value = new JsonObject
                    {
                        ["title"] = "Access to the reporting portal for a new analyst",
                        ["description"] =
                            "Our new business analyst started on Monday and needs read-only access to the "
                            + "quarterly reporting portal. Their manager has already approved the request.",
                        ["requesterRole"] = "InternalSupport",
                    },
                },
                ["persianTechnicalIssue"] = new OpenApiExample
                {
                    Summary = "Persian technical issue",
                    Description = "A Persian-language fault report. Jev handles either language.",
                    Value = new JsonObject
                    {
                        ["title"] = "خطا در ثبت تراکنش در سامانه شعبه",
                        ["description"] =
                            "از امروز صبح هنگام ثبت تراکنش در سامانه شعبه با خطا مواجه می‌شویم و صفحه لود نمی‌شود. "
                            + "کار باجه متوقف شده است.",
                        ["requesterRole"] = "BranchEmployee",
                    },
                },
            };
        }

        if (operation.Responses?.TryGetValue("200", out var okResponse) == true
            && okResponse.Content?.TryGetValue("application/json", out var responseMedia) == true)
        {
            responseMedia.Example = BuildResponseExample();
        }
    }

    private static JsonObject BuildResponseExample() => new()
    {
        ["ticketId"] = "9f1c2ab74e03",
        ["category"] = DecidedField("TechnicalIssue", 0.912, "JevModel", overridden: false),
        ["targetTeam"] = DecidedField("ApplicationSupport", 0.874, "JevModel", overridden: false),
        ["priority"] = DecidedField("High", 0.803, "JevModel", overridden: false),
        ["containsSensitiveData"] = new JsonObject
        {
            ["value"] = false,
            ["modelValue"] = false,
            ["confidence"] = null,
            ["origin"] = "JevModel",
            ["wasOverridden"] = false,
        },
        ["needsHumanReview"] = new JsonObject
        {
            ["value"] = false,
            ["modelValue"] = false,
            ["confidence"] = null,
            ["origin"] = "JevModel",
            ["wasOverridden"] = false,
        },
        ["routingSummary"] = "Auto-routed to ApplicationSupport at High priority.",
        ["appliedRules"] = new JsonArray(),
        ["jev"] = new JsonObject
        {
            ["mode"] = "Live",
            ["model"] = "jev-1.13.0",
            ["latencyMs"] = 412,
            ["priorityScore"] = 2.14,
            ["sensitiveDataProbability"] = 0.031,
            ["humanReviewProbability"] = 0.104,
            ["stateSummary"] = new JsonObject
            {
                ["title"] = "خطا در ثبت تراکنش در سامانه شعبه",
                ["description"] = "از امروز صبح هنگام ثبت تراکنش ...",
                ["requesterRole"] = "BranchEmployee",
                ["redacted"] = false,
            },
        },
    };

    private static JsonObject DecidedField(string value, double confidence, string origin, bool overridden) => new()
    {
        ["value"] = value,
        ["modelValue"] = value,
        ["confidence"] = confidence,
        ["origin"] = origin,
        ["wasOverridden"] = overridden,
    };
}
