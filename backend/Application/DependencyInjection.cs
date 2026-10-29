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
        services.AddScoped<CourseCopyService>();
        services.AddScoped<CourseRunService>();
        services.AddScoped<StudentContentService>();
        services.AddScoped<ContentService>();
        services.AddScoped<MeService>();
        services.AddScoped<IntegrationService>();
        services.AddScoped<GitHubLinkingService>();
        services.AddScoped<CalendarService>();
        services.AddScoped<StudyPlanService>();
        services.AddScoped<TodayService>();
        services.AddScoped<TeachingService>();
        services.AddScoped<IPermissions, PermissionService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<SubmissionService>();
        services.AddScoped<NotificationService>();
        return services;
    }
}
