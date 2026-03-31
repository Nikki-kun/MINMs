<script setup lang="ts">
import { Loader2, LogIn } from 'lucide-vue-next'
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuth } from '@/composables/useAuth'

type AuthResponse = {
  accessToken: string
  expiresInSeconds: number
  tokenType: string
  userId: number
  username: string
  userCreatedAt: string
}

const router = useRouter()
const route = useRoute()
const { persistSession } = useAuth()

const username = ref('')
const password = ref('')
const loading = ref(false)
const error = ref<string | null>(null)

async function submit() {
  loading.value = true
  error.value = null
  try {
    const res = await fetch('/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        username: username.value.trim(),
        password: password.value,
      }),
    })
    const data = (await res.json().catch(() => ({}))) as AuthResponse & { message?: string }
    if (!res.ok) {
      throw new Error(
        typeof data.message === 'string' ? data.message : `Ошибка входа (${res.status})`
      )
    }
    persistSession(data.accessToken, {
      userId: data.userId,
      username: data.username,
      userCreatedAt: data.userCreatedAt,
    })
    const raw = route.query.redirect
    const target =
      typeof raw === 'string' && raw.startsWith('/') && !raw.startsWith('//') ? raw : '/profile'
    await router.replace(target)
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Не удалось войти'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="mx-auto max-w-md px-5 py-12 sm:py-16">
    <div
      class="overflow-hidden rounded-3xl border border-white/10 bg-gradient-to-b from-zinc-900/90 to-zinc-950/95 p-6 shadow-2xl shadow-black/40 backdrop-blur-xl sm:p-8"
    >
      <div class="mb-6 flex items-center gap-3">
        <span
          class="flex size-11 items-center justify-center rounded-xl bg-emerald-500/15 text-emerald-300 ring-1 ring-emerald-500/25"
        >
          <LogIn class="size-5" aria-hidden="true" />
        </span>
        <div>
          <h1 class="text-xl font-bold tracking-tight text-white">
            Вход
          </h1>
          <p class="text-sm text-zinc-400">
            Войдите, чтобы начать общение.
          </p>
        </div>
      </div>

      <form class="space-y-4" @submit.prevent="submit">
        <div>
          <label for="login-username" class="mb-1.5 block text-xs font-medium text-zinc-400">
            Имя пользователя
          </label>
          <input
            id="login-username"
            v-model="username"
            type="text"
            autocomplete="username"
            required
            minlength="3"
            maxlength="100"
            class="w-full rounded-xl border border-white/10 bg-zinc-950/80 px-4 py-2.5 text-zinc-100 outline-none ring-emerald-500/0 transition focus:border-emerald-500/50 focus:ring-2 focus:ring-emerald-500/25"
          />
        </div>
        <div>
          <label for="login-password" class="mb-1.5 block text-xs font-medium text-zinc-400">
            Пароль
          </label>
          <input
            id="login-password"
            v-model="password"
            type="password"
            autocomplete="current-password"
            required
            class="w-full rounded-xl border border-white/10 bg-zinc-950/80 px-4 py-2.5 text-zinc-100 outline-none ring-emerald-500/0 transition focus:border-emerald-500/50 focus:ring-2 focus:ring-emerald-500/25"
          />
        </div>

        <p
          v-if="error"
          class="rounded-xl border border-red-500/30 bg-red-950/50 px-4 py-3 text-sm text-red-200"
        >
          {{ error }}
        </p>

        <button
          type="submit"
          :disabled="loading"
          class="flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-emerald-500 to-teal-600 py-3 text-sm font-semibold text-white shadow-lg shadow-emerald-900/30 transition hover:brightness-110 disabled:opacity-60"
        >
          <Loader2 v-if="loading" class="size-4 animate-spin" aria-hidden="true" />
          Войти
        </button>

        <p class="text-center text-sm text-zinc-500">
          Нет аккаунта?
          <RouterLink to="/register" class="font-medium text-emerald-400 hover:text-emerald-300">
            Регистрация
          </RouterLink>
        </p>
      </form>
    </div>
  </div>
</template>
