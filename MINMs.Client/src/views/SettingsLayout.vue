<script setup lang="ts">
import { Shield, UserCircle, Eye } from 'lucide-vue-next'
import { computed } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()

const itemClass =
  'flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-sm font-medium text-zinc-300 transition-colors hover:bg-white/10 hover:text-white'
const itemActiveClass = 'bg-white/12 text-white ring-1 ring-white/15'

function linkClass(pathSuffix: string) {
  const full = `/settings/${pathSuffix}`
  return [itemClass, route.path === full ? itemActiveClass : '']
}

const title = computed(() => {
  if (route.path.endsWith('/security')) return 'Безопасность'
  if (route.path.endsWith('/privacy')) return 'Приватность'
  return 'Профиль'
})
</script>

<template>
  <div class="mx-auto flex max-w-5xl flex-col gap-8 px-5 py-10 sm:flex-row sm:py-12">
    <aside
      class="w-full shrink-0 sm:w-56"
      aria-label="Разделы настроек"
    >
      <p class="mb-3 hidden text-xs font-medium uppercase tracking-wider text-zinc-500 sm:block">
        Настройки
      </p>
      <h1 class="mb-4 text-lg font-semibold tracking-tight text-white sm:hidden">
        Настройки
      </h1>
      <nav class="flex flex-col gap-1">
        <RouterLink
          to="/settings/profile"
          :class="linkClass('profile')"
        >
          <UserCircle class="size-5 shrink-0 opacity-90" aria-hidden="true" />
          Профиль
        </RouterLink>
        <RouterLink
          to="/settings/security"
          :class="linkClass('security')"
        >
          <Shield class="size-5 shrink-0 opacity-90" aria-hidden="true" />
          Безопасность
        </RouterLink>
        <RouterLink
          to="/settings/privacy"
          :class="linkClass('privacy')"
        >
          <Eye class="size-5 shrink-0 opacity-90" aria-hidden="true" />
          Приватность
        </RouterLink>
      </nav>
    </aside>

    <div class="min-w-0 flex-1">
      <div
        class="overflow-hidden rounded-3xl border border-white/10 bg-gradient-to-b from-zinc-900/90 to-zinc-950/95 shadow-2xl shadow-black/40 backdrop-blur-xl"
      >
        <div class="border-b border-white/10 px-6 py-4 sm:px-8">
          <h2 class="text-lg font-semibold text-white">
            {{ title }}
          </h2>
        </div>
        <div class="px-6 py-6 sm:px-8 sm:py-8">
          <RouterView />
        </div>
      </div>
    </div>
  </div>
</template>
