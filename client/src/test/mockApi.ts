import {vi} from 'vitest'

// Låtsas vara backend i testerna: byter ut fetch och sparar det som skickats.
// vitest.config.ts återställer fetch efter varje test (unstubGlobals).

export type Handler = (method: string, path: string, body: unknown) => {status: number, body?: unknown}

/** Det som skickats, som metod, sökväg och body. */
export const requests: {method: string, path: string, body: unknown}[] = []

export function mockApi(handler: Handler) {
  requests.length = 0
  vi.stubGlobal('fetch', async (request: Request) => {
    const path = new URL(request.url).pathname
    const text = await request.text()
    // Filer skickas som multipart, och då sparas texten som den är så att testet kan leta efter fälten i den.
    const isJson = request.headers.get('Content-Type')?.includes('json') ?? false
    const body = text && isJson
      ? JSON.parse(text)
      : text || undefined
    requests.push({method: request.method, path, body})
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
