using Backend.Application.Abstractions;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

public class ContentAccess(
    IPermissions permissions,
    IUserRepository users,
    ICurrentSchool currentSchool)
{
    // Lärare på någon av innehållets länkar. Författaren har ingen genväg: den som lämnat kursen
    // kan inte längre ändra det hen skrev. Admin har ingen genväg hit heller.
    public Task<bool> MayEditAsync(Content content, CancellationToken ct) =>
        permissions.CanTeachContentAsync(content.Id, ct);

    // Läsa får den som får ändra innehållet, elever och mentorer i en kurs eller grupp där en länk syns för
    // eleverna (publicerat, PublishAt passerat, veckan öppen), och admin i skolan där innehållets författare
    // hör hemma (admin läser kursernas innehåll, med utkast, men undervisar inte). Alla andra, också
    // inloggade i en annan kurs eller skola, får samma svar som för innehåll som inte finns.
    public async Task<bool> MayReadAsync(Content content, CancellationToken ct)
    {
        if (await MayEditAsync(content, ct)
            || await permissions.CanReadPublishedContentAsync(content.Id, ct))
        {
            return true;
        }

        var author = await users.GetAsync(content.AuthorId, ct);
        return author is not null
            && await permissions.IsAdminAsync(ct)
            && await currentSchool.OwnsAsync(author.SchoolId, ct);
    }
}
