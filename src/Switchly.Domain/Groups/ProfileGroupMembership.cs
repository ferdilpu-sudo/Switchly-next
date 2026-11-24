namespace Switchly.Domain.Groups;

public enum MembershipSource
{
    Manual,
    Auto,
}

/// <summary>Many-to-many profile/group membership (ENT-003).</summary>
public sealed class ProfileGroupMembership
{
    public Guid ProfileId { get; set; }

    public Guid GroupId { get; set; }

    public MembershipSource MembershipSource { get; set; }
}
