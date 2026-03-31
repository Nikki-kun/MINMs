<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { MessageCircle } from 'lucide-vue-next'

const route = useRoute()
const isHome = computed(() => route.path === '/')

const navLinkClass =
  'rounded-lg px-3 py-2 text-sm font-medium text-zinc-300 ring-1 ring-transparent transition-colors hover:bg-white/10 hover:text-white'
const navActiveClass = 'bg-white/15 text-white shadow-sm ring-white/20'

function navClass(path: string) {
  return [navLinkClass, route.path === path ? navActiveClass : '']
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
            <MessageCircle class="size-5" aria-hidden="true" />
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
          <RouterLink to="/about" :class="navClass('/about')">
            О проекте
          </RouterLink>
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
