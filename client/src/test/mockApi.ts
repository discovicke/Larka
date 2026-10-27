import {vi} from 'vitest'

// Låtsas vara backend i testerna: byter ut fetch och sparar det som skickats.
// vitest.config.ts återställer fetch efter varje test (unstubGlobals).

export type Handler = (method: string, path: string, body: unknown) => {status: number, body?: unknown}

/** Det som skickats, som metod, sökväg och body. */
export const requests: {method: string, path: string, body: unknown}[] = []

// Headrarna sparas vid sidan av requests, så att äldre kontroller av requests är oförändrade.
const sentHeaderList: Headers[] = []

export function mockApi(handler: Handler) {
  requests.length = 0
  sentHeaderList.length = 0
  // api-klienten skickar en Request, medan /api/me anropas med en sökväg och headers.
  vi.stubGlobal('fetch', async (input: Request | string, init?: RequestInit) => {
    const request = input instanceof Request
      ? input
      : new Request(new URL(input, window.location.origin), init)
    const path = new URL(request.url).pathname
    const contentType = request.headers.get('Content-Type')
    // jsdom kan inte läsa tillbaka multipart från en Request. Testerna kontrollerar
    // i stället FormData.append, så vi rör inte den body:n här.
    const text = contentType?.includes('multipart/form-data')
      ? ''
      : await request.text()
    const isJson = contentType?.includes('json') ?? false
    const body = text && isJson
      ? JSON.parse(text)
      : text || undefined
    requests.push({method: request.method, path, body})
    sentHeaderList.push(new Headers(request.headers))
    const response = handler(request.method, path, body)
    return new Response(response.body === undefined
      ? null
      : JSON.stringify(response.body), {
      status: response.status,
      headers: {'Content-Type': 'application/json'},
    })
  })
}

/** Bodyn i det senaste anropet med metoden och sökvägen. */
export function sent(method: string, path: string): unknown {
  const matching = requests.filter((request) => request.method === method && request.path === path)
  return matching[matching.length - 1]?.body
}

/** Headrarna i det senaste anropet med metoden och sökvägen. */
export function sentHeaders(method: string, path: string): Headers | undefined {
  const indices = requests
    .map((request, index) => (request.method === method && request.path === path
      ? index
      : -1))
    .filter((index) => index >= 0)
  const last = indices[indices.length - 1]
  return last === undefined
    ? undefined
    : sentHeaderList[last]
}
