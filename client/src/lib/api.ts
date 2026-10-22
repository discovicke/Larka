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

type Method = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'

async function request<T>(method: Method, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  const init: RequestInit = { method, headers }

  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
    init.body = JSON.stringify(body)
  }

  // När inloggning via Entra ID finns läggs Authorization-headern till här.
  const response = await fetch(path, init)
  const text = await response.text()

  if (!response.ok) {
    throw new ApiError(response.status, text || response.statusText)
  }

  // T.ex. 201 Created utan body.
  if (!text) {
    return undefined as T
  }

  const isJson = response.headers.get('Content-Type')?.includes('application/json') ?? false
  return (isJson ? JSON.parse(text) : text) as T
}

export const api = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body),
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, body),
  patch: <T>(path: string, body?: unknown) => request<T>('PATCH', path, body),
  delete: <T>(path: string) => request<T>('DELETE', path),
}
