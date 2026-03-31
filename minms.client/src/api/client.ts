import { getAccessToken } from '@/composables/useAuth'

export async function apiFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const headers = new Headers(init?.headers)
  const t = getAccessToken()
  if (t) headers.set('Authorization', `Bearer ${t}`)
  return fetch(input, { ...init, headers })
}
