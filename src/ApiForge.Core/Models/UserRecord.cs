namespace ApiForge.Core;

public sealed class UserRecord
{
    public long Id { get; }
    public string Username { get; }
    public string Email { get; }
    public string Password { get; }
    public string? Firstname { get; }
    public string? Lastname { get; }
    public List<string> Roles { get; }
    public bool Enabled { get; set; }

    public UserRecord(
        long id,
        string username,
        string email,
        string password,
        string? first,
        string? last,
        IEnumerable<string> roles,
        bool enabled)
    {
        Id = id;
        Username = username;
        Email = email;
        Password = password;
        Firstname = first;
        Lastname = last;
        Roles = roles.ToList();
        Enabled = enabled;
    }
}
