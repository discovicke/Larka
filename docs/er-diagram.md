# RebornLMS – ER-modell

Datamodellen bakom API:et. Content (nyheter, teori och uppgifter) är kärnan: det lever fristående, skrivs alltid av en användare och publiceras till grupper och kurser via `ContentLink`.

Varje relation är en riktig foreign key i databasen med ett index. Modellen gäller både SQLite (lokalt och i tester) och Postgres (Supabase i prod).

Modellen beslutades i #34 och byggs i #37 (organisation), #38 (content), #39 (uppgifter och inlämningar) och #40 (notiser).

```mermaid
erDiagram
    School ||--o{ User : "har"
    School ||--o{ Group : "har"
    School ||--o{ Course : "har"
    Group ||--o{ GroupCourse : "läser"
    Course ||--o{ GroupCourse : "läses av"
    Group ||--o{ Enrollment : "har medlemmar"
    User ||--o{ Enrollment : "är med i"

    User ||--o{ Content : "skriver"
    Content ||--o{ ContentLink : "publiceras via"
    Group |o--o{ ContentLink : "får"
    Course |o--o{ ContentLink : "får"
    Content ||--o| AssignmentSpec : "om Assignment"
    Content ||--o{ Attachment : "har"
    Content ||--o{ Comment : "har"
    User ||--o{ Comment : "skriver"

    ContentLink ||--o{ Submission : "tar emot"
    User ||--o{ Submission : "lämnar in"
    Submission ||--o{ SubmissionFile : "har"
    Submission ||--o| StudentRepo : "om repo-läge"
    Submission ||--o| Grade : "får"
    User ||--o{ Grade : "rättar"

    User ||--o{ Notification : "får"
    Content |o--o{ Notification : "gäller"
    Submission |o--o{ Notification : "gäller"
    User ||--o{ PushSubscription : "har enheter"

    School {
        uuid Id PK
        string Name
    }
    User {
        uuid Id PK
        uuid SchoolId FK
        string Name
        string Email UK
        UserRole Role "Admin | Teacher | Student"
    }
    Group {
        uuid Id PK
        uuid SchoolId FK
        GroupType Type "Class | Committee | Other"
        string Name
        string Term
    }
    Course {
        uuid Id PK
        uuid SchoolId FK
        string Title
        string Code
    }
    GroupCourse {
        uuid GroupId PK, FK
        uuid CourseId PK, FK
    }
    Enrollment {
        uuid UserId PK, FK
        uuid GroupId PK, FK
        GroupRole Role "Student | Teacher | Mentor"
    }
    Content {
        uuid Id PK
        uuid AuthorId FK
        ContentType Type "News | Theory | Assignment"
        string Title
        string BodyMd
        ContentStatus Status "Draft | Published"
        timestamptz CreatedAt
    }
    ContentLink {
        uuid Id PK
        uuid ContentId FK
        uuid GroupId FK "nullable"
        uuid CourseId FK "nullable"
        timestamptz PublishAt
        timestamptz DueAt "nullable"
        bool IsPinned
    }
    AssignmentSpec {
        uuid ContentId PK, FK
        int MaxScore "nullable"
        SubmissionMode SubmissionMode "None | File | Repo | FileOrRepo"
        string TemplateRepo "nullable"
    }
    Attachment {
        uuid Id PK
        uuid ContentId FK
        string FileName
        string Url
        long SizeBytes
    }
    Comment {
        uuid Id PK
        uuid ContentId FK
        uuid AuthorId FK
        string BodyMd
        timestamptz CreatedAt
    }
    Submission {
        uuid Id PK
        uuid ContentLinkId FK
        uuid StudentId FK
        SubmissionStatus Status "NotStarted | Submitted | NeedsWork | Graded"
        string BodyMd "nullable"
        timestamptz SubmittedAt "nullable"
    }
    SubmissionFile {
        uuid Id PK
        uuid SubmissionId FK
        string FileName
        string Url
        long SizeBytes
    }
    StudentRepo {
        uuid SubmissionId PK, FK
        string RepoFullName
        string LastSha "nullable"
        CheckStatus CheckStatus "Pending | Green | Red"
        timestamptz UpdatedAt
    }
    Grade {
        uuid SubmissionId PK, FK
        uuid GradedById FK
        int Score "nullable"
        string Feedback
        timestamptz GradedAt
    }
    Notification {
        uuid Id PK
        uuid UserId FK
        NotificationKind Kind "NewsPublished | AssignmentPublished | DeadlineSoon | SubmissionNeedsWork | SubmissionGraded"
        uuid ContentId FK "nullable"
        uuid SubmissionId FK "nullable"
        timestamptz CreatedAt
        timestamptz ReadAt "nullable"
    }
    PushSubscription {
        uuid Id PK
        uuid UserId FK
        string Endpoint UK
        string P256dh
        string Auth
        timestamptz CreatedAt
    }
```

## Varför relationerna ser ut så här

