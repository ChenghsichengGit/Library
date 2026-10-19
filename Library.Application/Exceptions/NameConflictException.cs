namespace Library.Application.Exceptions;

/// <summary>
/// 改名時和別的作者／社團或系列同名。Controller 會轉成 409。
/// </summary>
public class NameConflictException(string message) : Exception(message);
