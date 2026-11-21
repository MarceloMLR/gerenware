using System.ComponentModel;
using GerenWare.Files.Contracts;
using GerenWare.Projects.Application;
using GerenWare.Projects.Contracts;
using GerenWare.Projects.Domain;
using GerenWare.Users.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace GerenWare.Projects.Endpoints;

/// <summary>When and by whom the last report of the project was generated.</summary>
public sealed record StatusReportLastResponse(DateTime GeneratedAt, OverviewPersonResponse GeneratedBy);

/// <summary>An open ticket of the project that may go in as a risk, and whether it threatens milestones (ADR-0164).</summary>
public sealed record StatusReportTicketResponse(int Number, string Title, ReportTicketStatus Status, bool Threatens);

/// <summary>A risk of the report: the ticket, its level and the point of attention.</summary>
public sealed record StatusReportRiskResponse(int Number, RiskLevel Level, string Attention);

/// <summary>
/// The draft of a new status report (ADR-0165): the last report, its four texts, the open tickets of the project and
/// the risks suggested, the ones of the last report still open and the tickets that threaten milestones; the format of
/// the last report, the PowerPoint at the first one, and whether the PDF can be made, only with the files in SharePoint.
/// </summary>
public sealed record StatusReportDraftResponse(
    StatusReportLastResponse? Last,
    string Summary,
    string Focus,
    string NextSteps,
    string ClientNeeds,
    IReadOnlyList<StatusReportTicketResponse> Tickets,
    IReadOnlyList<StatusReportRiskResponse> Risks,
    StatusReportFormat Format,
    bool PdfAvailable);

/// <summary>A risk chosen: the number of an open ticket of the project, its level and a point of attention up to 500 characters.</summary>
public sealed record StatusReportRiskRequest(int Number, RiskLevel? Level, string? Attention);

/// <summary>What the person wrote and chose to generate a status report (ADR-0165, ADR-0166).</summary>
public sealed record StatusReportRequest(
    [property: Description("The summary of the period, up to 1,000 characters.")] string? Summary,
    [property: Description("The current focus, up to 1,000 characters.")] string? Focus,
    [property: Description("The next steps, up to 1,000 characters.")] string? NextSteps,
    [property: Description("What the client must do, up to 1,000 characters.")] string? ClientNeeds,
    [property: Description("From 0 to 10 open tickets of the project, without repeating, each with its level.")] IReadOnlyList<StatusReportRiskRequest>? Risks,
    [property: Description("Whether the hours of the project go in, only the totals (ADR-0166).")] bool ShowHours = false,
    [property: Description("PowerPoint, the default, or Pdf, converted by Microsoft 365, only with the files in SharePoint (ADR-0165).")]
    StatusReportFormat? Format = null);

/// <summary>The report kept in the documents of the project: the document, its folder, its name and its version.</summary>
public sealed record StatusReportResponse(Guid DocumentId, Guid FolderId, string FolderName, string Name, int Version);

/// <summary>Problem types of the status report, so the screen can tell the reason (ADR-0165).</summary>
public static class StatusReportProblems
{
    /// <summary>The PowerPoint is larger than the conversion into PDF takes; the extension maxBytes tells the limit; status 409.</summary>
    public const string TooLargeForPdf = "urn:gerenware:problem:status-report-too-large-for-pdf";

    /// <summary>Microsoft 365 did not convert the report into PDF, and nothing was kept; status 502.</summary>
    public const string PdfFailed = "urn:gerenware:problem:status-report-pdf-failed";
}

