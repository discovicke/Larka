import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest'

import {api, ApiError} from './api'

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
})
