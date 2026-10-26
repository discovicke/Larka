using Backend.Application.Services;

namespace Backend.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SchoolService>();
        services.AddScoped<UserService>();
        services.AddScoped<GroupService>();
        services.AddScoped<CourseService>();
        services.AddScoped<ModuleService>();
        services.AddScoped<ContentService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<SubmissionService>();
        return services;
    }
}
