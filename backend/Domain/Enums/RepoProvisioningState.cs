namespace Backend.Domain.Enums;

// Repot skapas asynkront efter att uppgiften delats ut. Ready betyder att GitHub-repot finns.
public enum RepoProvisioningState
{
    Pending,
    Creating,
    Ready,
    Failed
}
