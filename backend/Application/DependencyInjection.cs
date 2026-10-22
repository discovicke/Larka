using Backend.Application.Services;

namespace Backend.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GroupService>();
        services.AddScoped<ContentService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<SubmissionService>();
        services.AddScoped<LabService>();
        return services;
    }
}
