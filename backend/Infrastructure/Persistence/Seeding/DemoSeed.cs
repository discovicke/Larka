using Backend.Application.Abstractions;
using Backend.Application.Files;
using Backend.Application.Services;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.Persistence.Seeding;

/// <summary>
/// Demodata för en kort demonstration: tre klasser med en avslutad, en pågående och en kommande kurs, åtta elever
/// per klass och inlämningar i blandade lägen. Kontona admin, larare, elev och elev-fiber skapas av AccountSeed.
/// Texterna finns i <see cref="DemoContent"/>, och bilagorna och elevernas filer i DemoFiles.
/// <para>
/// Programmeringsuppgifterna blir GitHub-uppgifter när skolan har GitHub kopplat och ett mallrepo finns, annars
/// lämnas de in som text och filer. Kopplas GitHub senare görs de om vid nästa start
/// (<see cref="EnableGitHubAssignmentsAsync"/>).
/// </para>
/// <para>
/// Demodatan har en version: klassen .NET25 får ett fast id (<see cref="MarkerGroupId"/>). Saknas den klassen
/// tas all undervisningsdata bort och demodatan byggs om. Konton, skolan, dess Microsoft-koppling och dess
/// GitHub-installation finns kvar. Byt id:t när demodatan ska byggas om i prod.
/// </para>
/// </summary>
public sealed class DemoSeed(
    AppDb db,
    IPasswordHasher<User> passwordHasher,
    TimeProvider time,
    IOptions<DatabaseOptions> options,
    IGitHubAppService github,
    IServiceProvider services,
    ILogger<DemoSeed> logger)
{
    public const string DemoPassword = "larka-demo";
    public const string MarkerGroupName = ".NET25";
    public const string DemoStudentDomain = "@larka.local";

    /// <summary>Id:t för klassen .NET25 i den här versionen av demodatan.</summary>
    public static readonly Guid MarkerGroupId = Guid.Parse("5350d3b0-0000-4000-8000-000000000535");

    private const int _studentsPerClass = 8;

    // Projektgrupperna i en grupp-uppgift: åtta elever blir två grupper.
    private const int _studentsPerProjectGroup = 4;

    // Filerna som laddas upp innan demodatan sparas: lärarens bilagor och elevernas inlämnade filer.
    private readonly List<(object Row, string Folder, string FileName)> _uploads = [];

    // Mallrepot för GitHub-uppgifterna, och om fillagringen finns, medan demodatan byggs.
    private string? _template;
    private bool _withFiles;

    private static readonly string[] _studentNames =
    [
        "Anna Andersson", "Erik Johansson", "Sara Karlsson", "Oscar Nilsson",
        "Elin Eriksson", "Hugo Larsson", "Maja Olsson", "Liam Persson",
        "Alva Svensson", "Noah Gustafsson", "Ella Pettersson", "William Jonsson",
        "Iris Jansson", "Adam Hansson", "Freja Bengtsson", "Lucas Lindberg",
        "Nova Jakobsson", "Isak Magnusson", "Signe Lindqvist", "Leo Berg",
        "Sofia Ström", "Viktor Ek", "Tilda Holm", "Axel Dahl",
    ];

    private static readonly string[] _passFeedback =
    [
        "Godkänt. Allt som krävs finns med. Förklara gärna dina val lite mer nästa gång.",
        "Godkänt. Lösningen fungerar och är lätt att följa. Tänk på att döpa variablerna tydligare.",
        "Godkänt. Bra struktur. Nästa steg är att skriva tester för de svåra fallen också.",
    ];

    private static readonly string[] _distinctionFeedback =
    [
        "Mycket väl genomfört. Du uppfyller alla VG-krav och motiverar dina val tydligt.",
        "Väl godkänt. Snyggt och genomtänkt, och dina tester täcker de svåra fallen.",
    ];

    // IG sätts bara när sista deadline har passerat, så texten säger aldrig åt eleven att lämna in igen.
    private static readonly string[] _failFeedback =
    [
        "Inlämningen uppfyller inte kraven för G: flera obligatoriska delar saknas. Sista deadline har passerat, "
        + "så betyget blir IG. Boka en tid med mig så går vi igenom vad som saknades.",
        "Lösningen fungerar inte när jag kör den, och beskrivningen saknas. Sista deadline har passerat, så betyget "
        + "blir IG. Hör av dig så hittar vi en väg framåt.",
    ];

    private static readonly string[] _exerciseFeedback =
    [
        "Rätt uträkning. Tänk på att skriva ut enheten i varje steg.",
        "Bra, du har fått med påslaget. Kontrollera avrundningen en gång till.",
    ];

    private static readonly string[] _needsWorkFeedback =
    [
        "Komplettera: beskrivningen av hur du har löst uppgiften saknas, och ett av G-kraven är inte uppfyllt. "
        + "Lämna in igen före kompletteringsdeadline.",
        "Komplettera: bra början, men testerna går inte igenom. Rätta dem och lämna in igen före "
        + "kompletteringsdeadline.",
    ];

    private enum CoursePhase
    {
        Completed,
        Current,
        Upcoming
    }

    public async Task<bool> SeedAsync(CancellationToken ct)
    {
        var school = await FindSchoolAsync(db, ct);
        if (school is null)
        {
            logger.LogWarning("Demodata hoppades över: det finns ingen skola.");
            return false;
        }

        if (await db.Groups.AnyAsync(group => group.Id == MarkerGroupId, ct))
        {
            logger.LogInformation("Demodatan finns redan i skolan {School}, inget lades till.", school.Name);
            await EnableGitHubAssignmentsAsync(ct);
            return false;
        }

        var template = await TemplateForAsync(school.Id, ct);
        var storage = FileStorage();
        var oldFiles = await StoredKeysAsync(ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var removed = await RemoveTeachingDataAsync(ct);
        if (removed > 0)
        {
            logger.LogInformation("Äldre demodata och undervisningsdata togs bort ({Rows} rader).", removed);
        }

        var teacher = await db.Users.SingleAsync(user => user.Email == "larare", ct);
        var studentHash = passwordHasher.HashPassword(new User(), DemoPassword);
        var now = time.GetUtcNow();

        var offset = 0;
        _uploads.Clear();
        _template = template;
        _withFiles = storage is not null;
        foreach (var (name, plan) in DemoContent.Classes)
        {
            var featured = plan.FeaturedStudent is null
                ? null
                : await db.Users.SingleOrDefaultAsync(user => user.Email == plan.FeaturedStudent, ct);
            BuildClass(name, plan, now, teacher, studentHash, featured, offset);
            offset += _studentsPerClass;
        }

        if (storage is not null)
        {
            await UploadFilesAsync(storage, ct);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        if (storage is not null)
        {
            await DeleteFilesAsync(storage, oldFiles, ct);
        }

        logger.LogInformation(
            "Demodata skapad: {Classes}. GitHub-uppgifter: {GitHub}.",
            string.Join(", ", DemoContent.Classes.Keys),
            template ?? "nej, text och filer");
        return true;
    }

    // Fillagringen, eller null när den inte är inställd. Då får demodatan inga bilagor eller inlämnade filer.
    private IFileStorage? FileStorage()
    {
        try
        {
            return services.GetService<IFileStorage>();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Fillagringen är inte inställd, så demodatan får inga filer.");
            return null;
        }
    }

    private async Task<List<string>> StoredKeysAsync(CancellationToken ct) =>
        [
            .. await db.Attachments.Select(row => row.StorageKey).ToListAsync(ct),
            .. await db.SubmissionFiles.Select(row => row.StorageKey).ToListAsync(ct),
        ];

    // Laddar upp filerna i _uploads och sätter nyckeln på raden. En fil som inte går att ladda upp tas bort ur
    // demodatan, så att ingen rad pekar på en fil som saknas.
    private async Task UploadFilesAsync(IFileStorage storage, CancellationToken ct)
    {
        var assembly = typeof(DemoSeed).Assembly;
        foreach (var (row, folder, fileName) in _uploads)
        {
            try
            {
                await using var stream = assembly.GetManifestResourceStream($"DemoFiles/{fileName}")
                    ?? throw new InvalidOperationException($"Demofilen {fileName} saknas i bygget.");
                var key = await storage.UploadAsync(folder, stream, FileUploadPolicy.ContentTypeFor(fileName)!, ct);
                switch (row)
                {
                    case Attachment attachment:
                        attachment.StorageKey = key;
                        attachment.SizeBytes = stream.Length;
                        break;
                    case SubmissionFile file:
                        file.StorageKey = key;
                        file.SizeBytes = stream.Length;
                        break;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Demofilen {File} kunde inte laddas upp.", fileName);
                db.Remove(row);
            }
        }

        _uploads.Clear();
    }

    // Filerna från den förra demodatan. Raderna är redan borttagna, så filerna har ingen ägare kvar.
    private async Task DeleteFilesAsync(IFileStorage storage, List<string> keys, CancellationToken ct)
    {
        foreach (var key in keys.Where(key => key.Length > 0))
        {
            try
            {
                await storage.DeleteAsync(key, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "En gammal demofil kunde inte tas bort.");
            }
        }
    }

    /// <summary>
    /// Gör om demodatans programmeringsuppgifter till repo-uppgifter när GitHub har kopplats efter seedningen, på
    /// samma sätt som när en lärare delar ut en: varje elev, eller varje projektgrupp, får en repo-rad som
    /// bakgrundsjobbet skapar i GitHub. Mallen väljs som läraren gör, bland mallrepona i organisationen som skolan har
    /// kopplat (eller Database:DemoTemplateRepo). Körs vid varje start och gör inget med uppgifter som redan är
    /// repo-uppgifter. Returnerar antalet nya repo-rader.
    /// </summary>
    internal async Task<int> EnableGitHubAssignmentsAsync(CancellationToken ct)
    {
        var wanted = DemoContent.Classes
            .SelectMany(entry => new[] { entry.Value.Completed, entry.Value.Current, entry.Value.Upcoming }
                .SelectMany(course => course.Weeks
                    .SelectMany(week => week.Parts.OfType<DemoAssignment>())
                    .Where(UsesGitHub)
                    .Select(part => (Code: CodeFor(entry.Key, course.CodeSuffix), Part: part, course.CodeSuffix))))
            .ToList();
        var codes = wanted.Select(row => row.Code).Distinct().ToList();
        var targets = await (
                from link in db.ContentLinks
                join course in db.Courses on link.CourseId equals course.Id
                join content in db.Content.Include(row => row.Assignment) on link.ContentId equals content.Id
                where codes.Contains(course.Code) && content.Assignment != null
                select new { Link = link, Content = content, course.Code, course.SchoolId })
            .ToListAsync(ct);
        targets = targets
            .Where(row => !row.Content.Assignment!.UsesRepo
                && wanted.Any(item => item.Code == row.Code && item.Part.Title == row.Content.Title))
            .ToList();
        if (targets.Count == 0)
        {
            return 0;
        }

        var template = await TemplateForAsync(targets[0].SchoolId, ct);
        if (template is null)
        {
            return 0;
        }

        var now = time.GetUtcNow();
        var created = 0;
        foreach (var target in targets)
        {
            var (_, part, suffix) = wanted.First(item =>
                item.Code == target.Code && item.Part.Title == target.Content.Title);
            var spec = target.Content.Assignment!;
            ApplyHandIn(target.Content, spec, part, suffix, template);

            var students = await (
                    from groupCourse in db.GroupCourses
                    join enrollment in db.Enrollments on groupCourse.GroupId equals enrollment.GroupId
                    join student in db.Users on enrollment.UserId equals student.Id
                    where groupCourse.CourseId == target.Link.CourseId && enrollment.Role == GroupRole.Student
                    select student)
                .ToListAsync(ct);
            await db.Submissions.Where(row => row.ContentLinkId == target.Link.Id).Include(row => row.Repo)
                .LoadAsync(ct);
            created += HandOutRepos(
                target.Link,
                target.Content,
                spec,
                students.OrderBy(student => student.Email, StringComparer.Ordinal).ToList(),
                now);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Demodatans GitHub-uppgifter använder mallen {Template}: {Count} repon väntar på att skapas.",
            template,
            created);
        return created;
    }

    private static bool UsesGitHub(DemoAssignment part) =>
        part.HandIn is DemoHandIn.Repo or DemoHandIn.GroupRepo;

    // Inlämningssätten för en del. Programmeringsuppgifter har repo och text när GitHub finns, annars text och filer.
    private static List<SubmissionMode> ModesFor(DemoAssignment part, string? template) => part.HandIn switch
    {
        DemoHandIn.Repo or DemoHandIn.GroupRepo when template is not null => [SubmissionMode.Repo, SubmissionMode.Text],
        DemoHandIn.Repo or DemoHandIn.GroupRepo => [SubmissionMode.Text, SubmissionMode.File],
        DemoHandIn.File => [SubmissionMode.File, SubmissionMode.Text],
        DemoHandIn.OnSite => [SubmissionMode.OnSite],
        _ => [SubmissionMode.Text]
    };

    // Raden som säger hur eleven lämnar in, sist i uppgiftens text. OnSite och filuppgifter säger det själva.
    private static string? HandInNote(DemoAssignment part, string? template) => part.HandIn switch
    {
        DemoHandIn.Repo when template is not null =>
            "**Inlämning:** koden lämnar du i GitHub-repot som skapas åt dig. Skriv en kort beskrivning i textfältet.",
        DemoHandIn.GroupRepo when template is not null =>
            "**Inlämning:** gruppen arbetar i ett gemensamt GitHub-repo som skapas åt er. Var och en skriver en kort "
            + "beskrivning av sin del i textfältet.",
        DemoHandIn.Repo or DemoHandIn.GroupRepo =>
            "**Inlämning:** ladda upp koden som en zip-fil och skriv en kort beskrivning i textfältet.",
        _ => null
    };

    // Sätter inlämningssätten, GitHub-inställningarna och raden om inlämningen på en uppgift.
    private static void ApplyHandIn(
        Content content, AssignmentSpec spec, DemoAssignment part, string codeSuffix, string? template)
    {
        spec.SubmissionModes = ModesFor(part, template);
        spec.GradeScale = part.Scale;
        var note = HandInNote(part, template);
        content.BodyMd = note is null
            ? part.Body
            : $"{part.Body.TrimEnd()}\n\n{note}";
        if (!spec.UsesRepo)
        {
            return;
        }

        // Kursens förkortning först, så att samma uppgiftsnamn i två kurser inte krockar:
        // devops-inl-mning-4-drifts-tt-appen-grupp-1.
        var prefix = codeSuffix.ToLowerInvariant();
        spec.TemplateRepo = template;
        spec.WorkMode = part.HandIn == DemoHandIn.GroupRepo
            ? RepoWorkMode.Group
            : RepoWorkMode.Individual;
        spec.RepoNamePattern = spec.WorkMode == RepoWorkMode.Group
            ? $"{prefix}-{{assignmentSlug}}-{{groupSlug}}"
            : $"{prefix}-{{assignmentSlug}}-{{githubLogin}}";
    }

    /// <summary>
    /// Delar ut en repo-uppgift som SubmissionService.HandOutAsync gör: en inlämning per elev (de som saknas skapas
    /// som NotStarted) och en pending repo-rad per elev, eller per projektgrupp i grupp-läge. Grupperna fylls i
    /// elevernas ordning, fyra i taget. Inlämningarna ska vara laddade i db.Submissions.Local. Returnerar antalet nya
    /// repo-rader.
    /// </summary>
    private int HandOutRepos(
        ContentLink link, Content content, AssignmentSpec spec, IReadOnlyList<User> students, DateTimeOffset now)
    {
        var byStudent = db.Submissions.Local
            .Where(row => row.ContentLinkId == link.Id)
            .ToDictionary(row => row.StudentId);
        var ordered = students.ToList();
        foreach (var student in ordered.Where(student => !byStudent.ContainsKey(student.Id)))
        {
            var submission = new Submission { ContentLinkId = link.Id, StudentId = student.Id };
            db.Submissions.Add(submission);
            byStudent[student.Id] = submission;
        }

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var created = 0;
        if (spec.WorkMode == RepoWorkMode.Group)
        {
            var chunks = ordered.Chunk(_studentsPerProjectGroup).ToList();
            for (var index = 0; index < chunks.Count; index += 1)
            {
                var group = new AssignmentGroup
                {
                    ContentLinkId = link.Id,
                    Name = $"Grupp {index + 1}",
                    Position = index + 1
                };
                db.AssignmentGroups.Add(group);
                foreach (var member in chunks[index])
                {
                    db.AssignmentGroupMembers.Add(new AssignmentGroupMember
                    {
                        AssignmentGroupId = group.Id,
                        StudentId = member.Id
                    });
                    byStudent[member.Id].AssignmentGroupId = group.Id;
                }

                var leader = byStudent[chunks[index][0].Id];
                if (leader.Repo is not null)
                {
                    continue;
                }

                leader.Repo = NewRepo(leader, content, spec, now, GitHubRepoNaming.ForGroup(
                    spec.RepoNamePattern, content.Title, group.Name, group.Position, usedNames));
                leader.Repo.AssignmentGroupId = group.Id;
                created += 1;
            }

            return created;
        }

        foreach (var student in ordered)
        {
            var submission = byStudent[student.Id];
            if (submission.Repo is not null)
            {
                continue;
            }

            submission.Repo = NewRepo(submission, content, spec, now, GitHubRepoNaming.ForStudent(
                spec.RepoNamePattern, content.Title, student, usedNames));
            created += 1;
        }

        return created;
    }

    private static StudentRepo NewRepo(
        Submission submission, Content content, AssignmentSpec spec, DateTimeOffset now, string name) => new()
        {
            SubmissionId = submission.Id,
            TemplateRepo = spec.TemplateRepo,
            AssignmentTitle = content.Title,
            RepoNamePattern = spec.RepoNamePattern,
            DesiredRepoName = name,
            ProvisioningState = RepoProvisioningState.Pending,
            NextProvisioningAttemptAt = now,
            UpdatedAt = now
        };

    // Mallen ur inställningen, annars det första mallrepot i skolans kopplade organisation. Null när ingen finns.
    private async Task<string?> TemplateForAsync(Guid schoolId, CancellationToken ct)
    {
        var configured = options.Value.DemoTemplateRepo?.Trim();
        if (!string.IsNullOrEmpty(configured))
        {
            var parts = configured.Split('/');
            if (parts.Length == 2 && parts.All(part => part.Length > 0))
            {
                return configured;
            }

            logger.LogWarning("Database:DemoTemplateRepo ska vara ägare/namn, inte {Template}.", configured);
            return null;
        }

        var installation = await db.SchoolGitHubInstallations.FirstOrDefaultAsync(
            row => row.SchoolId == schoolId && row.Connected && row.Enabled, ct);
        if (installation is null)
        {
            return null;
        }

        try
        {
            var templates = await github.ListTemplateRepositoriesAsync(installation.InstallationId, ct);
            var template = templates
                .Where(repo => !repo.IsArchived)
                .OrderBy(repo => repo.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(repo => repo.FullName)
                .FirstOrDefault();
            if (template is null)
            {
                logger.LogInformation(
                    "Organisationen {Org} har inget mallrepo, så demodatans GitHub-uppgift lämnas in som text.",
                    installation.Org);
            }

            return template;
        }
        catch (Exception exception) when (exception is GitHubApiException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Mallrepona i {Org} gick inte att läsa.", installation.Org);
            return null;
        }
    }

    internal static string CodeFor(string className, string codeSuffix) => $"{className}_{codeSuffix}";

    internal static async Task<School?> FindSchoolAsync(AppDb db, CancellationToken ct) =>
        await db.Schools.FirstOrDefaultAsync(row => row.Id == PlaceholderIds.SchoolId, ct)
        ?? await db.Schools.OrderBy(row => row.Name).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Tar bort all undervisningsdata, i en ordning som håller främmande nycklar giltiga, och demodatans elever.
    /// Konton, skolor och GitHub-installationer rörs inte.
    /// </summary>
    private async Task<int> RemoveTeachingDataAsync(CancellationToken ct)
    {
        var rows = 0;
        rows += await db.Notifications.ExecuteDeleteAsync(ct);
        rows += await db.StudentHelpRequests.ExecuteDeleteAsync(ct);
        rows += await db.Comments.ExecuteDeleteAsync(ct);
        rows += await db.Attachments.ExecuteDeleteAsync(ct);
        rows += await db.ChecklistTicks.ExecuteDeleteAsync(ct);
        rows += await db.PartCompletions.ExecuteDeleteAsync(ct);
        rows += await db.StudentRepoCheckRuns.ExecuteDeleteAsync(ct);
        rows += await db.StudentRepos.ExecuteDeleteAsync(ct);
        rows += await db.SubmissionFiles.ExecuteDeleteAsync(ct);
        rows += await db.Grades.ExecuteDeleteAsync(ct);
        rows += await db.Submissions.ExecuteDeleteAsync(ct);
        rows += await db.AssignmentGroupMembers.ExecuteDeleteAsync(ct);
        rows += await db.AssignmentGroups.ExecuteDeleteAsync(ct);
        rows += await db.AssignmentReferences.ExecuteDeleteAsync(ct);
        rows += await db.ContentLinkDependencies.ExecuteDeleteAsync(ct);
        rows += await db.ContentLinks.ExecuteDeleteAsync(ct);
        rows += await db.AssignmentRequirements.ExecuteDeleteAsync(ct);
        rows += await db.AssignmentSpecs.ExecuteDeleteAsync(ct);
        rows += await db.Content.ExecuteDeleteAsync(ct);
        rows += await db.Modules.ExecuteDeleteAsync(ct);
        rows += await db.CourseTeachers.ExecuteDeleteAsync(ct);
        rows += await db.GroupCourses.ExecuteDeleteAsync(ct);
        rows += await db.Enrollments.ExecuteDeleteAsync(ct);
        rows += await db.Courses.ExecuteDeleteAsync(ct);
        rows += await db.Groups.ExecuteDeleteAsync(ct);
        rows += await db.Users.Where(user => user.Email.EndsWith(DemoStudentDomain)).ExecuteDeleteAsync(ct);
        return rows;
    }

    private void BuildClass(
        string name,
        DemoClass plan,
        DateTimeOffset now,
        User teacher,
        string studentHash,
        User? featured,
        int studentOffset)
    {
        var group = new Group
        {
            Id = name == MarkerGroupName
                ? MarkerGroupId
                : Guid.NewGuid(),
            SchoolId = teacher.SchoolId,
            Type = GroupType.Class,
            Name = name
        };
        db.Groups.Add(group);
        db.Enrollments.Add(new Enrollment { GroupId = group.Id, UserId = teacher.Id, Role = GroupRole.Teacher });

        var students = new List<User>();
        if (featured is not null)
        {
            students.Add(featured);
        }

        for (var index = students.Count; index < _studentsPerClass; index += 1)
        {
            var student = new User
            {
                SchoolId = teacher.SchoolId,
                Name = _studentNames[studentOffset + index],
                Email = $"{name.ToLowerInvariant()}.{index + 1}{DemoStudentDomain}",
                Role = UserRole.Student,
                PasswordHash = studentHash
            };
            db.Users.Add(student);
            students.Add(student);
        }

        foreach (var student in students)
        {
            db.Enrollments.Add(new Enrollment { GroupId = group.Id, UserId = student.Id, Role = GroupRole.Student });
        }

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var classStudents = new ClassStudents(students, featured, plan.HelpMessage);

        var completedStart = today.AddDays(-210);
        BuildCourse(group, teacher, classStudents, plan.Completed, CoursePhase.Completed, now,
            completedStart, completedStart.AddDays(plan.Completed.Days));
        var current = BuildCourse(group, teacher, classStudents, plan.Current, CoursePhase.Current, now,
            today.AddDays(-60), today.AddDays(plan.CurrentDaysLeft));
        var upcomingStart = today.AddDays(45);
        BuildCourse(group, teacher, classStudents, plan.Upcoming, CoursePhase.Upcoming, now,
            upcomingStart, upcomingStart.AddDays(plan.Upcoming.Days));

        AddClassNews(group, teacher, current, now);
    }

    private Course BuildCourse(
        Group group,
        User teacher,
        ClassStudents students,
        DemoCourse demo,
        CoursePhase phase,
        DateTimeOffset now,
        DateOnly startsOn,
        DateOnly endsOn)
    {
        var course = new Course
        {
            SchoolId = teacher.SchoolId,
            Title = demo.Title,
            Code = CodeFor(group.Name, demo.CodeSuffix),
            Description = demo.Description,
            Points = demo.Points,
            StartsOn = startsOn,
            EndsOn = endsOn
        };
        db.Courses.Add(course);
        db.GroupCourses.Add(new GroupCourse { GroupId = group.Id, CourseId = course.Id });
        db.CourseTeachers.Add(new CourseTeacher { CourseId = course.Id, UserId = teacher.Id });

        var assignments = new List<(DemoAssignment Demo, ContentLink Link, Content Content)>();
        var openWeek = demo.Weeks.LastOrDefault(week => OpensAt(startsOn, week) <= now);
        for (var weekIndex = 0; weekIndex < demo.Weeks.Length; weekIndex += 1)
        {
            var week = demo.Weeks[weekIndex];
            var opensAt = OpensAt(startsOn, week);
            var module = new Module
            {
                CourseId = course.Id,
                Title = week.Title,
                Description = week.Description,
                Position = weekIndex + 1,
                PublishAt = opensAt
            };
            db.Modules.Add(module);

            var links = new Dictionary<string, ContentLink>();
            for (var partIndex = 0; partIndex < week.Parts.Length; partIndex += 1)
            {
                var part = week.Parts[partIndex];
                var (link, content) = AddPart(
                    course, module, teacher, part, demo.CodeSuffix, partIndex + 1, startsOn, opensAt);
                links[part.Title] = link;
                if (part is DemoAssignment assignment)
                {
                    assignments.Add((assignment, link, content));
                }
            }

            foreach (var theory in week.Parts.OfType<DemoTheory>().Where(theory => theory.BlockedBy is not null))
            {
                db.ContentLinkDependencies.Add(new ContentLinkDependency
                {
                    BlockedLinkId = links[theory.Title].Id,
                    BlockingLinkId = links[theory.BlockedBy!].Id
                });
            }

            if (opensAt <= now)
            {
                var isOpenWeek = phase == CoursePhase.Current && week == openWeek;
                AddTheoryCompletions(week, links, students, opensAt, isOpenWeek);
            }
        }

        // Uppgifter som inte är publicerade än har inga inlämningar.
        if (phase != CoursePhase.Upcoming)
        {
            for (var index = 0; index < assignments.Count; index += 1)
            {
                if (assignments[index].Link.PublishAt > now)
                {
                    continue;
                }

                var isLast = index == assignments.Count - 1;
                AddSubmissions(teacher, students, assignments[index].Demo, assignments[index].Link, phase, index,
                    isLast, now);
            }
        }

        // Repo-uppgifter delas ut direkt, som när en lärare skapar dem, så att repona finns när veckan öppnar.
        foreach (var (_, link, content) in assignments.Where(row => row.Content.Assignment!.UsesRepo))
        {
            HandOutRepos(link, content, content.Assignment!, students.All, now);
        }

        foreach (var news in demo.News ?? [])
        {
            var content = new Content
            {
                AuthorId = teacher.Id,
                Type = ContentType.News,
                Title = news.Title,
                BodyMd = news.Body,
                Status = ContentStatus.Published,
                CreatedAt = now.AddDays(-news.DaysAgo)
            };
            db.Content.Add(content);
            db.ContentLinks.Add(new ContentLink
            {
                ContentId = content.Id,
                CourseId = course.Id,
                PublishAt = now.AddDays(-news.DaysAgo)
            });
        }

        // Den som bad om hjälp innan demon: läraren ser den i inkorgen redan, och demons egen fråga hamnar överst.
        if (phase == CoursePhase.Current && students.Featured is not null && students.HelpMessage is not null)
        {
            var open = assignments.FirstOrDefault(row => row.Link.DueAt > now && row.Link.PublishAt <= now);
            if (open.Link is not null)
            {
                AddHelpRequest(teacher, students.All[2], open.Link, now, students.HelpMessage);
            }
        }

        return course;
    }

    private (ContentLink Link, Content Content) AddPart(
        Course course, Module module, User teacher, DemoPart part, string codeSuffix, int position,
        DateOnly startsOn, DateTimeOffset opensAt)
    {
        var content = new Content
        {
            AuthorId = teacher.Id,
            Type = part is DemoAssignment
                ? ContentType.Assignment
                : ContentType.Theory,
            Title = part.Title,
            BodyMd = part.Body,
            Status = ContentStatus.Published,
            CreatedAt = opensAt.AddDays(-3)
        };
        db.Content.Add(content);

        var link = new ContentLink
        {
            ContentId = content.Id,
            CourseId = course.Id,
            ModuleId = module.Id,
            Position = position,
            PublishAt = opensAt
        };
        if (part is DemoAssignment assignment)
        {
            content.Assignment = new AssignmentSpec
            {
                ContentId = content.Id,
                CompletionRule = CompletionRule.Submitted
            };
            ApplyHandIn(content, content.Assignment, assignment, codeSuffix, _template);
            link.DueAt = DeadlineOn(startsOn.AddDays(assignment.DueOnDay));
            link.ResubmitDueAt = assignment.ResubmitOnDay is null
                ? []
                : [DeadlineOn(startsOn.AddDays(assignment.ResubmitOnDay.Value))];

            var attachments = _withFiles
                ? assignment.Attachments ?? []
                : [];
            foreach (var fileName in attachments)
            {
                var attachment = new Attachment { ContentId = content.Id, FileName = fileName };
                db.Attachments.Add(attachment);
                _uploads.Add((attachment, $"content/{content.Id}", fileName));
            }
        }

        db.ContentLinks.Add(link);
        return (link, content);
    }

    // Eleverna har läst teorin i veckorna som har gått. I veckan som pågår har bara hälften gjort det, och demoeleven
    // har teorin kvar, så att klarmarkeringen går att visa.
    private void AddTheoryCompletions(
        DemoWeek week, Dictionary<string, ContentLink> links, ClassStudents students, DateTimeOffset opensAt,
        bool isOpenWeek)
    {
        for (var index = 0; index < students.All.Count; index += 1)
        {
            var student = students.All[index];
            if (isOpenWeek && (student == students.Featured || index % 2 == 0))
            {
                continue;
            }

            foreach (var theory in week.Parts.OfType<DemoTheory>())
            {
                db.PartCompletions.Add(new PartCompletion
                {
                    ContentLinkId = links[theory.Title].Id,
                    StudentId = student.Id,
                    CompletedAt = opensAt.AddDays(1 + index % 3)
                });
            }
        }
    }

    private void AddSubmissions(
        User teacher,
        ClassStudents students,
        DemoAssignment demo,
        ContentLink link,
        CoursePhase phase,
        int assignmentIndex,
        bool isLastAssignment,
        DateTimeOffset now)
    {
        var dueAt = link.DueAt!.Value;
        var lastDeadline = link.ResubmitDueAt.Append(dueAt).Max();

        for (var index = 0; index < students.All.Count; index += 1)
        {
            var student = students.All[index];
            var isFeatured = student == students.Featured;

            // En projektgrupp lämnar in och bedöms tillsammans, så alla i gruppen följer gruppens första elev.
            var planIndex = demo.HandIn == DemoHandIn.GroupRepo && _template is not null
                ? index / _studentsPerProjectGroup * _studentsPerProjectGroup
                : index;
            var planFeatured = students.All[planIndex] == students.Featured;

            // I repo-läge har ingen lämnat in innan deadline. Vid bedömning på plats lämnar eleven aldrig in
            // något: läraren sätter betyget direkt, och den som inte har gjort provet än står som ej påbörjad.
            var plan = UsesGitHub(demo) && _template is not null && now < dueAt
                ? new SubmissionPlan(SubmissionStatus.NotStarted)
                : PlanFor(phase, link, planIndex, planFeatured, isLastAssignment, now);
            var onSite = demo.HandIn == DemoHandIn.OnSite;
            if (onSite && plan.Status == SubmissionStatus.Submitted)
            {
                plan = plan with { Status = SubmissionStatus.NotStarted };
            }

            var submittedAt = plan.Status == SubmissionStatus.NotStarted || onSite
                ? (DateTimeOffset?)null
                : Earliest(
                    DaytimeAfter(dueAt, -(1 + index % 3), 14 + index % 6),
                    DaytimeAfter(now, -(1 + index % 2), 15));
            var submission = new Submission
            {
                ContentLinkId = link.Id,
                StudentId = student.Id,
                Status = plan.Status,
                SubmittedAt = submittedAt,
                BodyMd = submittedAt is null
                    ? null
                    : demo.Answer,
                PersonalDueAt = plan.PersonalDeadline
                    ? now.AddDays(10)
                    : null
            };
            db.Submissions.Add(submission);

            var answerFiles = _withFiles && submittedAt is not null
                ? demo.AnswerFiles ?? []
                : [];
            foreach (var fileName in answerFiles)
            {
                var file = new SubmissionFile { SubmissionId = submission.Id, FileName = fileName };
                db.SubmissionFiles.Add(file);
                _uploads.Add((file, $"submissions/{submission.Id}", fileName));
            }

            if (plan.Status == SubmissionStatus.Graded)
            {
                var level = LevelFor(planIndex, assignmentIndex, planFeatured);
                var failAllowed = DaytimeAfter(lastDeadline, 2, 10) <= now;
                if (level == GradeLevel.Fail && !failAllowed)
                {
                    level = GradeLevel.Pass;
                }

                if (level == GradeLevel.Distinction && demo.Scale == GradeScale.PassFail)
                {
                    level = GradeLevel.Pass;
                }

                var gradedAt = level == GradeLevel.Fail
                    ? DaytimeAfter(lastDeadline, 2, 10)
                    : Earliest(DaytimeAfter(dueAt, 3, 9 + index % 4), now.AddHours(-1));
                var exercise = demo.Scale == GradeScale.None;
                db.Grades.Add(new Grade
                {
                    SubmissionId = submission.Id,
                    GradedById = teacher.Id,
                    Level = exercise
                        ? null
                        : level,
                    Feedback = exercise
                        ? _exerciseFeedback[index % _exerciseFeedback.Length]
                        : FeedbackFor(level, planIndex),
                    GradedAt = gradedAt
                });

                if (isFeatured)
                {
                    db.Notifications.Add(new Notification
                    {
                        UserId = student.Id,
                        Kind = NotificationKind.SubmissionGraded,
                        ContentId = link.ContentId,
                        SubmissionId = submission.Id,
                        CreatedAt = gradedAt,
                        ReadAt = Earliest(gradedAt.AddDays(1), now)
                    });
                }
            }
            else if (plan.Status == SubmissionStatus.NeedsWork)
            {
                db.Grades.Add(new Grade
                {
                    SubmissionId = submission.Id,
                    GradedById = teacher.Id,
                    Level = null,
                    Feedback = _needsWorkFeedback[planIndex % _needsWorkFeedback.Length],
                    GradedAt = Earliest(DaytimeAfter(dueAt, 3, 9 + index % 4), now.AddHours(-1))
                });
            }
        }
    }

    /// <summary>
    /// Elevens läge på en uppgift. Det följer reglerna i GradingWindow: Komplettera bara medan en deadline återstår,
    /// IG bara när sista deadline har passerat, och en egen deadline bara efter klassens sista deadline.
    /// </summary>
    private static SubmissionPlan PlanFor(
        CoursePhase phase, ContentLink link, int index, bool isFeatured, bool isLastAssignment, DateTimeOffset now)
    {
        var dueAt = link.DueAt!.Value;
        var lastDeadline = link.ResubmitDueAt.Append(dueAt).Max();

        // Deadlinen har inte gått: några har lämnat in i förväg, resten har uppgiften kvar.
        if (now < dueAt)
        {
            return new SubmissionPlan(!isFeatured && index is 3 or 5
                ? SubmissionStatus.Submitted
                : SubmissionStatus.NotStarted);
        }

        // Kompletteringen pågår: läraren har bedömt en del, några ska komplettera och några väntar på bedömning.
        if (now < lastDeadline)
        {
            return new SubmissionPlan(isFeatured
                ? SubmissionStatus.Submitted
                : index switch
                {
                    1 or 2 => SubmissionStatus.NeedsWork,
                    3 or 4 => SubmissionStatus.Submitted,
                    7 => SubmissionStatus.NotStarted,
                    _ => SubmissionStatus.Graded
                });
        }

        if (isFeatured)
        {
            return new SubmissionPlan(SubmissionStatus.Graded);
        }

        // En elev missade slutuppgiften i den avslutade kursen och får automatiskt IG.
        if (phase == CoursePhase.Completed)
        {
            return new SubmissionPlan(isLastAssignment && index == 6
                ? SubmissionStatus.NotStarted
                : SubmissionStatus.Graded);
        }

        // I den pågående kursen väntar en inlämning på bedömning, och en sjuk elev har fått en egen deadline.
        if (link.ResubmitDueAt.Count == 0)
        {
            return index switch
            {
                5 => new SubmissionPlan(SubmissionStatus.Submitted),
                6 => new SubmissionPlan(SubmissionStatus.NotStarted, PersonalDeadline: true),
                _ => new SubmissionPlan(SubmissionStatus.Graded)
            };
        }

        return new SubmissionPlan(SubmissionStatus.Graded);
    }

    private static GradeLevel LevelFor(int studentIndex, int assignmentIndex, bool isFeatured)
    {
        if (isFeatured)
        {
            return assignmentIndex % 2 == 0
                ? GradeLevel.Pass
                : GradeLevel.Distinction;
        }

        var roll = (studentIndex * 7 + assignmentIndex * 3) % 10;
        return roll < 1
            ? GradeLevel.Fail
            : roll < 7
                ? GradeLevel.Pass
                : GradeLevel.Distinction;
    }

    private static string FeedbackFor(GradeLevel level, int studentIndex) => level switch
    {
        GradeLevel.Fail => _failFeedback[studentIndex % _failFeedback.Length],
        GradeLevel.Distinction => _distinctionFeedback[studentIndex % _distinctionFeedback.Length],
        _ => _passFeedback[studentIndex % _passFeedback.Length]
    };

    private void AddHelpRequest(User teacher, User student, ContentLink link, DateTimeOffset now, string message)
    {
        var askedAt = now.AddDays(-1);
        db.StudentHelpRequests.Add(new StudentHelpRequest
        {
            StudentId = student.Id,
            ContentLinkId = link.Id,
            Message = message,
            CreatedAt = askedAt,
            UpdatedAt = askedAt
        });
        db.Notifications.Add(new Notification
        {
            UserId = teacher.Id,
            Kind = NotificationKind.StudentHelpRequested,
            ContentId = link.ContentId,
            CreatedAt = askedAt
        });
    }

    private void AddClassNews(Group group, User teacher, Course current, DateTimeOffset now)
    {
        var content = new Content
        {
            AuthorId = teacher.Id,
            Type = ContentType.News,
            Title = $"Välkommen till {group.Name}",
            BodyMd = $"Nu är vi igång med **{current.Title}**. Schemat och uppgifterna finns i kursen. Hör av er "
                + "i inkorgen eller med hjälpknappen på en uppgift om något är oklart.",
            Status = ContentStatus.Published,
            CreatedAt = now.AddDays(-20)
        };
        db.Content.Add(content);
        db.ContentLinks.Add(new ContentLink
        {
            ContentId = content.Id,
            GroupId = group.Id,
            PublishAt = now.AddDays(-20),
            IsPinned = true
        });
    }

    private static DateTimeOffset OpensAt(DateOnly startsOn, DemoWeek week) =>
        SwedishTime(startsOn.AddDays(week.OpensOnDay), new TimeOnly(7, 0));

    private static DateTimeOffset DeadlineOn(DateOnly day) => SwedishTime(day, new TimeOnly(23, 59));

    // Svensk tid utan tidszonsdatabas, som kanske saknas i containern: sommartid gäller från sista söndagen i mars
    // till sista söndagen i oktober. Postgres vill ha UTC, så tiden räknas om.
    private static DateTimeOffset SwedishTime(DateOnly day, TimeOnly local)
    {
        var summer = day >= LastSunday(day.Year, 3) && day < LastSunday(day.Year, 10);
        var offset = TimeSpan.FromHours(summer
            ? 2
            : 1);
        return new DateTimeOffset(day.ToDateTime(local), offset).ToUniversalTime();
    }

    private static DateOnly LastSunday(int year, int month)
    {
        var day = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        return day.AddDays(-(int)day.DayOfWeek);
    }

    // En tid på dagen dagarna efter ett datum, så att inlämningar och bedömningar inte ligger mitt i natten.
    private static DateTimeOffset DaytimeAfter(DateTimeOffset moment, int days, int hour) =>
        SwedishTime(DateOnly.FromDateTime(moment.UtcDateTime).AddDays(days), new TimeOnly(hour, 15));

    private static DateTimeOffset Earliest(DateTimeOffset first, DateTimeOffset second) =>
        first < second
            ? first
            : second;

    private sealed record ClassStudents(IReadOnlyList<User> All, User? Featured, string? HelpMessage);

    private sealed record SubmissionPlan(SubmissionStatus Status, bool PersonalDeadline = false);
}
