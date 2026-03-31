<script setup lang="ts">
import { apiFetch } from '@/api/client'
import { useAuth } from '@/composables/useAuth'
import { Calendar, Plus, Sparkles, User } from 'lucide-vue-next'
import { computed, onMounted } from 'vue'

const { user, token, persistSession } = useAuth()

onMounted(async () => {
  const t = token.value
  if (!t) return
  const res = await apiFetch('/api/auth/me')
  if (!res.ok) return
  const data = (await res.json()) as {
    userId: number
    username: string
    userCreatedAt: string
  }
  persistSession(t, {
    userId: data.userId,
    username: data.username,
    userCreatedAt: data.userCreatedAt,
  })
})

const createdLabel = computed(() => {
  const raw = user.value?.userCreatedAt
  if (!raw) return '—'
  const d = new Date(raw)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleString('ru-RU', {
    day: 'numeric',
    month: 'long',
    year: 'numeric'
  })
})
</script>

<template>
  <div class="bg-zinc-950">
    <div class="mx-auto max-w-5xl px-5 py-6 sm:py-8">
      <div class="flex flex-col gap-8 lg:flex-row lg:items-start lg:gap-8">
        <div class="w-full shrink-0 lg:max-w-[260px] xl:max-w-[280px]">
          <aside class="relative">
            <div
              class="pointer-events-none absolute -left-20 top-0 size-72 rounded-full bg-emerald-500/[0.07] blur-3xl"
              aria-hidden="true"
            />
            <div class="relative flex flex-col">
              <header class="mb-5 sm:mb-6">
                <p
                  class="mb-2 inline-flex items-center gap-1.5 rounded-full border border-emerald-500/25 bg-emerald-500/[0.12] px-3 py-1 text-[11px] font-semibold uppercase tracking-widest text-emerald-100/95"
                >
                  <Sparkles class="size-3.5 text-emerald-400" aria-hidden="true" />
                  Профиль
                </p>
              </header>

              <div
                class="relative flex flex-col overflow-hidden rounded-[1.35rem] border border-white/[0.09] bg-gradient-to-b from-zinc-900/80 via-zinc-950/90 to-zinc-950 p-5 shadow-[0_32px_64px_-20px_rgba(0,0,0,0.65)] sm:p-6"
              >
                <div
                  class="pointer-events-none absolute inset-x-5 top-[6.25rem] h-px bg-gradient-to-r from-transparent via-emerald-500/15 to-transparent sm:inset-x-6 sm:top-[6.5rem]"
                  aria-hidden="true"
                />

                <div class="relative flex flex-col">
                  <div class="flex flex-col items-center sm:items-start">
                    <div
                      class="mb-5 flex size-[4.75rem] items-center justify-center rounded-[1rem] bg-gradient-to-br from-zinc-500/90 via-zinc-800 to-zinc-950 text-zinc-100 shadow-[inset_0_1px_0_rgba(255,255,255,0.12)] ring-1 ring-white/15 sm:size-[5rem]"
                    >
                      <User class="size-9 opacity-[0.95] sm:size-[2.15rem]" aria-hidden="true" />
                    </div>

                    <div class="w-full space-y-4">
                      <div>
                        <p class="mt-1.5 break-all text-xl font-semibold tracking-tight text-white sm:text-2xl">
                          {{ user?.username ?? '—' }}
                        </p>
                      </div>

                      <div
                        class="grid gap-2.5 sm:grid-cols-1"
                      >
                        <div
                          class="rounded-xl bg-black/30 px-4 py-3 ring-1 ring-white/[0.06] sm:px-5 sm:py-3.5"
                        >
                          <p class="text-[11px] font-medium uppercase tracking-wide text-zinc-500">
                            Идентификатор
                          </p>
                          <p class="mt-1.5 font-mono text-sm text-zinc-200">
                            {{ user?.userId ?? '—' }}
                          </p>
                        </div>
                        <div
                          class="rounded-xl bg-black/30 px-4 py-3 ring-1 ring-white/[0.06] sm:px-5 sm:py-3.5"
                        >
                          <p class="flex items-center gap-2 text-[11px] font-medium uppercase tracking-wide text-zinc-500">
                            <Calendar class="size-3.5 text-zinc-600" aria-hidden="true" />
                            Профиль создан
                          </p>
                          <p class="mt-1.5 text-sm leading-snug text-zinc-200">
                            {{ createdLabel }}
                          </p>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </aside>
        </div>

        <section
          class="min-w-0 flex-1"
          aria-label="Стена"
        >
          <div class="mb-4 flex items-center justify-between gap-3">
            <h2 class="text-lg font-semibold tracking-tight text-white sm:text-xl">
              Стена
            </h2>
            <button
              type="button"
              class="flex size-9 shrink-0 items-center justify-center rounded-xl border border-white/15 bg-white/[0.06] text-zinc-200 transition hover:border-emerald-500/40 hover:bg-emerald-500/10 hover:text-emerald-200"
              aria-label="Добавить на стену"
            >
              <Plus class="size-5" aria-hidden="true" />
            </button>
          </div>
          <div
            class="flex min-h-[min(50vh,420px)] flex-col items-center justify-center rounded-[1.25rem] bg-transparent px-5 py-12 text-center sm:px-6"
          >
            <p class="max-w-sm text-sm leading-relaxed text-zinc-500">
              На стене пока ничего нет
            </p>
          </div>
        </section>
      </div>
    </div>
  </div>
</template>
