namespace ApiForge.Core;

public interface IUserStore
{
    Task<UserRecord?> Find(string identifier, CancellationToken ct = default);
    Task<UserRecord?> ById(long id, CancellationToken ct = default);
    Task<IReadOnlyList<UserRecord>> All(CancellationToken ct = default);
    Task<UserRecord> Add(
        string username,
        string email,
        string password,
        string? first,
        string? last,
        IReadOnlyList<string> roles,
        CancellationToken ct = default
    );
    Task<UserRecord?> SetRoles(long id, IReadOnlyList<string> roles, CancellationToken ct = default);
    Task<bool> Remove(long id, CancellationToken ct = default);
}
