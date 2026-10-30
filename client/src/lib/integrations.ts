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

export async function setGitHubEnabled(enabled: boolean): Promise<GitHubConnection> {
  const {data} = await api.PUT('/api/integrations/github/enabled', {body: {enabled}})
  return data!
}

export async function refreshGitHubRepos(linkId: string): Promise<void> {
  await api.POST('/api/links/{id}/repos/refresh', {params: {path: {id: linkId}}})
}

export async function lockGitHubRepos(linkId: string): Promise<void> {
  await api.POST('/api/links/{id}/repos/lock', {params: {path: {id: linkId}}})
}

export type GitHubTemplateRepo = Schema<'GitHubTemplateRepoDto'>
export type GitHubTemplateCommit = Schema<'GitHubTemplateCommitDto'>

export async function fetchGitHubTemplates(): Promise<GitHubTemplateRepo[]> {
  const {data} = await api.GET('/api/integrations/github/templates')
  return data ?? []
}

export async function fetchGitHubTemplateTests(fullName: string): Promise<string[]> {
  const [owner, repo] = fullName.split('/', 2)
  const {data} = await api.GET('/api/integrations/github/templates/{owner}/{repo}/tests', {
    params: {path: {owner, repo}},
  })
  return data ?? []
}

export async function fetchGitHubTemplateCommit(fullName: string): Promise<GitHubTemplateCommit> {
  const [owner, repo] = fullName.split('/', 2)
  const {data} = await api.GET('/api/integrations/github/templates/{owner}/{repo}/latest-commit', {
    params: {path: {owner, repo}},
  })
  return data!
}

export type UnlinkedGitHubStudent = Schema<'UnlinkedGitHubStudentDto'>

export async function fetchUnlinkedGitHubStudents(courseId: string): Promise<UnlinkedGitHubStudent[]> {
  const {data} = await api.GET('/api/courses/{id}/students/unlinked-github', {
    params: {path: {id: courseId}},
  })
  return data ?? []
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
