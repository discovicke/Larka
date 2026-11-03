using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Files;
using Backend.Application.Results;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

public class ContentCommentService(
    IContentRepository contents,
    IUserRepository users,
    ContentAccess access,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async Task<List<CommentDto>> GetCommentsAsync(Guid contentId, CancellationToken ct)
    {
        var content = await contents.GetAsync(contentId, ct);
        if (content is null || !await access.MayReadAsync(content, ct))
        {
            return [];
        }

        var comments = await contents.GetCommentsAsync(contentId, ct);
        var authorIds = comments.Select(comment => comment.AuthorId).Distinct().ToList();
        var names = await users.GetNamesAsync(authorIds, ct);
        var avatars = await users.GetAvatarUrlsAsync(authorIds, ct);
        return comments
            .Select(comment => CommentDto.From(
                comment, names.GetValueOrDefault(comment.AuthorId, ""), avatars.GetValueOrDefault(comment.AuthorId)))
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

        var comment = new Comment { ContentId = contentId, AuthorId = currentUser.UserId, BodyMd = dto.BodyMd };
        contents.AddComment(comment);
        await unitOfWork.SaveChangesAsync(ct);
        var author = await users.GetAsync(currentUser.UserId, ct);
        return ServiceResult<CommentDto>.Ok(CommentDto.From(
            comment, author?.Name ?? "", AvatarPolicy.UrlFor(currentUser.UserId, author?.AvatarKey)));
    }
}
