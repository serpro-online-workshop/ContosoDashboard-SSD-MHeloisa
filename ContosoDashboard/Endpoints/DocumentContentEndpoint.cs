using System.Security.Claims;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.Net.Http.Headers;

namespace ContosoDashboard.Endpoints;

public static class DocumentContentEndpoint
{
    public static IEndpointRouteBuilder MapDocumentContentEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/document-content/{documentId}", HandleAsync).AllowAnonymous();
        return endpoints;
    }

    public static async Task<IResult> HandleAsync(
        string documentId,
        HttpContext http,
        IDocumentQueryService queryService,
        IDocumentFileStorage storage,
        IDocumentAuditService audit,
        ILoggerFactory loggerFactory)
    {
        if (http.User.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();
        if (!Guid.TryParseExact(documentId, "D", out var id) && !Guid.TryParseExact(documentId, "N", out id))
            return Results.BadRequest();

        var mode = http.Request.Query.TryGetValue("mode", out var value) ? value.ToString() : "download";
        if (mode is not ("download" or "inline")) return Results.BadRequest();
        var document = await queryService.GetDocumentForUserAsync(id, http.User, http.RequestAborted);
        if (document is null) return Results.NotFound();
        if (mode == "inline" && document.DetectedContentType is not ("application/pdf" or "image/jpeg" or "image/png"))
            return Results.BadRequest();

        Stream stream;
        try
        {
            stream = await storage.OpenReadAsync(document.StorageKey, http.RequestAborted);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            loggerFactory.CreateLogger("DocumentContent").LogWarning(
                "Content operation failed for document {DocumentId} ({FailureType}).",
                id, exception.GetType().Name);
            return Results.NotFound();
        }

        await using (stream)
        {
            var actorClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(actorClaim, out var actorId))
                return Results.NotFound();
            try
            {
                await audit.RecordAsync(id, actorId,
                    mode == "inline" ? DocumentActivityAction.Preview : DocumentActivityAction.Download,
                    document.DetectedContentType, document.UploaderUserId);
            }
            catch (Exception exception)
            {
                loggerFactory.CreateLogger("DocumentContent").LogError(
                    "Content audit recording failed ({FailureType}).", exception.GetType().Name);
                return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
            }

            http.Response.StatusCode = StatusCodes.Status200OK;
            http.Response.ContentType = document.DetectedContentType;
            http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            http.Response.Headers[HeaderNames.ContentDisposition] =
                new ContentDispositionHeaderValue(mode == "inline" ? "inline" : "attachment")
                {
                    FileNameStar = document.OriginalFileName
                }.ToString();
            http.Response.ContentLength = document.SizeBytes;
            try
            {
                await stream.CopyToAsync(http.Response.Body, 64 * 1024, http.RequestAborted);
            }
            catch (IOException exception)
            {
                loggerFactory.CreateLogger("DocumentContent").LogError(
                    "Content stream failed for document {DocumentId} ({FailureType}).",
                    id, exception.GetType().Name);
                if (!http.Response.HasStarted)
                    return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        return Results.Empty;
    }
}
