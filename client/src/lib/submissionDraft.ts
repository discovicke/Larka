// Elevens osparade svar på en uppgift sparas i webbläsaren medan hen skriver. Då försvinner inget
// om sidan laddas om, nätet går ner eller fliken stängs av misstag. Filer sparas inte, bara texten.

const PREFIX = 'larka:submission-draft'

function keyOf(userId: string | null | undefined, linkId: string) {
  return `${PREFIX}:${userId ?? 'okand'}:${linkId}`
}

/** Utkastet för uppgiften, eller null om det inte finns något eller lagringen är avstängd. */
export function loadDraft(userId: string | null | undefined, linkId: string): string | null {
  try {
    return localStorage.getItem(keyOf(userId, linkId))
  } catch {
    return null
  }
}

/** Sparar utkastet. En tom text tar bort det. */
export function saveDraft(userId: string | null | undefined, linkId: string, bodyMd: string) {
  try {
    if (bodyMd.trim() === '') {
      localStorage.removeItem(keyOf(userId, linkId))
    } else {
      localStorage.setItem(keyOf(userId, linkId), bodyMd)
    }
  } catch {
    // Privat läge eller full lagring: formuläret fungerar ändå, bara utan utkast.
  }
}

export function clearDraft(userId: string | null | undefined, linkId: string) {
  try {
    localStorage.removeItem(keyOf(userId, linkId))
  } catch {
    // Se saveDraft.
  }
}
