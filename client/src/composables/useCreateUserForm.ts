import {computed, reactive, ref} from 'vue'

import {api, unwrap, type Schema} from '@/lib/api'
import {fetchMicrosoftSettings} from '@/lib/auth/microsoft'

type UserRole = Schema<'UserRole'>

// Samma regel som EmailRule i backend: något före och efter @, och en punkt i domänen.
const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const minPasswordLength = 8

/**
 * Formuläret Skapa användare: fälten, kontrollerna och anropet till POST /api/users. Samma formulär används
 * på sidan Användare och i Lägg till medlem, så att båda ställena kontrollerar och skickar likadant.
 * Fälten visas av CreateUserFields.
 */
export function useCreateUserForm(defaultRole: UserRole = 'Student') {
  const form = reactive({name: '', email: '', role: defaultRole, password: ''})
  // Felen visas först när någon har försökt spara, utom lösenordets längd som syns medan man skriver.
  const submitted = ref(false)
  // Med Microsoft-inloggning loggar alla in med skolans konto, så admin sätter inga lösenord.
  const microsoftSignIn = ref(false)

  const nameError = computed(() => submitted.value && !form.name.trim()
    ? 'Ange ett namn.'
    : '')
  const emailError = computed(() => submitted.value && !emailPattern.test(form.email.trim())
    ? 'Ange en giltig e-postadress.'
    : '')
  const passwordError = computed(() => form.password !== '' && form.password.length < minPasswordLength
    ? `Lösenordet måste vara minst ${minPasswordLength} tecken.`
    : '')

  /** Tömmer fälten och kollar om skolan har Microsoft-inloggning. Anropas när dialogen öppnas. */
  function reset(role: UserRole = defaultRole) {
    Object.assign(form, {name: '', email: '', role, password: ''})
    submitted.value = false
    void fetchMicrosoftSettings()
      .then((settings) => {
        microsoftSignIn.value = settings !== null
      })
      .catch(() => {
        microsoftSignIn.value = false
      })
  }

  /** Visar felen och svarar om formuläret kan skickas. */
  function validate() {
    submitted.value = true
    return !nameError.value && !emailError.value && !passwordError.value
  }

  /** Skapar användaren i skolan. Kastar ApiError med serverns text, t.ex. när e-posten redan används. */
  async function create(schoolId: string) {
    return unwrap(await api.POST('/api/users', {
      body: {
        schoolId,
        name: form.name.trim(),
        email: form.email.trim(),
        role: form.role,
        // Med Microsoft-inloggning syns inget lösenordsfält, så inget lösenord skickas heller.
        password: microsoftSignIn.value
          ? null
          : form.password || null,
      },
    }))
  }

  return {form, microsoftSignIn, nameError, emailError, passwordError, reset, validate, create}
}

export type CreateUserForm = ReturnType<typeof useCreateUserForm>
