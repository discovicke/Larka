using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

/// <summary>
/// Elevens synlighet för en del. Regeln delas av elevvyn och kalendern, så att de visar samma saker:
/// innehållet är publicerat, länken har passerat PublishAt och veckan är öppen.
/// </summary>
public static class ContentVisibility
{
    public static bool IsVisible(Content content, ContentLink link, DateTimeOffset now) =>
        content.Status == ContentStatus.Published && link.PublishAt <= now;

    public static bool IsModuleAvailable(DateTimeOffset? publishAt, DateTimeOffset now) =>
        publishAt is null || publishAt <= now;
}
