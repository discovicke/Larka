using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Files;
using Backend.Application.Results;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

// Den som får läsa innehållet får kommentera det. Författaren ändrar och tar bort sin egen kommentar. Läraren
// på innehållet (MayEditAsync) tar bort vilken kommentar som helst, till exempel en elak eller trasig. Admin
// läser men undervisar inte, så admin tar bara bort sina egna.
public class ContentCommentService(
    IContentRepository contents,
    IUserRepository users,
    ContentAccess access,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public const string CommentRequired = "comment text is required";
    public const string CommentNotFound = "comment not found";

    public async Task<List<CommentDto>> GetCommentsAsync(Guid contentId, CancellationToken ct)
    {
        var content = await contents.GetAsync(contentId, ct);
        if (content is null || !await access.MayReadAsync(content, ct))
        {
            return [];
        }

        var mayModerate = await access.MayEditAsync(content, ct);
        var comments = await contents.GetCommentsAsync(contentId, ct);
        var authorIds = comments.Select(comment => comment.AuthorId).Distinct().ToList();
        var names = await users.GetNamesAsync(authorIds, ct);
        var avatars = await users.GetAvatarUrlsAsync(authorIds, ct);
        return comments
            .Select(comment => ToDto(
                comment,
                names.GetValueOrDefault(comment.AuthorId, ""),
                avatars.GetValueOrDefault(comment.AuthorId),
                mayModerate))
            .ToList();
    }

    /// <summary>NotFound om innehållet inte finns eller användaren inte får läsa det.</summary>
    public async Task<ServiceResult<CommentDto>> AddCommentAsync(
        Guid contentId,
        CreateCommentDto dto,
        CancellationToken ct)
    {
        var content = await contents.GetAsync(contentId, ct);
        if (content is null || !await access.MayReadAsync(content, ct))
        {
            return ServiceResult<CommentDto>.NotFound("content not found");
        }

        var bodyError = CheckBody(dto.BodyMd);
        if (bodyError is not null)
        {
            return ServiceResult<CommentDto>.Invalid(bodyError);
        }

        var comment = new Comment { ContentId = contentId, AuthorId = currentUser.UserId, BodyMd = dto.BodyMd.Trim() };
        contents.AddComment(comment);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<CommentDto>.Ok(await ToDtoForAuthorAsync(comment, ct));
    }

    /// <summary>Bara författaren ändrar sin kommentar. NotFound om innehållet eller kommentaren inte går att läsa.</summary>
    public async Task<ServiceResult<CommentDto>> UpdateCommentAsync(
        Guid contentId,
        Guid commentId,
        UpdateCommentDto dto,
        CancellationToken ct)
    {
        var (content, comment) = await FindAsync(contentId, commentId, ct);
        if (content is null || comment is null)
        {
            return ServiceResult<CommentDto>.NotFound(CommentNotFound);
        }

        if (comment.AuthorId != currentUser.UserId)
        {
            return ServiceResult<CommentDto>.Forbidden();
        }

        var bodyError = CheckBody(dto.BodyMd);
        if (bodyError is not null)
        {
            return ServiceResult<CommentDto>.Invalid(bodyError);
        }

        comment.BodyMd = dto.BodyMd.Trim();
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<CommentDto>.Ok(await ToDtoForAuthorAsync(comment, ct));
    }

    /// <summary>Författaren eller läraren på innehållet tar bort kommentaren.</summary>
    public async Task<ServiceResult<bool>> DeleteCommentAsync(Guid contentId, Guid commentId, CancellationToken ct)
    {
        var (content, comment) = await FindAsync(contentId, commentId, ct);
        if (content is null || comment is null)
        {
            return ServiceResult<bool>.NotFound(CommentNotFound);
        }

        if (comment.AuthorId != currentUser.UserId && !await access.MayEditAsync(content, ct))
        {
            return ServiceResult<bool>.Forbidden();
        }

        contents.RemoveComment(comment);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<bool>.Ok(true);
    }

    private static string? CheckBody(string? bodyMd)
    {
        if (string.IsNullOrWhiteSpace(bodyMd))
        {
            return CommentRequired;
        }

        return bodyMd.Trim().Length > TextLimits.Comment
            ? TextLimits.CommentTooLong
            : null;
    }

    // Den som inte får läsa innehållet får samma svar som för en kommentar som inte finns.
    private async Task<(Content? Content, Comment? Comment)> FindAsync(
        Guid contentId, Guid commentId, CancellationToken ct)
    {
        var content = await contents.GetAsync(contentId, ct);
        if (content is null || !await access.MayReadAsync(content, ct))
        {
            return (null, null);
        }

        return (content, await contents.GetCommentAsync(contentId, commentId, ct));
    }

    private CommentDto ToDto(Comment comment, string authorName, string? avatarUrl, bool mayModerate)
    {
        var isAuthor = comment.AuthorId == currentUser.UserId;
        return CommentDto.From(comment, authorName, avatarUrl, canEdit: isAuthor, canDelete: isAuthor || mayModerate);
    }

    // Den som just skrev eller ändrade kommentaren är författaren, så hen får både ändra och ta bort den.
    private async Task<CommentDto> ToDtoForAuthorAsync(Comment comment, CancellationToken ct)
    {
        var author = await users.GetAsync(currentUser.UserId, ct);
        return ToDto(
            comment,
            author?.Name ?? "",
            AvatarPolicy.UrlFor(currentUser.UserId, author?.AvatarKey),
            mayModerate: false);
    }
}
