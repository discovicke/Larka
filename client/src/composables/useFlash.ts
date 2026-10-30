import {ref} from 'vue'

// Ett kort besked som följer med till nästa sida, till exempel "Bedömningen är sparad." Sidan som
// visar det hämtar det med `takeFlash`, så att det bara syns en gång.
const message = ref('')

export function useFlash() {
  function setFlash(text: string) {
    message.value = text
  }

  function takeFlash(): string {
    const text = message.value
    message.value = ''
    return text
  }

  return {setFlash, takeFlash}
}