/// <summary>The status report of a project (ADR-0165, ADR-0166), for the Administrator and the manager.</summary>
internal static partial class StatusReportEndpoints
{
    public static void MapStatusReportEndpoints(this RouteGroupBuilder projects)
    {
        projects.MapGet("/{id:guid}/status-report", DraftAsync).WithName("GetStatusReportDraft")
            .WithDescription(
                "The draft of a new status report of the project: the last texts, the open tickets, the risks suggested and the format (ADR-0165).")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        projects.MapPost("/{id:guid}/status-report", GenerateAsync).WithName("GenerateStatusReport")
            .WithDescription(
                "Generates the status report of the project in PowerPoint or PDF and keeps it in the folder of the status reports, in any status of the project (ADR-0165, ADR-0166).")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway);
    }

    private static async Task<Results<Ok<StatusReportDraftResponse>, NotFound, ForbidHttpResult>> DraftAsync(
        Guid id, HttpContext http, IUserAccess access, GetStatusReportDraft draft, CancellationToken cancellationToken)
    {
        var viewer = await ProjectEndpoints.ViewerOfAsync(http, access, cancellationToken);
        var (result, outcome) = await draft.ExecuteAsync(id, viewer, cancellationToken);
        return outcome switch
        {
            StatusReportOutcome.Forbidden => TypedResults.Forbid(),
            StatusReportOutcome.Done when result is not null => TypedResults.Ok(new StatusReportDraftResponse(
                result.Last is { } last
                    ? new StatusReportLastResponse(last.GeneratedAt, new OverviewPersonResponse(last.GeneratedById, last.GeneratedBy))
                    : null,
                result.Texts.Summary,
                result.Texts.Focus,
                result.Texts.NextSteps,
                result.Texts.ClientNeeds,
                [.. result.Tickets.Select(ticket => new StatusReportTicketResponse(ticket.Number, ticket.Title, ticket.Status, ticket.Threatens))],
                [.. result.Risks.Select(risk => new StatusReportRiskResponse(risk.TicketNumber, risk.Level, risk.Attention))],
                result.Format,
                result.PdfAvailable)),
            _ => TypedResults.NotFound(),
        };
    }

    private static async Task<Results<Ok<StatusReportResponse>, NotFound, ForbidHttpResult, ValidationProblem, ProblemHttpResult>> GenerateAsync(
        Guid id,
        StatusReportRequest request,
        HttpContext http,
        IUserAccess access,
        GenerateStatusReport generate,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var viewer = await ProjectEndpoints.ViewerOfAsync(http, access, cancellationToken);
        var result = await generate.ExecuteAsync(
            id,
            viewer,
            new StatusReportTexts(request.Summary ?? string.Empty, request.Focus ?? string.Empty, request.NextSteps ?? string.Empty, request.ClientNeeds ?? string.Empty),
            request.Risks?.Select(risk => new StatusReportRiskInput(risk.Number, risk.Level, risk.Attention)).ToList(),
            request.ShowHours,
            request.Format,
            cancellationToken);
        if (result.ConversionFailure is { Failure: FileConversionFailure.Refused } refused)
        {
            LogPdfRefused(loggers.CreateLogger(typeof(StatusReportEndpoints)), refused.Status, refused.Code);
        }

        return result.Outcome switch
        {
            StatusReportOutcome.Done when result.Saved is { } saved => TypedResults.Ok(
                new StatusReportResponse(saved.DocumentId, saved.FolderId, saved.FolderName, saved.Name, saved.Version)),
            StatusReportOutcome.Forbidden => TypedResults.Forbid(),
            StatusReportOutcome.Invalid => TypedResults.ValidationProblem(
                (result.Invalid ?? []).ToDictionary(FieldOf, MessageOf)),
            StatusReportOutcome.TooLargeForPdf => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Too large for the PDF",
                detail: "The PowerPoint of the report is larger than the conversion into PDF takes; generate it in PowerPoint.",
                type: StatusReportProblems.TooLargeForPdf,
                extensions: new Dictionary<string, object?> { ["maxBytes"] = FileConversion.MaxBytes }),
            StatusReportOutcome.PdfFailed => TypedResults.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "PDF not made",
                detail: "Microsoft 365 did not convert the report into PDF, and nothing was kept; try again or generate it in PowerPoint.",
                type: StatusReportProblems.PdfFailed),
            _ => TypedResults.NotFound(),
        };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Microsoft 365 refused the PDF of a status report: {Status} {Code}.")]
    private static partial void LogPdfRefused(ILogger logger, int? status, string? code);

    private static string FieldOf(StatusReportField field) => field switch
    {
        StatusReportField.Summary => "summary",
        StatusReportField.Focus => "focus",
        StatusReportField.NextSteps => "nextSteps",
        StatusReportField.ClientNeeds => "clientNeeds",
        StatusReportField.Format => "format",
        _ => "risks",
    };

    private static string[] MessageOf(StatusReportField field) => field switch
    {
        StatusReportField.Risks =>
            ["From 0 to 10 open tickets of the project, without repeating, each with its level and a point of attention up to 500 characters."],
        StatusReportField.Format => ["PowerPoint, or Pdf only with the files of the installation in SharePoint."],
        _ => ["Up to 1,000 characters, without control characters."],
    };
}