### Organisation

- **School → User, Group, Course.** Allt hör till en skola. En användare tillhör en skola.
- **GroupCourse (Group ↔ Course, M:N).** En klass läser flera kurser och en kurs kan läsas av flera klasser. Det här är den enda kopplingen mellan kurs och grupp. Det fanns tidigare även `Course.ProgramGroupId` ("ägande program"), men två kopplingar som kan säga olika saker togs bort till förmån för en. Committee-grupper har inga rader här.
- **Enrollment (User ↔ Group, M:N) med `Role`.** Rollen gäller _i gruppen_, så en lärare kan vara student i en annan grupp. Den är en enum i stället för fri text, så ett stavfel inte kan göra att en elev försvinner ur sin grupp. `User.Role` är fortfarande användarens roll i systemet (Admin, Teacher, Student).

### Content

- **Content skrivs alltid av en User (`AuthorId`, obligatorisk).** Content lever för sig själv och är inte bundet till en grupp, så samma teorimaterial kan återanvändas för flera klasser.
- **ContentLink publicerar content** till en grupp, en kurs eller båda (minst en av `GroupId` och `CourseId` måste vara satt, det kontrolleras med en check constraint):
  - bara grupp: t.ex. en nyhet till NET25
  - bara kurs: alla grupper som läser kursen (via GroupCourse)
  - grupp + kurs: kursen så som den går för just den gruppen
- **`PublishAt`, `DueAt` och `IsPinned` ligger på länken, inte på content.** Samma uppgift kan delas ut till två klasser med olika deadlines.
- **AssignmentSpec (1:0..1 med Content)** finns bara när `Type = Assignment`. `SubmissionMode` avgör hur man lämnar in: `None` (bara läsa och göra), `File`, `Repo` (GitHub) eller `FileOrRepo`. En "laboration" är alltså en uppgift med inlämning, inte en egen tabell. GitHub är ett tillval, så kurser utan programmering lämnar in filer.
- **Attachment och Comment** hör till content, inte till en länk. Bilagorna följer med när innehållet delas ut igen.

### Inlämningar

- **Submission hör till en ContentLink, inte till content.** Det är på länken deadlinen finns, och en elev lämnar in till uppgiften så som den delades ut till hens grupp. Det finns en rad per elev och länk (unik), och `Status` (`NotStarted`, `Submitted`, `NeedsWork`, `Graded`) visar var eleven är. Statusbytena styr notiserna.
- **SubmissionFile (1:N)** innehåller uppladdade filer, **StudentRepo (1:0..1)** elevens GitHub-repo när uppgiften använder repo-läge. Repot skapas när uppgiften delas ut, så då finns submission-raden redan med status `NotStarted`.
- **Grade (1:0..1 med Submission)** sparar vem som rättade (`GradedById`) och när. `Score` ligger mellan 0 och uppgiftens `MaxScore`. Uppgifter utan `MaxScore` får bara feedback. Vid komplettering sätts status `NeedsWork` och Grade har feedback men ingen poäng, och eleven kan lämna in igen. En rättad inlämning går inte att lämna in igen.

### Notiser

- **Notification** är _vad som hände_ för en användare (t.ex. ny nyhet, uppgift rättad), med valfri länk till content eller submission. Den visas i appen och `ReadAt` markerar den som läst.
- **PushSubscription** är _vart_ en pushnotis ska skickas: en rad per enhet (mobil, dator) som användaren har tillåtit notiser på. Den är `Endpoint` + nycklarna webbläsaren ger. Saknas rader skickas ingen push, men notisen finns ändå i appen.

## Delete-regler

| Relation                                                   | Vid delete av förälder                               |
| ---------------------------------------------------------- | ---------------------------------------------------- |
| School → User, Group, Course                               | blockeras (Restrict)                                 |
| Group, Course → GroupCourse, Enrollment, ContentLink       | blockeras                                            |
| User → Content, Comment, Submission, Grade, Enrollment     | blockeras                                            |
| Content → ContentLink, AssignmentSpec, Attachment, Comment | kaskad                                               |
| ContentLink → Submission                                   | blockeras (elevarbete ska inte försvinna av misstag) |
| Submission → SubmissionFile, StudentRepo, Grade            | kaskad                                               |
| User → Notification, PushSubscription                      | kaskad                                               |
| Content, Submission → Notification                         | sätts till null                                      |

Principen: det man äger raderas med en (content med sina bilagor, en inlämning med sina filer). Delade saker (skolor, grupper, kurser, användare) och elevernas arbete går inte att radera så länge något pekar på dem.

## Unika regler

- `User.Email`
- `Group (SchoolId, Name, Term)`
- `Course (SchoolId, Code)`
- `Submission (ContentLinkId, StudentId)`
- `PushSubscription.Endpoint`

## Enums

Enums sparas som text i databasen (t.ex. `"Student"`), så att tabellerna går att läsa direkt i Supabase.
