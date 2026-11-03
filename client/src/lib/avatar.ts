// Samma gränser som AvatarPolicy i backend, så att användaren får ett svenskt fel innan något skickas.
export const AVATAR_MAX_BYTES = 5 * 1024 * 1024

const AVATAR_EXTENSIONS = ['.png', '.jpg', '.jpeg', '.gif', '.webp']

/** Filtyperna som filväljaren visar. */
export const AVATAR_ACCEPT = 'image/png,image/jpeg,image/gif,image/webp'

/** Varför bilden inte går att använda, eller null om den går bra. */
export function avatarProblem(file: File): string | null {
  const name = file.name.toLowerCase()
  if (!AVATAR_EXTENSIONS.some((extension) => name.endsWith(extension))) {
    return 'Välj en bild i formatet PNG, JPG, GIF eller WebP.'
  }
  if (file.size === 0) {
    return 'Bilden är tom.'
  }
  if (file.size > AVATAR_MAX_BYTES) {
    return 'Bilden får vara högst 5 MB.'
  }
  return null
}
