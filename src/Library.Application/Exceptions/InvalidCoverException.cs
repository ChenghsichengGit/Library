namespace Library.Application.Exceptions;

/// <summary>
/// 封面不合法（太大、不是支援的圖片格式、網址不是 http/https）。CoversController 會轉成 400。
/// </summary>
public class InvalidCoverException(string message) : Exception(message);
