import {ref} from 'vue'

import {fetchTeaching} from '@/lib/coursesApi'

// Antal inlämningar som väntar på bedömning i lärarens kurser, delat av sidomenyns märke och lärarens Idag.
const gradingCount = ref(0)

async function loadGradingCount(): Promise<void> {
  try {
    gradingCount.value = (await fetchTeaching()).toGradeTotal
  } catch {
    // Märket i sidomenyn är bara en hjälp; sidan fungerar utan det.
  }
}

function setGradingCount(count: number): void {
  gradingCount.value = count
}

export function useGradingCount() {
  return {gradingCount, loadGradingCount, setGradingCount}
}
