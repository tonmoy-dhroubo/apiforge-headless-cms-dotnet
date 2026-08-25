using ApiForge.Core;
using ApiForge.Core.Services;
using ApiForge.Infrastructure;

namespace ApiForge.Api.Services;

public class UserService(IUserStore users) : IUserService
{
    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct = default)
    {
        var records = await users.All(ct);
        return records.Select(ToDto).ToList();
    }

    public async Task<UserDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var user = await users.ById(id, ct);
        if (user is null)
        {
            throw new ApiForgeException("User not found", 404);
        }

        return ToDto(user);
    }

    public async Task<UserDto> AssignRolesAsync(long id, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        var user = await users.SetRoles(id, roles, ct);
        if (user is null)
        {
            throw new ApiForgeException("User not found", 404);
        }

        return ToDto(user);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var removed = await users.Remove(id, ct);
        if (!removed)
        {
            throw new ApiForgeException("User not found", 404);
        }
    }

    private static UserDto ToDto(UserRecord u) =>
        new(u.Id, u.Username, u.Email, u.Firstname, u.Lastname, u.Roles, u.Enabled);
}
