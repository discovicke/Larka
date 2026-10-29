import {reactive} from 'vue'

// Namn som brödsmulorna visar men som bara sidan själv känner till (gruppen, uppgiften, eleven).
// Kurser har useCourses; för resten sätter sidan namnet när det är hämtat, och brödsmulan visar
// en reservtext tills dess.
const labels = reactive(new Map<string, string>())

export function useCrumbLabels() {
  function setCrumbLabel(key: string, label: string | undefined) {
    if (label) {
      labels.set(key, label)
    }
  }

  function crumbLabel(key: string, fallback: string): string {
    return labels.get(key) ?? fallback
  }

  return {setCrumbLabel, crumbLabel}
}
