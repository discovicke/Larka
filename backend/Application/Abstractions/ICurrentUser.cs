namespace Backend.Application.Abstractions;

public interface ICurrentUser
{
    Guid UserId { get; }
}
