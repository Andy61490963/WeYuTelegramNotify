using Microsoft.AspNetCore.Mvc;
using WeYuTelegramNotify.Models;
using WeYuTelegramNotify.Services;

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

    [HttpPost("telegram")]
    public async Task<IActionResult> PostTelegram([FromBody] TelegramNotifyRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var requestId = Guid.NewGuid();
        try
        {
            await _telegramService.SendAsync(request, cancellationToken);
            return Accepted(new { requestId });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = ex.Message, requestId });
        }
    }
}

