namespace DressCoder.Core.Models;

/// <summary>
/// Typed result for operations whose failure is an expected part of business flow
/// (ambiguous detection, validation errors, missing dependencies) rather than an
/// exceptional condition. Exceptions are reserved for truly unexpected errors (corrupt
/// file, I/O failure). See docs/02-documento-tecnico.md, architecture principles.
/// </summary>
public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}

/// <summary>Non-generic variant for operations that don't produce a value.</summary>
public readonly struct Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }

    private Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);
}
