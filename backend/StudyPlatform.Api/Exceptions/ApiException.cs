namespace StudyPlatform.Api.Exceptions;

public sealed class ApiException : Exception
{
    public ApiException(
        int status,
        string code,
        string message,
        IReadOnlyDictionary<string, string[]>? errors = null)
        : base(message)
    {
        Status = status;
        Code = code;
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    public int Status { get; }
    public string Code { get; }
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
