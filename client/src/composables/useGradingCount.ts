import {ref} from 'vue'

import {api} from '@/lib/api'

// Antal inlämningar som väntar på bedömning i lärarens kurser, delat av sidomenyns märke och lärarens Idag.
const gradingCount = ref(0)

async function loadGradingCount(): Promise<void> {
  try {
    const result = await api.GET('/api/me/teaching')
    gradingCount.value = result.data?.toGradeTotal ?? gradingCount.value
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
