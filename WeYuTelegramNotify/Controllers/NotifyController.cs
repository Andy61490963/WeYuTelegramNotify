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

    public NotifyController(ITelegramNotifyService telegramService)
    {
        _telegramService = telegramService;
    }
    
    [HttpGet("telegram")]
    public Task<IActionResult> GetTelegram([FromQuery] TelegramNotifyRequest request, CancellationToken cancellationToken)
        => SendTelegramAsync(request, cancellationToken);

    [HttpPost("telegram")]
    public Task<IActionResult> PostTelegramForm([FromForm] TelegramNotifyRequest request, CancellationToken cancellationToken)
        => SendTelegramAsync(request, cancellationToken);

    private async Task<IActionResult> SendTelegramAsync(TelegramNotifyRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _telegramService.SendAsync(request, cancellationToken);

        if (result.Success)
        {
            return Ok(new
            {
                message = "sent",
                subject = result.Subject,
                body = HttpUtility.HtmlDecode(result.Body),
                logId = result.LogId
            });
        }

        // 失敗依 Stage 分類 HTTP 狀態碼
        var status = result.Stage switch
        {
            FailureStage.Validation => StatusCodes.Status400BadRequest,
            FailureStage.HttpSend   => StatusCodes.Status502BadGateway, // 上游（Telegram）失敗
            FailureStage.DbWrite    => StatusCodes.Status500InternalServerError,
            _                       => StatusCodes.Status500InternalServerError
        };

        return StatusCode(status, new
        {
            message = "failed",
            stage = result.Stage?.ToString(),
            error = result.Error,
            httpStatus = result.HttpStatus,
            logId = result.LogId
        });
    }
}

