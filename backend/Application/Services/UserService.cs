using System.Net.Mail;
using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Backend.Application.Services;

public class UserService(
    IUserRepository users,
    ISchoolRepository schools,
    IPermissions permissions,
    IPasswordHasher<User> passwordHasher,
    IUnitOfWork unitOfWork)
{
    private const int _minPasswordLength = 8;

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
    public async Task<ServiceResult<UserDto>> CreateAsync(CreateUserDto dto, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<UserDto>.Forbidden();
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return ServiceResult<UserDto>.Invalid("name is required");
        }

        // E-post lagras med små bokstäver så att samma adress inte kan finnas två gånger med olika versaler.
        var email = dto.Email?.Trim().ToLowerInvariant() ?? "";
        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
        {
            return ServiceResult<UserDto>.Invalid("email is not a valid address");
        }

        if (await users.GetByEmailAsync(email, ct) is not null)
        {
            return ServiceResult<UserDto>.Invalid("email is already in use");
        }

        if (!await schools.ExistsAsync(dto.SchoolId, ct))
        {
            return ServiceResult<UserDto>.Invalid("school does not exist");
        }

        // Lösenordet är valfritt. Utan det kan användaren inte logga in med e-post och lösenord.
        if (dto.Password is not null && dto.Password.Length < _minPasswordLength)
        {
            return ServiceResult<UserDto>.Invalid($"password must be at least {_minPasswordLength} characters");
        }

        var user = new User { SchoolId = dto.SchoolId, Name = dto.Name.Trim(), Email = email, Role = dto.Role };
        if (dto.Password is not null)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, dto.Password);
        }

        users.Add(user);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<UserDto>.Ok(UserDto.From(user));
    }
}
