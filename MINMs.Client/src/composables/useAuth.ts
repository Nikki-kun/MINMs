import { computed, ref } from 'vue'

export const AUTH_STORAGE_KEY = 'minms.accessToken'
const STORAGE_KEY = AUTH_STORAGE_KEY

export type AuthUser = {
  login: string
  username: string
  userCreatedAt?: string
}

const token = ref<string | null>(null)
const storedUser = ref<AuthUser | null>(null)

function hydrateFromStorage() {
  if (typeof localStorage === 'undefined') return
  token.value = localStorage.getItem(STORAGE_KEY)
  storedUser.value = readStoredUser()
}

hydrateFromStorage()

function readStoredUser(): AuthUser | null {
  if (typeof localStorage === 'undefined') return null
  const raw = localStorage.getItem('minms.user')
  if (!raw) return null
  try {
    const u = JSON.parse(raw) as Partial<AuthUser> & { username?: string; login?: string }
    if (typeof u.username !== 'string' || typeof u.login !== 'string' || !u.login.trim()) return null
    return {
      login: u.login,
      username: u.username,
      userCreatedAt: u.userCreatedAt,
    }
  } catch {
    return null
  }
}

export function getAccessToken(): string | null {
  return token.value
}

export function useAuth() {
  const isAuthenticated = computed(() => token.value != null && token.value.length > 0)

  function persistSession(accessToken: string, user: AuthUser) {
    token.value = accessToken
    storedUser.value = user
    localStorage.setItem(STORAGE_KEY, accessToken)
    localStorage.setItem('minms.user', JSON.stringify(user))
  }

  function clearSession() {
    token.value = null
    storedUser.value = null
    localStorage.removeItem(STORAGE_KEY)
    localStorage.removeItem('minms.user')
  }

  return {
    token,
    user: storedUser,
    isAuthenticated,
    persistSession,
    clearSession,
  }
}
