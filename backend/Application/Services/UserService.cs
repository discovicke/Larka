using System.Net.Mail;
using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

public class UserService(IUserRepository users, ISchoolRepository schools, IUnitOfWork unitOfWork)
{
    public async Task<List<UserDto>> GetAllAsync(CancellationToken ct)
    {
        var allUsers = await users.GetAllAsync(ct);
        return allUsers.Select(UserDto.From).ToList();
    }

    public async Task<UserDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var user = await users.GetAsync(id, ct);
        return user is null
            ? null
            : UserDto.From(user);
    }

    // Rollen är en enum i DTO:n, så okända roller stoppas redan när JSON läses in (400).
    public async Task<ValidationResult<UserDto>> CreateAsync(CreateUserDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return ValidationResult<UserDto>.Invalid("name is required");
        }

        // E-post lagras med små bokstäver så att samma adress inte kan finnas två gånger med olika versaler.
        var email = dto.Email?.Trim().ToLowerInvariant() ?? "";
        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
        {
            return ValidationResult<UserDto>.Invalid("email is not a valid address");
        }

        if (await users.GetByEmailAsync(email, ct) is not null)
        {
            return ValidationResult<UserDto>.Invalid("email is already in use");
        }

        if (!await schools.ExistsAsync(dto.SchoolId, ct))
        {
            return ValidationResult<UserDto>.Invalid("school does not exist");
        }

        var user = new User { SchoolId = dto.SchoolId, Name = dto.Name.Trim(), Email = email, Role = dto.Role };
        users.Add(user);
        await unitOfWork.SaveChangesAsync(ct);
        return ValidationResult<UserDto>.Ok(UserDto.From(user));
    }
}
