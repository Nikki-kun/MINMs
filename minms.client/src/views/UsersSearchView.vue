<script setup lang="ts">
import { apiFetch } from '@/api/client'
import { useAuth } from '@/composables/useAuth'
import { Loader2, Search, User } from 'lucide-vue-next'
import { onUnmounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const { clearSession } = useAuth()

export type UserPublic = {
  userId: number
  username: string
  userCreatedAt: string
}

const query = ref('')
const results = ref<UserPublic[]>([])
const loading = ref(false)
const showLoadingIndicator = ref(false)
const error = ref<string | null>(null)

let debounceTimer: ReturnType<typeof setTimeout> | null = null
let loadingDelayTimer: ReturnType<typeof setTimeout> | null = null

function clearLoadingDelay() {
  if (loadingDelayTimer) {
    clearTimeout(loadingDelayTimer)
    loadingDelayTimer = null
  }
  showLoadingIndicator.value = false
}

function scheduleLoadingIndicator() {
  clearLoadingDelay()
  loadingDelayTimer = setTimeout(() => {
    loadingDelayTimer = null
    showLoadingIndicator.value = true
  }, 100)
}

async function runSearch(q: string) {
  const term = q.trim()
  if (!term) {
    results.value = []
    error.value = null
    loading.value = false
    clearLoadingDelay()
    return
  }

  loading.value = true
  scheduleLoadingIndicator()
  error.value = null
  try {
    const params = new URLSearchParams({ q: term, limit: '30' })
    const res = await apiFetch(`/api/users/search?${params}`)
    if (!res.ok) {
      throw new Error(`Request failed (${res.status})`)
    }
    results.value = (await res.json()) as UserPublic[]
  } catch (e) {
    results.value = []
    error.value = e instanceof Error ? e.message : 'Something went wrong'
  } finally {
    loading.value = false
    clearLoadingDelay()
  }
}

watch(
  query,
  (value) => {
    if (debounceTimer) clearTimeout(debounceTimer)
    debounceTimer = setTimeout(() => {
      debounceTimer = null
      void runSearch(value)
    }, 300)
  },
  { immediate: true }
)

onUnmounted(() => {
  if (debounceTimer) clearTimeout(debounceTimer)
  clearLoadingDelay()
})

function formatWhen(iso: string) {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleString()
}
</script>

<template>
  <div class="mx-auto max-w-3xl px-5 py-12 sm:py-16">
    <div
      class="overflow-hidden rounded-3xl border border-white/10 bg-gradient-to-b from-zinc-900/90 to-zinc-950/95 p-6 shadow-2xl shadow-black/40 backdrop-blur-xl sm:p-8"
    >
      <section class="space-y-6">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white">
            Поиск пользователей
          </h1>
          <p class="mt-2 text-sm leading-relaxed text-zinc-400">
            Введите часть имени пользователя — покажем до 30 совпадений.
          </p>
        </div>

        <div class="relative">
          <Search
            class="pointer-events-none absolute left-3.5 top-1/2 size-5 -translate-y-1/2 text-zinc-500"
            aria-hidden="true"
          />
          <input
            v-model="query"
            type="search"
            autocomplete="off"
            placeholder="Имя пользователя…"
            class="w-full rounded-xl border border-white/10 bg-zinc-950/80 py-3 pl-11 pr-4 text-zinc-100 shadow-inner outline-none ring-emerald-500/0 transition placeholder:text-zinc-600 focus:border-emerald-500/50 focus:ring-2 focus:ring-emerald-500/25"
          />
        </div>

        <p
          v-if="error"
          class="rounded-xl border border-red-500/30 bg-red-950/50 px-4 py-3 text-sm text-red-200"
        >
          {{ error }}
        </p>

        <div
          v-if="showLoadingIndicator"
          class="flex items-center gap-2 text-sm text-zinc-400"
          aria-live="polite"
        >
          <Loader2 class="size-4 animate-spin text-emerald-400" aria-hidden="true" />
          Поиск…
        </div>

        <ul
          v-else-if="results.length > 0"
          class="divide-y divide-white/10 overflow-hidden rounded-2xl border border-white/10 bg-zinc-950/60"
          role="list"
        >
          <li
            v-for="u in results"
            :key="u.userId"
            class="flex items-center gap-3 px-4 py-3.5 transition hover:bg-white/[0.04]"
          >
            <div
              class="flex size-11 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-zinc-700 to-zinc-800 text-zinc-300 ring-1 ring-white/10"
            >
              <User class="size-5" aria-hidden="true" />
            </div>
            <div class="min-w-0 flex-1">
              <div class="flex flex-wrap items-center gap-2">
                <span class="font-medium text-zinc-100">{{ u.username }}</span>
              </div>
              <p class="mt-0.5 text-xs text-zinc-600">
                Зарегистрирован(а) {{ formatWhen(u.userCreatedAt) }}
              </p>
            </div>
          </li>
        </ul>

        <p
          v-else-if="query.trim().length > 0 && !loading"
          class="text-sm text-zinc-500"
        >
          Ничего не найдено.
        </p>
      </section>
    </div>
  </div>
</template>
