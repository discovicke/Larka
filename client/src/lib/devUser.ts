// Utvecklingsläget kan visa appen som en annan användare genom att skicka X-Dev-User (#140).
// Hela filen tas bort när inloggning (#141) finns.

const storageKey = 'rebornlms.devUser'

/** Användaren som valts i "Visa som", eller null för den inloggade. */
export function devUserId(): string | null {
  try {
    return localStorage.getItem(storageKey)
  } catch {
    // localStorage kan vara avstängt (t.ex. i privat läge); då visas den inloggade användaren.
    return null
  }
}

export function setDevUser(id: string | null): void {
  try {
    if (id) {
      localStorage.setItem(storageKey, id)
    } else {
      localStorage.removeItem(storageKey)
    }
  } catch {
    // Utan localStorage fungerar appen som vanligt, bara utan bytet.
  }
}

/** Headern som låter API:et svara som den valda användaren. Tom när ingen är vald. */
export function devHeaders(): Record<string, string> {
  const id = devUserId()
  return id
    ? {'X-Dev-User': id}
    : {}
}
