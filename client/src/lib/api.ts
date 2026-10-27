import createClient, {type Middleware} from 'openapi-fetch'

import type {components, paths} from './api-types'
import {devUserId} from './devUser'

// Klienten och API:et har samma origin (Vite-proxy lokalt, backend serverar klienten i prod),
// så alla anrop går till samma origin som sidan. Ingen CORS behövs.
//
// Sökvägar, parametrar, bodies och svar är typade från backendens OpenAPI-dokument
// (client/openapi.json -> src/lib/api-types.ts med npm run gen:api).

/** En typ från OpenAPI-dokumentet, t.ex. Schema<'GroupDto'>. */
export type Schema<Name extends keyof components['schemas']> = components['schemas'][Name]

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

/** Felmeddelandet att visa för användaren, t.ex. "title is required" från backend. */
export function errorMessage(exception: unknown): string {
  // 403 betyder att API:et nekat åtgärden, oavsett vad svaret heter i övrigt.
  if (exception instanceof ApiError && exception.status === 403) {
    return 'Du har inte behörighet.'
  }
  return exception instanceof Error
    ? exception.message
    : 'Något gick fel. Försök igen.'
}

export function parseBody(text: string, contentType: string | null): unknown {
  if (!text) {
    return undefined
  }
  return contentType?.includes('json')
    ? JSON.parse(text)
    : text
}

// Backend skickar fel som en JSON-sträng ("title is required") eller som ProblemDetails.
export function messageFromBody(body: unknown, fallback: string): string {
  if (typeof body === 'string' && body) {
    return body
  }
  if (body && typeof body === 'object') {
    const problem = body as {detail?: string, title?: string}
    return problem.detail ?? problem.title ?? fallback
  }
  return fallback
}

// Svar som inte är 2xx kastas som ApiError, så anropen bara behöver hantera det lyckade svaret.
const throwOnError: Middleware = {
  async onResponse({response}) {
    if (response.ok) {
      return undefined
    }

    const body = parseBody(await response.clone().text(), response.headers.get('Content-Type'))
    throw new ApiError(response.status, messageFromBody(body, response.statusText || `HTTP ${response.status}`))
  },
}

// Utvecklingsläget kan svara som en annan användare än den inloggade (#140).
// Headern läggs bara på i dev-bygget; tas bort när inloggning (#141) finns.
const withDevUser: Middleware = {
  onRequest({request}) {
    const userId = import.meta.env.DEV
      ? devUserId()
      : null
    if (userId) {
      request.headers.set('X-Dev-User', userId)
    }
  },
}

export const api = createClient<paths>({
  baseUrl: typeof window === 'undefined'
    ? ''
    : window.location.origin,
  // Slås upp vid varje anrop, så att testerna kan byta ut fetch.
  // När inloggning via Entra ID finns läggs Authorization-headern till i en middleware.
  fetch: (request) => globalThis.fetch(request),
})

api.use(throwOnError)
api.use(withDevUser)
