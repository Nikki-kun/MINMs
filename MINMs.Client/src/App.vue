<script setup lang="ts">
import { useAuth } from '@/composables/useAuth'
import { LogIn, LogOut, MessagesSquare, Search, Send, Settings, UserCircle, Users } from 'lucide-vue-next'
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

const route = useRoute()
const router = useRouter()
const { isAuthenticated, user, clearSession } = useAuth()
const isHome = computed(() => route.path === '/')

const searchPeople = ref('')

watch(
  () => ({ path: route.path, q: route.query.q }),
  ({ path, q }) => {
    if (path !== '/users') return
    searchPeople.value = typeof q === 'string' ? q : ''
  },
  { immediate: true }
)

function logout() {
  clearSession()
  void router.push('/login')
}

function goPeopleSearch() {
  const q = searchPeople.value.trim()
  void router.push({ path: '/users', query: q ? { q } : {} })
}

const navLinkClass =
  'rounded-lg px-3 py-2 text-sm font-medium text-zinc-300 ring-1 ring-transparent transition-colors hover:bg-white/10 hover:text-white'
const navActiveClass = 'bg-white/15 text-white shadow-sm ring-white/20'

function navClass(path: string) {
  return [navLinkClass, route.path === path ? navActiveClass : '']
}

function authEntryClass() {
  const onAuthPages = route.path === '/login' || route.path === '/register'
  return [navLinkClass, onAuthPages ? navActiveClass : '', 'inline-flex items-center gap-1.5']
}

const iconBtnClass =
  'inline-flex size-10 items-center justify-center rounded-lg text-zinc-300 ring-1 ring-transparent transition-colors hover:bg-white/10 hover:text-white'
const iconBtnActiveClass = 'bg-white/15 text-white ring-white/20'

function iconNavClass(path: string) {
  return [iconBtnClass, route.path === path ? iconBtnActiveClass : '']
}
</script>

<template>
  <div class="flex min-h-dvh flex-col">
    <header
      class="sticky top-0 z-50 border-b border-white/10 bg-zinc-950/55 backdrop-blur-xl backdrop-saturate-150"
    >
      <div class="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-3 px-5 py-3 sm:gap-4 sm:py-4">
        <RouterLink
          to="/"
          class="group flex shrink-0 items-center gap-2.5 text-zinc-100 no-underline"
        >
          <span
            class="flex size-10 items-center justify-center rounded-xl bg-gradient-to-br from-emerald-400/90 to-teal-600/90 text-white shadow-lg shadow-emerald-900/40 ring-1 ring-white/20 transition group-hover:brightness-110"
          >
            <Send class="size-5" aria-hidden="true" />
          </span>
          <span class="text-lg font-semibold tracking-tight">MINMs</span>
        </RouterLink>

        <template v-if="isAuthenticated">
          <nav
            class="order-3 flex w-full min-w-0 flex-1 flex-wrap items-center justify-end gap-2 sm:order-none"
            aria-label="Основная навигация"
          >
            <div class="relative w-[10.5rem] shrink-0 sm:w-44">
              <Search
                class="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-zinc-500"
                aria-hidden="true"
              />
              <input
                v-model="searchPeople"
                type="search"
                autocomplete="off"
                placeholder="Поиск людей…"
                aria-label="Поиск людей"
                class="w-full rounded-lg border border-white/10 bg-zinc-950/80 py-2 pl-9 pr-3 text-sm text-zinc-100 shadow-inner outline-none ring-emerald-500/0 transition placeholder:text-zinc-600 focus:border-emerald-500/50 focus:ring-2 focus:ring-emerald-500/25"
                @keydown.enter.prevent="goPeopleSearch"
              />
            </div>
            <div class="flex shrink-0 items-center gap-0">
              <div class="flex items-center gap-1">
                <RouterLink
                  to="/messages"
                  :class="iconNavClass('/messages')"
                  title="Сообщения"
                  aria-label="Сообщения"
                >
                  <MessagesSquare class="size-5" aria-hidden="true" />
                </RouterLink>
                <RouterLink
                  to="/contacts"
                  :class="iconNavClass('/contacts')"
                  title="Контакты"
                  aria-label="Контакты"
                >
                  <Users class="size-5" aria-hidden="true" />
                </RouterLink>
                <RouterLink
                  to="/profile"
                  :class="iconNavClass('/profile')"
                  :title="user?.login ? `@${user.login}` : (user?.username ?? 'Профиль')"
                  aria-label="Профиль"
                >
                  <UserCircle class="size-5" aria-hidden="true" />
                </RouterLink>
                <RouterLink
                  to="/settings/profile"
                  :class="[iconBtnClass, route.path.startsWith('/settings') ? iconBtnActiveClass : '']"
                  title="Настройки"
                  aria-label="Настройки"
                >
                  <Settings class="size-5" aria-hidden="true" />
                </RouterLink>
                <button
                  type="button"
                  :class="iconBtnClass"
                  title="Выйти"
                  aria-label="Выйти"
                  @click="logout"
                >
                  <LogOut class="size-5" aria-hidden="true" />
                </button>
              </div>
            </div>
          </nav>
        </template>

        <template v-else>
          <nav class="flex flex-wrap items-center gap-1 sm:gap-2" aria-label="Основная навигация">
            <RouterLink to="/" :class="navClass('/')">
              Главная
            </RouterLink>
            <RouterLink to="/users" :class="navClass('/users')">
              Люди
            </RouterLink>
            <RouterLink
              to="/login"
              :class="authEntryClass()"
              title="Вход. Регистрация — со страницы входа."
              aria-label="Вход"
            >
              <LogIn class="size-4 shrink-0 opacity-90" aria-hidden="true" />
              <span>Вход</span>
            </RouterLink>
          </nav>
        </template>
      </div>
    </header>

    <main
      class="flex-1"
      :class="isHome ? '' : 'bg-zinc-950'"
    >
      <RouterView />
    </main>
  </div>
</template>
