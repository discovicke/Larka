using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

public class SchoolService(ISchoolRepository schools, IUnitOfWork unitOfWork)
{
    public async Task<List<SchoolDto>> GetAllAsync(CancellationToken ct)
    {
        var allSchools = await schools.GetAllAsync(ct);
        return allSchools.Select(SchoolDto.From).ToList();
    }

    public async Task<SchoolDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var school = await schools.GetAsync(id, ct);
        return school is null
            ? null
            : SchoolDto.From(school);
    }

    public async Task<ValidationResult<SchoolDto>> CreateAsync(CreateSchoolDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return ValidationResult<SchoolDto>.Invalid("name is required");
        }

        var school = new School { Name = dto.Name.Trim() };
        schools.Add(school);
        await unitOfWork.SaveChangesAsync(ct);
        return ValidationResult<SchoolDto>.Ok(SchoolDto.From(school));
    }
}
