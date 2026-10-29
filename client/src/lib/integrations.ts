import {api, type Schema} from './api'

/** Svaret från GET /api/integrations/github. Bara sanerad config, aldrig hemligheter. */
export type GitHubConnection = Schema<'GitHubConnectionDto'>

export async function fetchGitHubConnection(): Promise<GitHubConnection> {
  const {data} = await api.GET('/api/integrations/github')
  return data!
}

export type GitHubInstallLink = Schema<'GitHubInstallLinkDto'>

export async function fetchGitHubInstallUrl(): Promise<GitHubInstallLink> {
  const {data} = await api.GET('/api/integrations/github/install')
  return data!
}

export async function connectGitHub(
  installationId?: number | null,
  state?: string | null,
): Promise<GitHubConnection> {
  const {data} = await api.POST('/api/integrations/github/connect', {
    body: {
      installationId: installationId ?? null,
      state: state ?? null,
    },
  })
  return data!
}

export async function disconnectGitHub(): Promise<void> {
  await api.DELETE('/api/integrations/github')
}

export type GitHubLink = Schema<'GitHubLinkDto'>

export async function fetchGitHubLink(): Promise<GitHubLink> {
  const {data} = await api.GET('/api/github/link')
  return data!
}

export async function unlinkGitHub(): Promise<void> {
  await api.DELETE('/api/github/link')
}

export async function fetchGitHubAuthorizeUrl(redirectTo?: string): Promise<string> {
  const {data} = await api.GET('/api/github/authorize', {
    params: {query: {redirect_to: redirectTo}},
  })
  return data!.url
}
