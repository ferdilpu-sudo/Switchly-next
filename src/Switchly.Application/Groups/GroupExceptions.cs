namespace Switchly.Application.Groups;

public sealed class GroupNotFoundException : InvalidOperationException
{
    public GroupNotFoundException(Guid groupId)
        : base($"Group '{groupId}' was not found.")
    {
        GroupId = groupId;
    }

    public Guid GroupId { get; }
}

public sealed class SystemGroupReadOnlyException : InvalidOperationException
{
    public SystemGroupReadOnlyException(Guid groupId, string operation)
        : base($"System-derived WebsiteDomain group '{groupId}' is read-only and does not allow '{operation}'.")
    {
        GroupId = groupId;
    }

    public Guid GroupId { get; }
}
