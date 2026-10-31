using Backend.Domain.Entities;

namespace Backend.Application.Services;

public static class PositionOrder
{
    /// <summary>Sätter position 1, 2, 3 … i den ordning objekten kommer.</summary>
    public static void Renumber<T>(IEnumerable<T> ordered)
        where T : IHasPosition
    {
        var position = 1;
        foreach (var item in ordered)
        {
            item.Position = position++;
        }
    }
}
