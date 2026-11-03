// Samma gränser som FileUploadPolicy i backend, så att fel syns på svenska innan något skickas.
// Backend prövar ändå allt själv.
export const UPLOAD_MAX_FILE_BYTES = 20 * 1024 * 1024
export const UPLOAD_MAX_REQUEST_BYTES = 50 * 1024 * 1024
export const UPLOAD_MAX_FILES = 10

const EXTENSIONS = ['.pdf', '.png', '.jpg', '.jpeg', '.gif', '.webp', '.txt', '.md', '.docx', '.pptx', '.xlsx', '.zip']

/** Filtyperna som filväljaren visar. */
export const UPLOAD_ACCEPT = EXTENSIONS.join(',')

/** Varför filerna inte går att ladda upp tillsammans, eller null om de går bra. */
export function uploadProblem(files: readonly File[]): string | null {
  if (files.length > UPLOAD_MAX_FILES) {
    return `Högst ${UPLOAD_MAX_FILES} filer kan laddas upp åt gången.`
  }
  for (const file of files) {
    const name = file.name.toLowerCase()
    if (!EXTENSIONS.some((extension) => name.endsWith(extension))) {
      return `${file.name} har en filtyp som inte är tillåten. Välj pdf, bilder, text, Office-filer eller zip.`
    }
    if (file.size === 0) {
      return `${file.name} är tom.`
    }
    if (file.size > UPLOAD_MAX_FILE_BYTES) {
      return `${file.name} är större än 20 MB.`
    }
  }
  if (files.reduce((sum, file) => sum + file.size, 0) > UPLOAD_MAX_REQUEST_BYTES) {
    return 'Filerna får vara högst 50 MB tillsammans.'
  }
  return null
}
