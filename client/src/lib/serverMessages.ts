// Backend svarar med korta engelska meddelanden ("title is required"). Det är kontraktet som
// backendens tester och API-dokumentet bygger på, så texten översätts här, där den visas.
// Meddelanden som backend redan skriver på svenska (behörighet, GitHub, Microsoft) släpps igenom
// av errorMessage. Nytt meddelande i backend: lägg raden här, annars visas en allmän svensk text.

const exact: Record<string, string> = {
  // Saknade och ogiltiga fält
  'title is required': 'Titeln saknas.',
  'news text is required': 'Skriv något i nyheten.',
  'name is required': 'Namnet saknas.',
  'links is required': 'Ordningen saknas.',
  'content and link are required': 'Innehåll och plats i kursen saknas.',
  'name must be 1 to 200 characters': 'Namnet måste vara mellan 1 och 200 tecken.',
  'email is not a valid address': 'E-postadressen är inte giltig.',
  'email is already in use': 'E-postadressen används redan av en annan användare.',
  'points must be between 1 and 400': 'Poängen måste vara mellan 1 och 400.',
  'endsOn must be on or after startsOn': 'Slutdatumet kan inte ligga före startdatumet.',
  'to must be after from': 'Slutdatumet måste ligga efter startdatumet.',
  'the range can be at most 62 days': 'Perioden får vara högst 62 dagar.',

  // Dubbletter
  'the school already has a group with this name': 'Det finns redan en klass med det namnet.',
  'the school already has a course with this code': 'Det finns redan en kurs med den koden.',
  'user is already in the group': 'Personen är redan med i gruppen.',

  // Finns inte
  'course not found': 'Kursen finns inte längre.',
  'group not found': 'Klassen eller gruppen finns inte längre.',
  'user not found': 'Användaren finns inte längre.',
  'school not found': 'Skolan finns inte längre.',
  'submission not found': 'Inlämningen finns inte längre.',
  'assignment not found': 'Uppgiften finns inte längre.',
  'content not found': 'Materialet finns inte längre.',
  'module not found': 'Veckan finns inte längre.',
  'link not found': 'Delen finns inte längre.',
  'part not found': 'Delen finns inte längre.',
  'member not found': 'Personen är inte med i gruppen.',
  'user does not exist': 'Användaren finns inte.',
  'school does not exist': 'Skolan finns inte.',
  'user belongs to another school': 'Användaren tillhör en annan skola.',

  // Roller
  'user must be a teacher or an admin': 'Användaren måste vara lärare eller admin.',
  'only a teacher can be a teacher in a group': 'Bara en lärare kan vara lärare i en klass.',
  'you cannot deactivate yourself': 'Du kan inte spärra dig själv.',
  'you cannot change your own role': 'Du kan inte ändra din egen roll.',
  'a committee cannot read courses, unlink them first': 'En kommitté kan inte läsa kurser. Koppla bort kurserna först.',

  // Inlämning och bedömning
  'this assignment needs a text answer or files': 'Uppgiften behöver en text eller filer.',
  'this content takes no submissions': 'Det här materialet tar inte emot inlämningar.',
  'this assignment does not take files': 'Uppgiften tar inte emot filer.',
  'already graded': 'Inlämningen är redan bedömd.',
  'nothing has been submitted yet': 'Det finns ingen inlämning att bedöma än.',
  'a submission that needs work gets feedback, not a grade': 'Ber du om komplettering får eleven återkoppling, inte betyg.',
  'a fail grade requires a comment': 'Icke godkänd kräver en kommentar.',
  'news cannot be completed': 'Nyheter kan inte klarmarkeras.',

  // Filer och profilbild
  'every file needs a name': 'Varje fil måste ha ett namn.',
  'a profile picture must be a png, jpg, gif or webp image': 'Välj en bild i formatet PNG, JPG, GIF eller WebP.',
  'the image is empty': 'Bilden är tom.',
  'this part is completed by handing in the assignment': 'Delen klarmarkeras när uppgiften lämnas in.',

  // Veckor och delar
  'the part has student activity and cannot be moved': 'Delen har elevaktivitet och kan inte flyttas.',
  'the part has student activity, files or a repository and cannot be deleted':
    'Delen har elevaktivitet, filer eller ett repo och kan inte tas bort.',
  'module does not belong to the course': 'Veckan hör inte till kursen.',
  'link is not in a week': 'Delen ligger inte i någon vecka.',
  'link is already in that module': 'Delen ligger redan i den veckan.',
  'moduleIds must list every module in the course once': 'Alla veckor i kursen måste vara med i ordningen, en gång var.',
  'moduleIds must only contain weeks in the course': 'Bara veckor i kursen kan väljas.',
  'moduleIds must only contain weeks in the source course': 'Bara veckor i den valda kursen kan väljas.',
  'links must list every part in the week once': 'Alla delar i veckan måste vara med i ordningen, en gång var.',
  'a part cannot be blocked by itself': 'En del kan inte vara låst av sig själv.',
  'blockedByLinkIds must only contain parts in the same week': 'Bara delar i samma vecka kan låsa en del.',
  'news cannot be a blocker': 'En nyhet kan inte låsa en del.',
  'news cannot be part of the roadmap': 'Nyheter ingår inte i färdplanen.',

  // Microsoft och inloggning
  'microsoft sign-in is not set up': 'Inloggning med Microsoft är inte påslagen.',
  'another school is linked to the tenant': 'En annan skola är redan kopplad till den Microsoft-katalogen.',
  'invalid id token': 'Inloggningen hos Microsoft kunde inte verifieras.',
  'wrong email or password': 'Fel e-post eller lösenord.',

  // Standardtexter från ASP.NET
  'One or more validation errors occurred.': 'Kontrollera uppgifterna och försök igen.',
}

