using System.Web;
using Microsoft.AspNetCore.Mvc;
using WeYuTelegramNotify.interfaces;
using WeYuTelegramNotify.Models;

namespace WeYuTelegramNotify.Controllers;

[ApiController]
[Route("api/notify")]
public class NotifyController : ControllerBase
{
    private readonly ITelegramNotifyService _telegramService;
    private readonly IEmailNotifyService _emailNotifyService;
    private readonly ILogger<NotifyController> _logger;

    public NotifyController(ILogger<NotifyController> logger, ITelegramNotifyService telegramService, IEmailNotifyService emailNotifyService)
    {
        _logger = logger;
        _telegramService = telegramService;
        _emailNotifyService = emailNotifyService;
    }

    [HttpGet("telegram")]
    public Task<IActionResult> GetTelegram([FromQuery] TelegramNotifyRequest request, CancellationToken cancellationToken)
        => SendTelegramAsync(request, cancellationToken);
    
    /// <summary>
    /// 單一發信
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("singleEmail")]
    public Task<IActionResult> SendEmail([FromQuery] EmailNotifyRequest request, CancellationToken cancellationToken)
        => SendEmailAsync(request, cancellationToken);

    /// <summary>
    /// 依群組發信
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("groupEmail")]
    public Task<IActionResult> SendGroupEmail([FromQuery] GroupEmailNotifyRequest request, CancellationToken cancellationToken)
        => SendGroupEmailAsync(request, cancellationToken);

    private async Task<IActionResult> SendTelegramAsync(TelegramNotifyRequest request, CancellationToken cancellationToken)
    {
        // ---- 建立可追蹤的 scope：把 requestId / route / user-agent 等塞進 scope，之後每筆 log 都會帶到 ----
        var requestId = HttpContext.Request.Headers.TryGetValue("X-Request-Id", out var rid)
                        ? rid.ToString()
                        : HttpContext.TraceIdentifier;

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["requestId"]   = requestId,
            ["route"]       = HttpContext.Request.Path.Value,
            ["method"]      = HttpContext.Request.Method,
            ["remoteIp"]    = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ["userAgent"]   = HttpContext.Request.Headers.UserAgent.ToString()
        });

        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            // ---- 紀錄收到請求（避免敏感資訊外洩，訊息內容採長度/哈希或摘要而非原文）----
            _logger.LogInformation("Incoming telegram notify request: {Summary}",
                BuildRequestSummarySafe(request));

            if (!ModelState.IsValid)
            {
                // 把 ModelState 錯誤展平成 list，利於查詢
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

            var result = await _telegramService.SendAsync(request, cancellationToken);

            var elapsedMs = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;

            if (result.Success)
            {
                _logger.LogInformation(
                    "Telegram sent successfully. logId={LogId}, subject={Subject}, elapsedMs={Elapsed}",
                    result.LogId, result.Subject, elapsedMs);

                return Ok(new
                {
                    message = "sent",
                    subject = result.Subject,
                    body = HttpUtility.HtmlDecode(result.Body), // 給前端回預覽（若要避免 XSS，建議僅回純文字或加上 allow-list）
                    logId = result.LogId
                });
            }

            // 失敗依 Stage 分級 & 記錄更多上下文
            var status = result.Stage switch
            {
                FailureStage.Validation => StatusCodes.Status400BadRequest,
                FailureStage.HttpSend   => StatusCodes.Status502BadGateway,
                FailureStage.DbWrite    => StatusCodes.Status500InternalServerError,
                _                       => StatusCodes.Status500InternalServerError
            };

            // 以嚴重度區分 log level
            var logProps = new
            {
                result.Stage,
                result.Error,
                result.HttpStatus,
                result.LogId,
                elapsedMs
            };

            if (status >= 500)
            {
                _logger.LogError("Telegram notify failed (server-side). {@Props}", logProps);
            }
            else
            {
                _logger.LogWarning("Telegram notify failed (client/upstream). {@Props}", logProps);
            }

            return StatusCode(status, new
            {
                message = "failed",
                stage = result.Stage?.ToString(),
                error = result.Error,
                httpStatus = result.HttpStatus,
                logId = result.LogId
            });
        }
        catch (OperationCanceledException)
        {
            // 取消也應有一致的紀錄
            _logger.LogWarning("Request cancelled by client or server token.");
            return StatusCode(StatusCodes.Status499ClientClosedRequest, new
            {
                message = "cancelled"
            });
        }
        catch (Exception ex)
        {
            // 未預期例外：一定要 Fatal/Error 級別 + 帶 scope
            _logger.LogError(ex, "Unhandled exception during telegram notify.");
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
    
    private async Task<IActionResult> SendEmailAsync(EmailNotifyRequest request, CancellationToken cancellationToken)
    {
        var requestId = HttpContext.Request.Headers.TryGetValue("X-Request-Id", out var rid)
                        ? rid.ToString()
                        : HttpContext.TraceIdentifier;

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["requestId"] = requestId,
            ["route"] = HttpContext.Request.Path.Value,
            ["method"] = HttpContext.Request.Method,
            ["remoteIp"] = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ["userAgent"] = HttpContext.Request.Headers.UserAgent.ToString()
        });

        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            _logger.LogInformation("Incoming email notify request: {Summary}",
                BuildEmailRequestSummarySafe(request));

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

            var result = await _emailNotifyService.SendAsync(request, cancellationToken);
            var elapsedMs = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;

            if (result.Success)
            {
                _logger.LogInformation(
                    "Email sent successfully. logId={LogId}, to={Email}, subject={Subject}, elapsedMs={Elapsed}",
                    result.LogId, request.Email, request.Subject, elapsedMs);

                return Ok(new { message = "sent", logId = result.LogId });
            }

            _logger.LogWarning("Email notify failed: {Error}, logId={LogId}", result.Error, result.LogId);

            return StatusCode(StatusCodes.Status400BadRequest, new
            {
                message = "failed",
                error = result.Error,
                logId = result.LogId
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Request cancelled by client or server token.");
            return StatusCode(StatusCodes.Status499ClientClosedRequest, new { message = "cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during email notify.");
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

    private async Task<IActionResult> SendGroupEmailAsync(GroupEmailNotifyRequest request, CancellationToken cancellationToken)
    {
        var requestId = HttpContext.Request.Headers.TryGetValue("X-Request-Id", out var rid)
                        ? rid.ToString()
                        : HttpContext.TraceIdentifier;

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["requestId"] = requestId,
            ["route"] = HttpContext.Request.Path.Value,
            ["method"] = HttpContext.Request.Method,
            ["remoteIp"] = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ["userAgent"] = HttpContext.Request.Headers.UserAgent.ToString()
        });

        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            _logger.LogInformation("Incoming group email notify request: {Summary}",
                BuildGroupEmailRequestSummarySafe(request));

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

            var result = await _emailNotifyService.SendGroupAsync(request, cancellationToken);
            var elapsedMs = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;

            if (result.Success)
            {
                _logger.LogInformation(
                    "Group email sent successfully. logId={LogId}, group={GroupId}, count={Count}, elapsedMs={Elapsed}",
                    result.LogId, request.GroupId, result.SentCount, elapsedMs);

                return Ok(new { message = "sent", count = result.SentCount, logId = result.LogId });
            }

            _logger.LogWarning("Group email notify failed: {Error}, logId={LogId}", result.Error, result.LogId);

            return StatusCode(StatusCodes.Status400BadRequest, new
            {
                message = "failed",
                error = result.Error,
                logId = result.LogId
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Request cancelled by client or server token.");
            return StatusCode(StatusCodes.Status499ClientClosedRequest, new { message = "cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during group email notify.");
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="req"></param>
    /// <returns></returns>
    private static object BuildRequestSummarySafe(TelegramNotifyRequest req)
    {
        var bodyLength = req?.Body?.Length ?? 0;
        var subjectLength = req?.Subject?.Length ?? 0;

        return new
        {
            req?.ChatId,
            HasHtmlLikeTag = req?.Body?.Contains('<') == true,
            SubjectLength = subjectLength,
            BodyLength = bodyLength
        };
    }

    private static object BuildEmailRequestSummarySafe(EmailNotifyRequest req)
    {
        var bodyLength = req?.Body?.Length ?? 0;
        var subjectLength = req?.Subject?.Length ?? 0;

        return new
        {
            req?.Email,
            HasHtmlLikeTag = req?.Body?.Contains('<') == true,
            SubjectLength = subjectLength,
            BodyLength = bodyLength
        };
    }

    private static object BuildGroupEmailRequestSummarySafe(GroupEmailNotifyRequest req)
    {
        var bodyLength = req?.Body?.Length ?? 0;
        var subjectLength = req?.Subject?.Length ?? 0;

        return new
        {
            req?.GroupId,
            SubjectLength = subjectLength,
            BodyLength = bodyLength
        };
    }
}
