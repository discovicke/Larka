using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Files;
using Backend.Application.Results;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

public class ContentAttachmentService(
    IContentRepository contents,
    ContentAccess access,
    IFileStorage storage,
    IUnitOfWork unitOfWork)
{
    /// <summary>Sparar filen i lagringen under content/{contentId}. NotFound om innehållet inte finns.</summary>
    public async Task<ServiceResult<AttachmentDto>> AddAttachmentAsync(
        Guid contentId, FileUpload file, CancellationToken ct)
    {
        var content = await contents.GetAsync(contentId, ct);
        if (content is null)
        {
            return ServiceResult<AttachmentDto>.NotFound("content not found");
        }

        if (!await access.MayEditAsync(content, ct))
        {
            return ServiceResult<AttachmentDto>.Forbidden();
        }

        var error = FileUploadPolicy.Validate([file]);
        if (error is not null)
        {
            return ServiceResult<AttachmentDto>.Invalid(error);
        }

        var key = await storage.UploadAsync(
            $"content/{contentId}", file.Content, FileUploadPolicy.ContentTypeFor(file.FileName)!, ct);
        var attachment = new Attachment
        {
            ContentId = contentId,
            FileName = FileUploadPolicy.CleanFileName(file.FileName),
            StorageKey = key,
            SizeBytes = file.SizeBytes
        };
        contents.AddAttachment(attachment);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        return ServiceResult<AttachmentDto>.Ok(await ToDtoAsync(attachment, ct));
    }

    /// <summary>Länkarna skapas här, när bilagorna läses. NotFound om innehållet inte finns.</summary>
    public async Task<ServiceResult<List<AttachmentDto>>> GetAttachmentsAsync(Guid contentId, CancellationToken ct)
    {
        var content = await contents.GetAsync(contentId, ct);
        if (content is null || !await access.MayReadAsync(content, ct))
        {
            return ServiceResult<List<AttachmentDto>>.NotFound("content not found");
        }

        var attachments = await contents.GetAttachmentsAsync(contentId, ct);
        var dtos = new List<AttachmentDto>();
        foreach (var attachment in attachments)
        {
            dtos.Add(await ToDtoAsync(attachment, ct));
        }

        return ServiceResult<List<AttachmentDto>>.Ok(dtos);
    }

    /// <summary>NotFound om bilagan inte finns, 403 om användaren inte får ändra innehållet.</summary>
    public async Task<ServiceResult<bool>> DeleteAttachmentAsync(
        Guid contentId, Guid attachmentId, CancellationToken ct)
    {
        var attachment = await contents.GetAttachmentAsync(contentId, attachmentId, ct);
        if (attachment is null)
        {
            return ServiceResult<bool>.NotFound("attachment not found");
        }

        var content = await contents.GetAsync(contentId, ct);
        if (content is null)
        {
            return ServiceResult<bool>.NotFound("content not found");
        }

        if (!await access.MayEditAsync(content, ct))
        {
            return ServiceResult<bool>.Forbidden();
        }

        contents.RemoveAttachment(attachment);
        await unitOfWork.SaveChangesAsync(ct);
        await storage.DeleteAsync(attachment.StorageKey, ct);
        return ServiceResult<bool>.Ok(true);
    }

    private async Task<AttachmentDto> ToDtoAsync(Attachment attachment, CancellationToken ct) =>
        AttachmentDto.From(
            attachment,
            await storage.GetSignedUrlAsync(attachment.StorageKey, IFileStorage.SignedUrlLifetime, ct));
}