const patterns: [RegExp, (...groups: string[]) => string][] = [
  [/^password must be at least (\d+) characters$/, (count) => `Lösenordet måste vara minst ${count} tecken.`],
  [/^lines must contain between 1 and (\d+) students$/, (count) => `Klistra in mellan 1 och ${count} elever.`],
  [/^the part is locked by: (.+)$/, (parts) => `Delen är låst tills du har klarat: ${parts}.`],
  [/^cycle: (.+)$/, (path) => `Ordningen blir en ring: ${path}.`],
  [/^at most (\d+) files can be uploaded at once$/, (count) => `Högst ${count} filer kan laddas upp åt gången.`],
  [/^the files together can be at most (\d+) MB$/, (size) => `Filerna får vara högst ${size} MB tillsammans.`],
  [/^(.+) has a file type that is not allowed$/, (name) => `${name} har en filtyp som inte är tillåten.`],
  [/^(.+) is larger than (\d+) MB$/, (name, size) => `${name} är större än ${size} MB.`],
  [/^(.+) is empty$/, (name) => `${name} är tom.`],
  [/^a profile picture can be at most (\d+) MB$/, (size) => `Bilden får vara högst ${size} MB.`],
  [/^the week contains student activity in "(.+)" and cannot be deleted$/,
    (title) => `Veckan har elevaktivitet i "${title}" och kan inte tas bort.`],
  [/^the course has student work in "(.+)" and cannot be deleted$/,
    (title) => `Kursen har elevarbete i "${title}" och kan inte tas bort.`],
]

/** Den svenska texten för ett känt backendmeddelande, eller null när meddelandet inte är känt. */
export function swedishServerMessage(message: string): string | null {
  const known = exact[message]
  if (known) {
    return known
  }
  for (const [pattern, build] of patterns) {
    const match = pattern.exec(message)
    if (match) {
      return build(...match.slice(1))
    }
  }
  return null
}

/** Meddelanden med å, ä eller ö är redan skrivna på svenska av backend, till exempel för GitHub. */
export function isSwedish(message: string): boolean {
  return /[åäöÅÄÖ]/.test(message)
}

/** Allmän svensk text för en statuskod, när backend inte har skickat något som går att visa. */
export function fallbackForStatus(status: number): string {
  if (status === 404) {
    return 'Det du söker finns inte längre.'
  }
  if (status === 413) {
    return 'Filerna är för stora att skicka. Högst 50 MB åt gången.'
  }
  if (status === 409) {
    return 'Det går inte att göra just nu, eftersom det krockar med något som redan finns.'
  }
  if (status >= 500) {
    return 'Något gick fel hos servern. Försök igen om en stund.'
  }
  if (status === 400 || status === 422) {
    return 'Det gick inte att spara. Kontrollera uppgifterna och försök igen.'
  }
  return 'Något gick fel. Försök igen.'
}
