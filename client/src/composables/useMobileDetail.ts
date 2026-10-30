import {useMediaQuery} from '@vueuse/core'
import {nextTick, type Ref, watch} from 'vue'
import {useRouter} from 'vue-router'

// Lista och artikel i samma vy: bredare än md visas båda sida vid sida, smalare visas en i taget.
// Valt inlägg ligger i ?post. Utan det visas bara listan, med det bara artikeln.
export function useMobileDetail(openId: Ref<string | undefined>) {
  const router = useRouter()
  const isMobile = useMediaQuery('(max-width: 767px)')

  // push på mobil så att bakåtknappen går tillbaka till listan. replace på desktop, där listan alltid syns.
  function open(id: string) {
    const location = {query: {post: id}}
    return isMobile.value
      ? router.push(location)
      : router.replace(location)
  }

  // En artikel som öppnas ska börja överst, inte där listan var skrollad.
  watch(openId, async (id) => {
    if (isMobile.value && id) {
      await nextTick()
      window.scrollTo(0, 0)
    }
  })

  return {isMobile, open}
}
