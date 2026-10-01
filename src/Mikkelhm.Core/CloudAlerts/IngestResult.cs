namespace Mikkelhm.Core.CloudAlerts;

public enum IngestStatus
{
    Created,
    Duplicate,
    NoContainer,
    Invalid,
}

public sealed record IngestResult(IngestStatus Status, Guid? Key, IReadOnlyList<string> Errors)
{
    public static IngestResult Created(Guid key) => new(IngestStatus.Created, key, []);

    public static IngestResult Duplicate() => new(IngestStatus.Duplicate, null, []);

    public static IngestResult NoContainer() => new(IngestStatus.NoContainer, null, []);

    public static IngestResult Invalid(IReadOnlyList<string> errors) => new(IngestStatus.Invalid, null, errors);
}
