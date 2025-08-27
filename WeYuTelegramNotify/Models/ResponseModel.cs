namespace WeYuTelegramNotify.Models;

public enum FailureStage
{
    /// <summary>
    /// Body/TemplateId 條件不符、參數不正確
    /// </summary>
    Validation,        
    
    /// <summary>
    /// 找不到或非啟用的目標
    /// </summary>
    TargetLookup,      
    
    /// <summary>
    /// 找不到模板
    /// </summary>
    TemplateLookup,   
    
    /// <summary>
    /// 模板渲染錯誤
    /// </summary>
    Render,            
    
    /// <summary>
    /// 呼叫 Telegram API 失敗
    /// </summary>
    HttpSend,         
    
    /// <summary>
    /// DB 寫入/更新失敗
    /// </summary>
    DbWrite, 
    
    Unknown
}

public class TelegramSendResult
{
    public bool Success { get; init; }
    
    /// <summary>
    /// 最終實際送出的標題
    /// </summary>
    public string? Subject { get; init; }    
    
    /// <summary>
    /// 最終實際送出的內文
    /// </summary>
    public string? Body { get; init; }        
    public Guid? LogId { get; init; }
    public FailureStage? Stage { get; init; }
    
    /// <summary>
    /// 簡要錯誤訊息
    /// </summary>
    public string? Error { get; init; }       
    
    /// <summary>
    /// Telegram 回傳的 HTTP 狀態碼（若有）
    /// </summary>
    public int? HttpStatus { get; init; }     
}