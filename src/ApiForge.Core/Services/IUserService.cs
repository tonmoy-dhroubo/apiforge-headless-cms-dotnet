using ApiForge.Core;

namespace ApiForge.Core.Services;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct = default);
    Task<UserDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<UserDto> AssignRolesAsync(long id, IReadOnlyList<string> roles, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}
