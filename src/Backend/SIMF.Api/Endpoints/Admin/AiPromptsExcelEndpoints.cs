// Tests: SIMF.Api.Tests/AiPromptsExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Ai.Abstractions;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Ai;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/ai/prompts/export</c> — the D-356 grid export for the
/// AI prompt catalogue. All the work lives in
/// <see cref="AdminGridExportEndpoint{TRow}"/>; this subclass only declares the
/// route, permission, sheet/file names, the column layout (mirroring the
/// AiPrompts grid) and how to list + identify a prompt row.
/// </summary>
public sealed class ExportAiPromptsEndpoint(IAdminAiPromptService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminAiPromptSummary>(exporter)
{
    protected override string RoutePath => "/admin/ai/prompts/export";
    protected override string Permission => PermissionCatalog.AiPrompts.Export;
    protected override string SheetName => "AiPrompts";
    protected override string FilePrefix => "simf-ai-prompts";

    protected override IReadOnlyList<GridExcelColumn<AdminAiPromptSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminAiPromptSummary>> _columns =
    [
        new("Key", row => row.Key),
        new("Feature", row => row.Feature.ToString()),
        new("DisplayName", row => row.DisplayName),
        new("DisplayNameArabic", row => row.DisplayNameArabic),
        new("Provider", row => row.Provider.ToString()),
        new("Model", row => row.Model),
        new("Temperature", row => row.Temperature),
        new("MaxOutputTokens", row => row.MaxOutputTokens),
        new("Version", row => row.Version),
        new("IsActive", row => row.IsActive),
    ];

    protected override async Task<IReadOnlyList<AdminAiPromptSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAsync(query, ct)).Items;

    protected override Guid IdOf(AdminAiPromptSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/ai/prompts/import</c> — the D-356 grid import
/// (insert-only) for the AI prompt catalogue. The base does the upload defence,
/// parse and per-row error aggregation; this subclass binds one row to
/// <see cref="CreateAiPromptRequest"/> and creates it (the service rejects a
/// duplicate key or an invalid field → a per-row error, not a batch abort).
/// SystemPrompt + UserPromptTemplate are not on the light grid, so they live in
/// extra import columns the admin fills in before re-uploading.
/// </summary>
public sealed class ImportAiPromptsEndpoint(IAdminAiPromptService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/ai/prompts/import";
    protected override string Permission => PermissionCatalog.AiPrompts.Import;
    protected override string SheetName => "AiPrompts";
    protected override IReadOnlyList<string> RequiredHeaders =>
        ["Key", "Feature", "DisplayName", "DisplayNameArabic", "Provider", "Model",
         "SystemPrompt", "UserPromptTemplate"];

    protected override string? RowKey(GridImportRow row) =>
        row.Cells.TryGetValue("Key", out var key) ? key : null;

    protected override async Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct)
    {
        var key = row.Cells.GetValueOrDefault("Key", string.Empty);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new DataValidationException(
                "The key is required.",
                "المفتاح مطلوب.");
        }

        var displayName = row.Cells.GetValueOrDefault("DisplayName", string.Empty);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DataValidationException(
                "The English display name is required.",
                "الاسم المعروض بالإنجليزية مطلوب.");
        }

        var displayNameArabic = row.Cells.GetValueOrDefault("DisplayNameArabic", string.Empty);
        if (string.IsNullOrWhiteSpace(displayNameArabic))
        {
            throw new DataValidationException(
                "The Arabic display name is required.",
                "الاسم المعروض بالعربية مطلوب.");
        }

        var model = row.Cells.GetValueOrDefault("Model", string.Empty);
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new DataValidationException(
                "The model is required.",
                "النموذج مطلوب.");
        }

        var systemPrompt = row.Cells.GetValueOrDefault("SystemPrompt", string.Empty);
        if (string.IsNullOrWhiteSpace(systemPrompt))
        {
            throw new DataValidationException(
                "The system prompt is required.",
                "محفّز النظام مطلوب.");
        }

        var userPromptTemplate = row.Cells.GetValueOrDefault("UserPromptTemplate", string.Empty);
        if (string.IsNullOrWhiteSpace(userPromptTemplate))
        {
            throw new DataValidationException(
                "The user prompt template is required.",
                "قالب محفّز المستخدم مطلوب.");
        }

        var featureRaw = row.Cells.GetValueOrDefault("Feature", string.Empty);
        if (!Enum.TryParse<AiFeature>(featureRaw, ignoreCase: true, out var feature))
        {
            throw new DataValidationException(
                $"The feature '{featureRaw}' is not recognised.",
                $"الميزة '{featureRaw}' غير معروفة.");
        }

        var providerRaw = row.Cells.GetValueOrDefault("Provider", string.Empty);
        if (!Enum.TryParse<AiProvider>(providerRaw, ignoreCase: true, out var provider))
        {
            throw new DataValidationException(
                $"The provider '{providerRaw}' is not recognised.",
                $"المزوّد '{providerRaw}' غير معروف.");
        }

        var request = new CreateAiPromptRequest
        {
            Key = key,
            Feature = feature,
            DisplayName = displayName,
            DisplayNameArabic = displayNameArabic,
            Description = NullIfBlank(row.Cells.GetValueOrDefault("Description", string.Empty)),
            DescriptionArabic = NullIfBlank(row.Cells.GetValueOrDefault("DescriptionArabic", string.Empty)),
            Provider = provider,
            Model = model,
            SystemPrompt = systemPrompt,
            UserPromptTemplate = userPromptTemplate,
        };

        var temperatureRaw = row.Cells.GetValueOrDefault("Temperature", string.Empty);
        if (double.TryParse(temperatureRaw, out var temperature))
        {
            request.Temperature = temperature;
        }

        var maxTokensRaw = row.Cells.GetValueOrDefault("MaxOutputTokens", string.Empty);
        if (int.TryParse(maxTokensRaw, out var maxTokens))
        {
            request.MaxOutputTokens = maxTokens;
        }

        await service.CreateAsync(actorId, request, ct);
        return GridRowApplyKind.Created;
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
