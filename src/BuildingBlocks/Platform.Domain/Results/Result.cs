using System.Diagnostics.CodeAnalysis;

namespace Platform.Domain.Results;

/// <summary>The empty value for commands that return nothing.</summary>
public readonly record struct Unit
{
    /// <summary>The single value.</summary>
    public static readonly Unit Value;
}

/// <summary>Outcome of an operation that returns no value: success, or a typed <see cref="Error"/>.</summary>
public class Result
{
    /// <summary>Creates a result. Use the factory methods.</summary>
    protected Result(Error? error) => Error = error;

    /// <summary>The error when <see cref="IsFailure"/>; otherwise <c>null</c>.</summary>
    public Error? Error { get; }

    /// <summary>True when the operation succeeded.</summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>True when the operation failed.</summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    /// <summary>A successful result.</summary>
    public static Result Success() => new(null);

    /// <summary>A failed result.</summary>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    /// <summary>A successful result carrying <paramref name="value"/>.</summary>
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    /// <summary>A failed result of type <typeparamref name="T"/>.</summary>
    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);

    /// <summary>Implicit conversion from an error.</summary>
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>Outcome of an operation that returns a <typeparamref name="T"/> on success.</summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, Error? error) : base(error) => _value = value;

    /// <summary>The value. Throws when the result is a failure.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read the value of a failed result ({Error.Code}).");

    /// <summary>A successful result.</summary>
    public static Result<T> Success(T value) => new(value, null);

    /// <summary>A failed result.</summary>
    public static new Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(default, error);
    }

    /// <summary>Maps the value when successful; propagates the error otherwise.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return IsSuccess ? Result<TOut>.Success(map(_value!)) : Result<TOut>.Failure(Error);
    }

    /// <summary>Chains another operation when successful.</summary>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return IsSuccess ? bind(_value!) : Result<TOut>.Failure(Error);
    }

    /// <summary>Implicit conversion from a value.</summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Implicit conversion from an error.</summary>
    public static implicit operator Result<T>(Error error) => Failure(error);
}
