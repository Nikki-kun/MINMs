<script setup lang="ts">
import { useAuth } from '@/composables/useAuth'
import { LogIn, Send } from 'lucide-vue-next'
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'

const route = useRoute()
const router = useRouter()
const { isAuthenticated, user, clearSession } = useAuth()
const isHome = computed(() => route.path === '/')

function logout() {
  clearSession()
  void router.push('/login')
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
</script>

<template>
  <div class="flex min-h-dvh flex-col">
    <header
      class="sticky top-0 z-50 border-b border-white/10 bg-zinc-950/55 backdrop-blur-xl backdrop-saturate-150"
    >
      <div class="mx-auto flex max-w-5xl items-center justify-between gap-4 px-5 py-4">
        <RouterLink
          to="/"
          class="group flex items-center gap-2.5 text-zinc-100 no-underline"
        >
          <span
            class="flex size-10 items-center justify-center rounded-xl bg-gradient-to-br from-emerald-400/90 to-teal-600/90 text-white shadow-lg shadow-emerald-900/40 ring-1 ring-white/20 transition group-hover:brightness-110"
          >
            <Send class="size-5" aria-hidden="true" />
          </span>
          <span class="text-lg font-semibold tracking-tight">MINMs</span>
        </RouterLink>
        <nav class="flex flex-wrap items-center gap-1 sm:gap-2" aria-label="Основная навигация">
          <RouterLink to="/" :class="navClass('/')">
            Главная
          </RouterLink>
          <RouterLink to="/users" :class="navClass('/users')">
            Люди
          </RouterLink>
          <template v-if="isAuthenticated">
            <span class="hidden text-zinc-600 sm:inline" aria-hidden="true">|</span>
            <RouterLink
              to="/profile"
              :class="navClass('/profile')"
              class="max-w-[10rem] truncate"
              :title="user?.username ?? 'Профиль'"
            >
              {{ user?.username }}
            </RouterLink>
            <button
              type="button"
              class="rounded-lg px-3 py-2 text-sm font-medium text-zinc-300 ring-1 ring-transparent transition-colors hover:bg-white/10 hover:text-white"
              @click="logout"
            >
              Выйти
            </button>
          </template>
          <template v-else>
            <RouterLink
              to="/login"
              :class="authEntryClass()"
              title="Вход. Регистрация — со страницы входа."
              aria-label="Вход"
            >
              <LogIn class="size-4 shrink-0 opacity-90" aria-hidden="true" />
              <span>Вход</span>
            </RouterLink>
          </template>
        </nav>
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
