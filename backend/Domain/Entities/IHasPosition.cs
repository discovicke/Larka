namespace Backend.Domain.Entities;

// Något som ligger i en ordnad lista med position 1, 2, 3 … utan luckor.
public interface IHasPosition
{
    int Position { get; set; }
}
