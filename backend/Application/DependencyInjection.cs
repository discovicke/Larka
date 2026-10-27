using Backend.Application.Abstractions;
using Backend.Application.Services;

namespace Backend.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SchoolService>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<GroupService>();
        services.AddScoped<CourseService>();
        services.AddScoped<ModuleService>();
        services.AddScoped<RoadmapService>();
        services.AddScoped<StudentContentService>();
        services.AddScoped<ContentService>();
        services.AddScoped<MeService>();
        services.AddScoped<IPermissions, PermissionService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<SubmissionService>();
        return services;
    }
}
