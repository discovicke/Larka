using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Files;
using Backend.Application.Results;

namespace Backend.Application.Services;

// Användarens profilbild. Bilden ligger i samma privata lagring som andra filer, under avatars/{userId}.
public class AvatarService(
    IUserRepository users,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    MeService me,
    ILogger<AvatarService> logger)
{
    /// <summary>Sparar en ny bild åt den inloggade användaren och tar bort den gamla. Invalid om filen inte är en bild.</summary>
    public async Task<ServiceResult<MeDto>> SetAsync(FileUpload file, CancellationToken ct)
    {
        var error = AvatarPolicy.Validate(file);
        if (error is not null)
        {
            return ServiceResult<MeDto>.Invalid(error);
        }

        var user = await users.GetAsync(currentUser.UserId, ct);
        if (user is null)
        {
            return ServiceResult<MeDto>.NotFound("user not found");
        }

        var key = await storage.UploadAsync(
            $"avatars/{user.Id}", file.Content, FileUploadPolicy.ContentTypeFor(file.FileName)!, ct);
        var oldKey = user.AvatarKey;
        user.AvatarKey = key;
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteQuietlyAsync(key, logger);
            throw;
        }

        if (oldKey is not null)
        {
            await storage.DeleteQuietlyAsync(oldKey, logger);
        }

        return ServiceResult<MeDto>.Ok((await me.GetAsync(ct))!);
    }

    /// <summary>Tar bort den inloggade användarens bild. Det gör inget om hen inte hade någon.</summary>
    public async Task<ServiceResult<MeDto>> RemoveAsync(CancellationToken ct)
    {
        var user = await users.GetAsync(currentUser.UserId, ct);
        if (user is null)
        {
            return ServiceResult<MeDto>.NotFound("user not found");
        }

        var oldKey = user.AvatarKey;
        if (oldKey is null)
        {
            return ServiceResult<MeDto>.Ok((await me.GetAsync(ct))!);
        }

        user.AvatarKey = null;
        await unitOfWork.SaveChangesAsync(ct);
        await storage.DeleteQuietlyAsync(oldKey, logger);
        return ServiceResult<MeDto>.Ok((await me.GetAsync(ct))!);
    }

    /// <summary>
    /// En kortlivad länk till bilden, eller null om användaren inte har den bilden (längre). Anropas utan inloggning,
    /// eftersom en img-tagg inte skickar med token. Adressen innehåller bildens slumpade guid, så den går inte att
    /// gissa, och den slutar fungera när bilden byts eller tas bort.
    /// </summary>
    public async Task<string?> GetSignedUrlAsync(Guid userId, string version, CancellationToken ct)
    {
        var user = await users.GetAsync(userId, ct);
        if (user?.AvatarKey is null || Path.GetFileName(user.AvatarKey) != version)
        {
            return null;
        }

        return await storage.GetSignedUrlAsync(user.AvatarKey, IFileStorage.SignedUrlLifetime, ct);
    }
}
