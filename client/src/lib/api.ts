// Klienten och API:et har samma origin (Vite-proxy lokalt, backend serverar klienten i prod),
// så alla anrop går till relativa sökvägar som /api/groups. Ingen bas-URL eller CORS behövs.

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

export type Method = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'

export interface ApiResponse<T> {
  status: number
  data: T
}

function parseBody(text: string, contentType: string | null): unknown {
  if (!text) {
    return undefined
  }
  return contentType?.includes('json')
    ? JSON.parse(text)
    : text
}

// Backend skickar fel som en JSON-sträng ("title is required") eller som ProblemDetails.
function errorMessage(body: unknown, fallback: string): string {
  if (typeof body === 'string' && body) {
    return body
  }
  if (body && typeof body === 'object') {
    const problem = body as {detail?: string, title?: string}
    return problem.detail ?? problem.title ?? fallback
  }
  return fallback
}

/** Som request, men returnerar även statuskoden. */
async function send<T>(method: Method, path: string, body?: unknown): Promise<ApiResponse<T>> {
  const headers: Record<string, string> = {Accept: 'application/json'}
  const init: RequestInit = {method, headers}

  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
    init.body = JSON.stringify(body)
  }

  // När inloggning via Entra ID finns läggs Authorization-headern till här.
  const response = await fetch(path, init)
  const data = parseBody(await response.text(), response.headers.get('Content-Type'))

  if (!response.ok) {
    throw new ApiError(response.status, errorMessage(data, response.statusText || `HTTP ${response.status}`))
  }

  // data är undefined för svar utan body, t.ex. 201 Created.
  return {status: response.status, data: data as T}
}

async function request<T>(method: Method, path: string, body?: unknown): Promise<T> {
  return (await send<T>(method, path, body)).data
}

export const api = {
  send,
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body),
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, body),
  patch: <T>(path: string, body?: unknown) => request<T>('PATCH', path, body),
  delete: <T>(path: string) => request<T>('DELETE', path),
}
