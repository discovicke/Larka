import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest'

import {api, ApiError, errorMessage, unwrap} from './api'
import {auth} from './auth'
import {redirectToLogin} from './auth/redirect'
import {signInForTests, signOutForTests} from '@/test/auth'

// jsdom låter oss inte byta ut window.location.assign, så omdirigeringen mockas i stället.
vi.mock('./auth/redirect', () => ({
  redirectToLogin: vi.fn(),
}))

const fetchMock = vi.fn<(request: Request) => Promise<Response>>()

function respond(status: number, body: string, contentType = 'application/json') {
  fetchMock.mockResolvedValueOnce(
    new Response(body || null, {status, headers: {'Content-Type': contentType}}),
  )
}

function sentRequest(): Request {
  return fetchMock.mock.calls[0]![0]
}

describe('api', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    fetchMock.mockReset()
    vi.unstubAllGlobals()
  })

  it('tolkar JSON-svar', async () => {
    respond(200, JSON.stringify({version: '1.0.0', commit: 'abc'}))

    const {data} = await api.GET('/version')

    expect(data).toEqual({version: '1.0.0', commit: 'abc'})
    expect(new URL(sentRequest().url).pathname).toBe('/version')
    expect(sentRequest().method).toBe('GET')
  })

  it('fyller i sökvägens parametrar och skickar body som JSON', async () => {
    respond(201, JSON.stringify({userId: 'u1', groupId: 'g1', role: 'Student'}))

    await api.POST('/api/groups/{id}/members', {params: {path: {id: 'g1'}}, body: {userId: 'u1'}})

    const request = sentRequest()
    expect(new URL(request.url).pathname).toBe('/api/groups/g1/members')
    expect(request.method).toBe('POST')
    expect(request.headers.get('Content-Type')).toBe('application/json')
    expect(await request.json()).toEqual({userId: 'u1'})
  })

  it('klarar svar utan body', async () => {
    respond(201, '')

    const {data, response} = await api.POST('/api/groups/{id}/courses/{courseId}', {
      params: {path: {id: '1', courseId: '2'}},
    })

    expect(data).toBeUndefined()
    expect(response.status).toBe(201)
  })

  it('kastar ApiError med status vid fel', async () => {
    respond(404, '', 'text/plain')

    const error = await api.GET('/api/groups/{id}', {params: {path: {id: 'nope'}}})
      .catch((thrown: unknown) => thrown)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(404)
  })

  it('packar upp felmeddelanden som skickas som JSON-sträng', async () => {
    respond(400, '"title is required"')

    const error = await api.POST('/api/content', {body: {type: 'News', title: '', bodyMd: ''}})
      .catch((thrown: unknown) => thrown)

    expect((error as ApiError).message).toBe('title is required')
  })

  it('läser meddelandet från ProblemDetails', async () => {
    respond(500, JSON.stringify({title: 'Something broke'}), 'application/problem+json')

    await expect(api.GET('/api/schools')).rejects.toThrow('Something broke')
  })

  it('säger att användaren inte har behörighet vid 403', async () => {
    respond(403, '')

    const error = await api.GET('/api/schools')
      .catch((thrown: unknown) => thrown)

    expect(errorMessage(error)).toBe('Du har inte behörighet.')
  })

  it('visar det vanliga felet för andra statuskoder', async () => {
    respond(400, '"title is required"')

    const error = await api.POST('/api/content', {body: {type: 'News', title: '', bodyMd: ''}})
      .catch((thrown: unknown) => thrown)

    expect(errorMessage(error)).toBe('Titeln saknas.')
  })
})

describe('errorMessage på svenska', () => {
  it('översätter backendens engelska meddelanden', () => {
    expect(errorMessage(new ApiError(400, 'the school already has a group with this name')))
      .toBe('Det finns redan en klass med det namnet.')
    expect(errorMessage(new ApiError(400, 'password must be at least 8 characters')))
      .toBe('Lösenordet måste vara minst 8 tecken.')
    expect(errorMessage(new ApiError(409, 'the week contains student activity in "Lab 1" and cannot be deleted')))
      .toBe('Veckan har elevaktivitet i "Lab 1" och kan inte tas bort.')
  })

  it('släpper igenom meddelanden som backend redan skrivit på svenska', () => {
    expect(errorMessage(new ApiError(403, 'Ditt konto är spärrat.'))).toBe('Du har inte behörighet.')
    expect(errorMessage(new ApiError(400, 'GitHub är inte konfigurerat. Saknas: App-id.')))
      .toBe('GitHub är inte konfigurerat. Saknas: App-id.')
  })

  it('ger en svensk text efter statuskoden när meddelandet är okänt', () => {
    expect(errorMessage(new ApiError(404, 'something we never mapped'))).toBe('Det du söker finns inte längre.')
    expect(errorMessage(new ApiError(409, 'something we never mapped')))
      .toBe('Det går inte att göra just nu, eftersom det krockar med något som redan finns.')
    expect(errorMessage(new ApiError(400, 'Bad Request')))
      .toBe('Det gick inte att spara. Kontrollera uppgifterna och försök igen.')
    expect(errorMessage(new ApiError(500, 'Internal Server Error')))
      .toBe('Något gick fel hos servern. Försök igen om en stund.')
  })

  it('säger det när servern inte går att nå', () => {
    expect(errorMessage(new TypeError('Failed to fetch')))
      .toBe('Kunde inte nå servern. Kontrollera anslutningen och försök igen.')
  })
})

describe('api med inloggning', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock)
    vi.mocked(redirectToLogin).mockClear()
  })

  afterEach(() => {
    fetchMock.mockReset()
    vi.unstubAllGlobals()
    signOutForTests()
  })

  it('skickar med Bearer-token när man är inloggad', async () => {
    signInForTests()
    respond(200, JSON.stringify({id: 'u1'}))

    await api.GET('/api/me')

    expect(sentRequest().headers.get('Authorization')).toBe('Bearer test-token')
  })

  it('skickar ingen Authorization när man är utloggad', async () => {
    respond(200, JSON.stringify({version: '1.0.0', commit: 'abc'}))

    await api.GET('/version')

    expect(sentRequest().headers.get('Authorization')).toBeNull()
  })

  it('ett 401 loggar ut och skickar till inloggningen med sidan man var på', async () => {
    signInForTests()
    window.history.replaceState({}, '', '/groups')
    respond(401, JSON.stringify({detail: 'Ogiltigt token'}))

    await api.GET('/api/me').catch(() => undefined)

    expect(auth.isSignedIn()).toBe(false)
    expect(redirectToLogin).toHaveBeenCalledWith('/groups')
  })

  it('ett 401 från inloggningen skickar inte vidare', async () => {
    respond(401, JSON.stringify({detail: 'Fel e-post eller lösenord.'}))

    await api.POST('/api/auth/login', {body: {username: 'elev', password: 'fel'}})
      .catch(() => undefined)

    expect(redirectToLogin).not.toHaveBeenCalled()
  })
})

describe('unwrap', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    fetchMock.mockReset()
    vi.unstubAllGlobals()
  })

  it('returnerar data', async () => {
    respond(200, JSON.stringify({version: '1.0.0', commit: 'abc'}))

    const result = await api.GET('/version')

    expect(unwrap(result)).toEqual({version: '1.0.0', commit: 'abc'})
  })

  it('kastar ApiError när svaret saknar innehåll', async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, {status: 204}))

    const result = await api.GET('/version')

    expect(() => unwrap(result)).toThrow(ApiError)
    expect(() => unwrap(result)).toThrow('Svaret saknar innehåll.')
  })
})
