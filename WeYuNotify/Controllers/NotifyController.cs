using Microsoft.AspNetCore.Mvc;
using WeYuNotify.interfaces;
using WeYuNotify.Models;

namespace WeYuNotify.Controllers;

[ApiController]
[Route("api/notify")]
public class NotifyController : ControllerBase
{
    private readonly ITelegramNotifyService _telegramService;
    private readonly IEmailNotifyService _emailNotifyService;
    private readonly ILogger<NotifyController> _logger;

    public NotifyController(
        ILogger<NotifyController> logger,
        ITelegramNotifyService telegramService,
        IEmailNotifyService emailNotifyService)
    {
        _logger = logger;
        _telegramService = telegramService;
        _emailNotifyService = emailNotifyService;
    }

    // ----------------------
    // Telegram
    // ----------------------
    [HttpPost("telegram")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> PostTelegram([FromBody] TelegramNotifyRequest request, CancellationToken ct)
        => HandleAsync(
            request,
            summaryBuilder: BuildRequestSummarySafe,
            doSendAsync: _telegramService.SendAsync,
            success: r => Ok(new
            {
                message = "sent",
                subject = r.Subject,
                bodyPreview = r.Body,
                logId = r.LogId
            }),
            failure: r =>
            {
                var status = r.Stage switch
                {
                    FailureStage.Validation => StatusCodes.Status400BadRequest,
                    FailureStage.HttpSend   => StatusCodes.Status502BadGateway,
                    _                       => StatusCodes.Status500InternalServerError
                };
                return StatusCode(status, new
                {
                    message = "failed",
                    stage = r.Stage?.ToString(),
                    error = r.Error,
                    httpStatus = r.HttpStatus,
                    logId = r.LogId
                });
            },
            ct);

    /// <summary>（向後相容）GET 版 Telegram 發送：建議改用 POST</summary>
    [HttpGet("telegram")]
    public Task<IActionResult> GetTelegram([FromQuery] TelegramNotifyRequest request, CancellationToken ct)
        => PostTelegram(request, ct);

    // ----------------------
    // 單一 Email
    // ----------------------
    [HttpPost("email")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> PostEmail([FromBody] EmailNotifyRequest request, CancellationToken ct)
        => HandleAsync(
            request,
            summaryBuilder: BuildEmailRequestSummarySafe,
            doSendAsync: _emailNotifyService.SendAsync,
            success: r => Ok(new { message = "sent", logId = r.LogId }),
            failure: r => StatusCode(StatusCodes.Status400BadRequest, new
            {
                message = "failed",
                error = r.Error,
                logId = r.LogId
            }),
            ct);

    /// <summary>（向後相容）GET 版單一 Email：建議改用 POST</summary>
    [HttpGet("singleEmail")]
    public Task<IActionResult> GetEmail([FromQuery] EmailNotifyRequest request, CancellationToken ct)
        => PostEmail(request, ct);

    // ----------------------
    // 群組 Email
    // ----------------------
    [HttpPost("groupEmail")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> PostGroupEmail([FromBody] GroupEmailNotifyRequest request, CancellationToken ct)
        => HandleAsync(
            request,
            summaryBuilder: BuildGroupEmailRequestSummarySafe,
            doSendAsync: _emailNotifyService.SendGroupAsync,
            success: r => Ok(new { message = "sent", count = r.SentCount, logId = r.LogId }),
            failure: r => StatusCode(StatusCodes.Status400BadRequest, new
            {
                message = "failed",
                error = r.Error,
                logId = r.LogId
            }),
            ct);

    /// <summary>（向後相容）GET 版群組 Email：建議改用 POST</summary>
    [HttpGet("groupEmail")]
    public Task<IActionResult> GetGroupEmail([FromQuery] GroupEmailNotifyRequest request, CancellationToken ct)
        => PostGroupEmail(request, ct);

    // ----------------------
    // 共用樣板：Scope + 驗證 + 例外處理 + 統一記錄
    // ----------------------

    private async Task<IActionResult> HandleAsync<TRequest, TResult>(
        TRequest request,
        Func<TRequest, object> summaryBuilder,
        Func<TRequest, CancellationToken, Task<TResult>> doSendAsync,
        Func<TResult, IActionResult> success,
        Func<TResult, IActionResult> failure,
        CancellationToken ct)
        where TRequest : class
    {
        // ---- 建 Scope（每個 log 都會帶這些欄位）----
        var requestId = HttpContext.Request.Headers.TryGetValue("X-Request-Id", out var rid)
            ? rid.ToString()
            : HttpContext.TraceIdentifier;

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["requestId"] = requestId,
            ["route"]     = HttpContext.Request.Path.Value,
            ["method"]    = HttpContext.Request.Method,
            ["remoteIp"]  = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ["userAgent"] = HttpContext.Request.Headers.UserAgent.ToString()
        });

        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            // `ApiController` 會自動驗證 ModelState，但我們想記錄更友善的 log
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(kv => kv.Value?.Errors?.Count > 0)
                    .Select(kv => new
                    {
                        Field = kv.Key,
                        Messages = kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                    })
                    .ToArray();

                _logger.LogWarning("Validation failed: {Errors}", errors);
                return ValidationProblem(ModelState);
            }

            _logger.LogInformation("-----------------------------------------------------------------------------------------------------------------------------");
            _logger.LogInformation("Incoming request: {Summary}", summaryBuilder(request));

            var result = await doSendAsync(request, ct).ConfigureAwait(false);
            var elapsedMs = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;

            // 反射抓取可能存在的 LogId / Stage / Error（服務層欄位命名不一致也能帶出來）
            var logId   = result?.GetType().GetProperty("LogId")?.GetValue(result, null);
            var stage   = result?.GetType().GetProperty("Stage")?.GetValue(result, null);
            var successProp = result?.GetType().GetProperty("Success")?.GetValue(result, null) as bool?;

            if (successProp == true)
            {
                _logger.LogInformation("Send OK. logId={LogId}, elapsedMs={Elapsed}", logId, elapsedMs);
                return success(result!);
            }

            var error   = result?.GetType().GetProperty("Error")?.GetValue(result, null);
            var httpSt  = result?.GetType().GetProperty("HttpStatus")?.GetValue(result, null);

            // 失敗級別分流（Server 5xx / Upstream/Client 4xx）
            if (stage?.ToString() is "HttpSend" or "Validation")
                _logger.LogWarning("Send failed: stage={Stage}, error={Error}, http={Http}, logId={LogId}",
                    stage, error, httpSt, logId);
            else
                _logger.LogError("Send failed (server-side): stage={Stage}, error={Error}, http={Http}, logId={LogId}",
                    stage, error, httpSt, logId);

            return failure(result!);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Request cancelled by client or server token.");
            return StatusCode(StatusCodes.Status499ClientClosedRequest, new { message = "cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during notify.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "failed",
                stage = "UnhandledException"
            });
        }
        finally
        {
            var totalMs = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
            _logger.LogInformation("Request completed in {Elapsed} ms", totalMs);
        }
    }

    // ----------------------
    // 安全的摘要/預覽建構
    // ----------------------

    private static object BuildRequestSummarySafe(TelegramNotifyRequest req)
        => new
        {
            req?.ChatId,
            SubjectLength = req?.Subject?.Length ?? 0,
            BodyLength = req?.Body?.Length ?? 0
        };

    private static object BuildEmailRequestSummarySafe(EmailNotifyRequest req)
        => new
        {
            req?.Email,
            SubjectLength = req?.Subject?.Length ?? 0,
            BodyLength = req?.Body?.Length ?? 0
        };

    private static object BuildGroupEmailRequestSummarySafe(GroupEmailNotifyRequest req)
        => new
        {
            req?.GroupId,
            SubjectLength = req?.Subject?.Length ?? 0,
            BodyLength = req?.Body?.Length ?? 0
        };
}
