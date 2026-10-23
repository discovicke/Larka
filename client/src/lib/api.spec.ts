import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest'

import {api, ApiError} from './api'

const fetchMock = vi.fn<typeof fetch>()

function respond(status: number, body: string, contentType = 'application/json') {
  fetchMock.mockResolvedValueOnce(
    new Response(body || null, {status, headers: {'Content-Type': contentType}}),
  )
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

    const result = await api.get<{version: string}>('/version')

    expect(result).toEqual({version: '1.0.0', commit: 'abc'})
    expect(fetchMock).toHaveBeenCalledWith('/version', expect.objectContaining({method: 'GET'}))
  })

  it('returnerar text när svaret inte är JSON', async () => {
    respond(200, 'OK', 'text/plain')

    expect(await api.get<string>('/health')).toBe('OK')
  })

  it('skickar body som JSON', async () => {
    respond(201, JSON.stringify({id: '1'}))

    await api.post('/api/groups/1/posts', {title: 'Hej'})

    const [, init] = fetchMock.mock.calls[0]!
    expect(init?.method).toBe('POST')
    expect(init?.body).toBe(JSON.stringify({title: 'Hej'}))
    expect(init?.headers).toMatchObject({'Content-Type': 'application/json'})
  })

  it('klarar svar utan body', async () => {
    respond(201, '')

    expect(await api.post('/api/groups/1/courses/2')).toBeUndefined()
  })

  it('kastar ApiError med status vid fel', async () => {
    respond(404, '', 'text/plain')

    const error = await api.get('/api/nope').catch((thrown: unknown) => thrown)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(404)
  })

  it('tar med felmeddelandet från backend', async () => {
    respond(400, '"type must be news|theory|assignment"')

    await expect(api.post('/api/groups/1/posts', {})).rejects.toThrow(
      'type must be news|theory|assignment',
    )
  })

  it('packar upp felmeddelanden som skickas som JSON-sträng', async () => {
    respond(400, '"title is required"')

    const error = await api.post('/api/content', {}).catch((thrown: unknown) => thrown)

    expect((error as ApiError).message).toBe('title is required')
  })

  it('send returnerar status och data', async () => {
    respond(201, JSON.stringify({id: '1'}))

    const response = await api.send('POST', '/api/content', {title: 'Hej'})

    expect(response).toEqual({status: 201, data: {id: '1'}})
  })
})
