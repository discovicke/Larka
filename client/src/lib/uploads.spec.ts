import {describe, expect, it} from 'vitest'

import {ApiError, errorMessage} from './api'
import {UPLOAD_ACCEPT, UPLOAD_MAX_FILE_BYTES, uploadProblem} from './uploads'

function file(name: string, size = 3): File {
  const value = new File(['abc'], name)
  Object.defineProperty(value, 'size', {value: size})
  return value
}

describe('uploadProblem', () => {
  it('godtar tillåtna filtyper oavsett skiftläge', () => {
    expect(uploadProblem([file('Rapport.PDF'), file('bild.webp'), file('data.xlsx')])).toBeNull()
  })

  it('säger vilken fil som inte går, och varför', () => {
    expect(uploadProblem([file('a.pdf'), file('virus.exe')])).toContain('virus.exe har en filtyp som inte är tillåten.')
    expect(uploadProblem([file('tom.pdf', 0)])).toBe('tom.pdf är tom.')
    expect(uploadProblem([file('stor.pdf', UPLOAD_MAX_FILE_BYTES + 1)])).toBe('stor.pdf är större än 20 MB.')
  })

  it('stoppar för många och för stora filer tillsammans', () => {
    expect(uploadProblem(Array.from({length: 11}, (_, index) => file(`${index}.pdf`))))
      .toBe('Högst 10 filer kan laddas upp åt gången.')
    expect(uploadProblem([file('a.pdf', 19 * 1024 * 1024), file('b.pdf', 19 * 1024 * 1024), file('c.pdf', 19 * 1024 * 1024)]))
      .toBe('Filerna får vara högst 50 MB tillsammans.')
  })

  it('visar samma filtyper som backend tar emot', () => {
    expect(UPLOAD_ACCEPT).toBe('.pdf,.png,.jpg,.jpeg,.gif,.webp,.txt,.md,.docx,.pptx,.xlsx,.zip')
  })
})

describe('uppladdningsfel från servern på svenska', () => {
  it.each([
    ['virus.exe has a file type that is not allowed', 'virus.exe har en filtyp som inte är tillåten.'],
    ['stor.pdf is larger than 20 MB', 'stor.pdf är större än 20 MB.'],
    ['tom.pdf is empty', 'tom.pdf är tom.'],
    ['at most 10 files can be uploaded at once', 'Högst 10 filer kan laddas upp åt gången.'],
    ['the files together can be at most 50 MB', 'Filerna får vara högst 50 MB tillsammans.'],
    ['a profile picture can be at most 5 MB', 'Bilden får vara högst 5 MB.'],
    ['a profile picture must be a png, jpg, gif or webp image', 'Välj en bild i formatet PNG, JPG, GIF eller WebP.'],
  ])('%s', (message, swedish) => {
    expect(errorMessage(new ApiError(400, message))).toBe(swedish)
  })

  it('förklarar en för stor förfrågan (413)', () => {
    expect(errorMessage(new ApiError(413, ''))).toBe('Filerna är för stora att skicka. Högst 50 MB åt gången.')
  })
})
