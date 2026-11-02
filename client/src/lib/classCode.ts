// Adresskoden en klass fångar, med samma normalisering som backend (SchoolAddress.NormalizeCode): utan versaler och
// tecken blir ".NET25" och "NET 25" båda "net25". En elev hamnar i klassen när adressen slutar på ".net25@…".
export function normalizeClassCode(text: string): string {
  return text.replace(/[^\p{L}\p{N}]/gu, '').toLowerCase()
}

// Koden som går att skriva i en adress: bokstäver, siffror och högst en bokstav sist ("net25", "te22a"). "NET 25
// DevOps" ger null, för ingen har en adress som slutar på ".net25devops@…".
export function classCodeFromName(name: string): string | null {
  const code = normalizeClassCode(name)
  return /^\p{L}+\p{N}+\p{L}?$/u.test(code)
    ? code
    : null
}
