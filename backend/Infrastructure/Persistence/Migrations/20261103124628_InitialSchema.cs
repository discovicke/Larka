using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Schools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    EntraTenantId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schools", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: true),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Courses_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Groups_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchoolGitHubInstallations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallationId = table.Column<long>(type: "bigint", nullable: false),
                    Org = table.Column<string>(type: "text", nullable: false),
                    Connected = table.Column<bool>(type: "boolean", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ConnectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DisconnectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolGitHubInstallations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolGitHubInstallations_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    ExternalId = table.Column<string>(type: "text", nullable: true),
                    DeactivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GitHubLogin = table.Column<string>(type: "text", nullable: true),
                    AvatarKey = table.Column<string>(type: "text", nullable: true),
                    MutedNotificationKinds = table.Column<int[]>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Modules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    PublishAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Modules_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroupCourses",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupCourses", x => new { x.GroupId, x.CourseId });
                    table.ForeignKey(
                        name: "FK_GroupCourses_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupCourses_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    BodyMd = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Content", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Content_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseTeachers",
                columns: table => new
                {
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseTeachers", x => new { x.CourseId, x.UserId });
                    table.ForeignKey(
                        name: "FK_CourseTeachers_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseTeachers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Enrollments",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollments", x => new { x.UserId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_Enrollments_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrollments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentSpecs",
                columns: table => new
                {
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    GradeScale = table.Column<string>(type: "text", nullable: false),
                    SubmissionModes = table.Column<string[]>(type: "text[]", nullable: false),
                    CompletionRule = table.Column<string>(type: "text", nullable: false, defaultValue: "Submitted"),
                    TemplateRepo = table.Column<string>(type: "text", nullable: true),
                    RepoNamePattern = table.Column<string>(type: "text", nullable: true),
                    WorkMode = table.Column<int>(type: "integer", nullable: false),
                    LockAtDeadline = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    FeedbackPullRequest = table.Column<bool>(type: "boolean", nullable: false),
                    AllowLate = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentSpecs", x => x.ContentId);
                    table.ForeignKey(
                        name: "FK_AssignmentSpecs_Content_ContentId",
                        column: x => x.ContentId,
                        principalTable: "Content",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachments_Content_ContentId",
                        column: x => x.ContentId,
                        principalTable: "Content",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    BodyMd = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comments_Content_ContentId",
                        column: x => x.ContentId,
                        principalTable: "Content",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Comments_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContentLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    PublishAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResubmitDueAt = table.Column<List<DateTimeOffset>>(type: "timestamp with time zone[]", nullable: false, defaultValueSql: "'{}'"),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentLinks", x => x.Id);
                    table.CheckConstraint("CK_ContentLinks_GroupOrCourse", "\"GroupId\" IS NOT NULL OR \"CourseId\" IS NOT NULL");
                    table.CheckConstraint("CK_ContentLinks_ModuleNeedsCourse", "\"ModuleId\" IS NULL OR \"CourseId\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_ContentLinks_Content_ContentId",
                        column: x => x.ContentId,
                        principalTable: "Content",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentLinks_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentLinks_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentLinks_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    TestPattern = table.Column<string>(type: "text", nullable: true),
                    IsManual = table.Column<bool>(type: "boolean", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentRequirements_AssignmentSpecs_ContentId",
                        column: x => x.ContentId,
                        principalTable: "AssignmentSpecs",
                        principalColumn: "ContentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentGroups_ContentLinks_ContentLinkId",
                        column: x => x.ContentLinkId,
                        principalTable: "ContentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentReferences_ContentLinks_SourceLinkId",
                        column: x => x.SourceLinkId,
                        principalTable: "ContentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssignmentReferences_ContentLinks_TargetLinkId",
                        column: x => x.TargetLinkId,
                        principalTable: "ContentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistTicks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemIndex = table.Column<int>(type: "integer", nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistTicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistTicks_ContentLinks_ContentLinkId",
                        column: x => x.ContentLinkId,
                        principalTable: "ContentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChecklistTicks_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContentLinkDependencies",
                columns: table => new
                {
                    BlockedLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockingLinkId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentLinkDependencies", x => new { x.BlockedLinkId, x.BlockingLinkId });
                    table.CheckConstraint("CK_ContentLinkDependencies_NotSelf", "\"BlockedLinkId\" <> \"BlockingLinkId\"");
                    table.ForeignKey(
                        name: "FK_ContentLinkDependencies_ContentLinks_BlockedLinkId",
                        column: x => x.BlockedLinkId,
                        principalTable: "ContentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentLinkDependencies_ContentLinks_BlockingLinkId",
                        column: x => x.BlockingLinkId,
                        principalTable: "ContentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartCompletions_ContentLinks_ContentLinkId",
                        column: x => x.ContentLinkId,
                        principalTable: "ContentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartCompletions_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentHelpRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentHelpRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentHelpRequests_ContentLinks_ContentLinkId",
                        column: x => x.ContentLinkId,
                        principalTable: "ContentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentHelpRequests_Users_ResolvedById",
                        column: x => x.ResolvedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StudentHelpRequests_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentGroupMembers",
                columns: table => new
                {
                    AssignmentGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentGroupMembers", x => new { x.AssignmentGroupId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_AssignmentGroupMembers_AssignmentGroups_AssignmentGroupId",
                        column: x => x.AssignmentGroupId,
                        principalTable: "AssignmentGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssignmentGroupMembers_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    BodyMd = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Submissions_AssignmentGroups_AssignmentGroupId",
                        column: x => x.AssignmentGroupId,
                        principalTable: "AssignmentGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Submissions_ContentLinks_ContentLinkId",
                        column: x => x.ContentLinkId,
                        principalTable: "ContentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Submissions_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Grades",
                columns: table => new
                {
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GradedById = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<string>(type: "text", nullable: true),
                    Feedback = table.Column<string>(type: "text", nullable: false),
                    GradedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grades", x => x.SubmissionId);
                    table.ForeignKey(
                        name: "FK_Grades_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Grades_Users_GradedById",
                        column: x => x.GradedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Content_ContentId",
                        column: x => x.ContentId,
                        principalTable: "Content",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Notifications_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentRepos",
                columns: table => new
                {
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    RepoFullName = table.Column<string>(type: "text", nullable: false),
                    TemplateRepo = table.Column<string>(type: "text", nullable: true),
                    AssignmentTitle = table.Column<string>(type: "text", nullable: true),
                    RepoNamePattern = table.Column<string>(type: "text", nullable: true),
                    DesiredRepoName = table.Column<string>(type: "text", nullable: true),
                    ProvisioningState = table.Column<int>(type: "integer", nullable: false),
                    ProvisioningError = table.Column<string>(type: "text", nullable: true),
                    ProvisioningAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextProvisioningAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InitialSha = table.Column<string>(type: "text", nullable: true),
                    InitialCommitAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProvisionedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSha = table.Column<string>(type: "text", nullable: true),
                    LastCommitMessage = table.Column<string>(type: "text", nullable: true),
                    LastCommitAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CommitCount = table.Column<int>(type: "integer", nullable: false),
                    CheckStatus = table.Column<string>(type: "text", nullable: false),
                    CheckStatusChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ChecksPassed = table.Column<int>(type: "integer", nullable: false),
                    ChecksTotal = table.Column<int>(type: "integer", nullable: false),
                    SubmissionSha = table.Column<string>(type: "text", nullable: true),
                    LockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockError = table.Column<string>(type: "text", nullable: true),
                    ReopenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentRepos", x => x.SubmissionId);
                    table.ForeignKey(
                        name: "FK_StudentRepos_AssignmentGroups_AssignmentGroupId",
                        column: x => x.AssignmentGroupId,
                        principalTable: "AssignmentGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentRepos_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionFiles_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentRepoCheckRuns",
                columns: table => new
                {
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalKey = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Conclusion = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentRepoCheckRuns", x => new { x.SubmissionId, x.ExternalKey });
                    table.ForeignKey(
                        name: "FK_StudentRepoCheckRuns_StudentRepos_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "StudentRepos",
                        principalColumn: "SubmissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Schools",
                columns: new[] { "Id", "EntraTenantId", "Name" },
                values: new object[] { new Guid("00000000-0000-0000-0000-00000000000a"), null, "Placeholder-skolan" });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "AvatarKey", "DeactivatedAt", "Email", "ExternalId", "GitHubLogin", "MutedNotificationKinds", "Name", "PasswordHash", "Role", "SchoolId" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), null, null, "placeholder@larka.local", null, null, new int[0], "Placeholder", null, "Admin", new Guid("00000000-0000-0000-0000-00000000000a") });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentGroupMembers_StudentId",
                table: "AssignmentGroupMembers",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentGroups_ContentLinkId_Position",
                table: "AssignmentGroups",
                columns: new[] { "ContentLinkId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentReferences_SourceLinkId_Position",
                table: "AssignmentReferences",
                columns: new[] { "SourceLinkId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentReferences_SourceLinkId_TargetLinkId",
                table: "AssignmentReferences",
                columns: new[] { "SourceLinkId", "TargetLinkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentReferences_TargetLinkId",
                table: "AssignmentReferences",
                column: "TargetLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRequirements_ContentId_Position",
                table: "AssignmentRequirements",
                columns: new[] { "ContentId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_ContentId",
                table: "Attachments",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTicks_ContentLinkId",
                table: "ChecklistTicks",
                column: "ContentLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTicks_StudentId_ContentLinkId_ItemIndex",
                table: "ChecklistTicks",
                columns: new[] { "StudentId", "ContentLinkId", "ItemIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Comments_AuthorId",
                table: "Comments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ContentId",
                table: "Comments",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_Content_AuthorId",
                table: "Content",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLinkDependencies_BlockingLinkId",
                table: "ContentLinkDependencies",
                column: "BlockingLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLinks_ContentId",
                table: "ContentLinks",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLinks_CourseId",
                table: "ContentLinks",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLinks_GroupId",
                table: "ContentLinks",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLinks_ModuleId_Position",
                table: "ContentLinks",
                columns: new[] { "ModuleId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_Courses_SchoolId_Code",
                table: "Courses",
                columns: new[] { "SchoolId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseTeachers_UserId",
                table: "CourseTeachers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_GroupId",
                table: "Enrollments",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Grades_GradedById",
                table: "Grades",
                column: "GradedById");

            migrationBuilder.CreateIndex(
                name: "IX_GroupCourses_CourseId",
                table: "GroupCourses",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_SchoolId_Name",
                table: "Groups",
                columns: new[] { "SchoolId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Modules_CourseId_Position",
                table: "Modules",
                columns: new[] { "CourseId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ContentId",
                table: "Notifications",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_SubmissionId",
                table: "Notifications",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PartCompletions_ContentLinkId_StudentId",
                table: "PartCompletions",
                columns: new[] { "ContentLinkId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartCompletions_StudentId",
                table: "PartCompletions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolGitHubInstallations_SchoolId",
                table: "SchoolGitHubInstallations",
                column: "SchoolId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schools_EntraTenantId",
                table: "Schools",
                column: "EntraTenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentHelpRequests_ContentLinkId_StudentId",
                table: "StudentHelpRequests",
                columns: new[] { "ContentLinkId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentHelpRequests_ResolvedById",
                table: "StudentHelpRequests",
                column: "ResolvedById");

            migrationBuilder.CreateIndex(
                name: "IX_StudentHelpRequests_StudentId",
                table: "StudentHelpRequests",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRepos_AssignmentGroupId",
                table: "StudentRepos",
                column: "AssignmentGroupId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionFiles_SubmissionId",
                table: "SubmissionFiles",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_AssignmentGroupId",
                table: "Submissions",
                column: "AssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_ContentLinkId_StudentId",
                table: "Submissions",
                columns: new[] { "ContentLinkId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_StudentId",
                table: "Submissions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ExternalId",
                table: "Users",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_GitHubLogin",
                table: "Users",
                column: "GitHubLogin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_SchoolId",
                table: "Users",
                column: "SchoolId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssignmentGroupMembers");

            migrationBuilder.DropTable(
                name: "AssignmentReferences");

            migrationBuilder.DropTable(
                name: "AssignmentRequirements");

            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "ChecklistTicks");

            migrationBuilder.DropTable(
                name: "Comments");

            migrationBuilder.DropTable(
                name: "ContentLinkDependencies");

            migrationBuilder.DropTable(
                name: "CourseTeachers");

            migrationBuilder.DropTable(
                name: "Enrollments");

            migrationBuilder.DropTable(
                name: "Grades");

            migrationBuilder.DropTable(
                name: "GroupCourses");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PartCompletions");

            migrationBuilder.DropTable(
                name: "SchoolGitHubInstallations");

            migrationBuilder.DropTable(
                name: "StudentHelpRequests");

            migrationBuilder.DropTable(
                name: "StudentRepoCheckRuns");

            migrationBuilder.DropTable(
                name: "SubmissionFiles");

            migrationBuilder.DropTable(
                name: "AssignmentSpecs");

            migrationBuilder.DropTable(
                name: "StudentRepos");

            migrationBuilder.DropTable(
                name: "Submissions");

            migrationBuilder.DropTable(
                name: "AssignmentGroups");

            migrationBuilder.DropTable(
                name: "ContentLinks");

            migrationBuilder.DropTable(
                name: "Content");

            migrationBuilder.DropTable(
                name: "Groups");

            migrationBuilder.DropTable(
                name: "Modules");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Courses");

            migrationBuilder.DropTable(
                name: "Schools");
        }
    }
}
