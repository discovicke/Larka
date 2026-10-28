import {api, type Schema} from './api'

/** Svaret från GET /api/integrations/github. Bara sanerad config, aldrig hemligheter. */
export type GitHubConnection = Schema<'GitHubConnectionDto'>

export async function fetchGitHubConnection(): Promise<GitHubConnection> {
  const {data} = await api.GET('/api/integrations/github')
  return data!
}
