<script setup lang="ts">
import { Loader2, UserPlus } from 'lucide-vue-next'
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuth } from '@/composables/useAuth'

type AuthResponse = {
  accessToken: string
  expiresInSeconds: number
  tokenType: string
  login: string
  username: string
  userCreatedAt: string
}

const router = useRouter()
const { persistSession } = useAuth()

const username = ref('')
const password = ref('')
const loading = ref(false)
const error = ref<string | null>(null)

async function submit() {
  loading.value = true
  error.value = null
  try {
    const res = await fetch('/api/auth/register', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        username: username.value.trim(),
        password: password.value,
      }),
    })
    const data = (await res.json().catch(() => ({}))) as AuthResponse & { message?: string }
    if (res.status === 409) {
      throw new Error(
        typeof data.message === 'string' ? data.message : 'Этот логин уже занят.'
      )
    }
    if (res.status === 400) {
      throw new Error(
        typeof data.message === 'string'
          ? data.message
          : 'Проверьте корректность данных.'
      )
    }
    if (!res.ok) {
      throw new Error(`Регистрация не удалась (${res.status})`)
    }
    persistSession(data.accessToken, {
      login: data.login,
      username: data.username,
      userCreatedAt: data.userCreatedAt,
    })
    await router.replace('/profile')
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Не удалось зарегистрироваться'
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
          class="flex size-11 items-center justify-center rounded-xl bg-teal-500/15 text-teal-300 ring-1 ring-teal-500/25"
        >
          <UserPlus class="size-5" aria-hidden="true" />
        </span>
        <div>
          <h1 class="text-xl font-bold tracking-tight text-white">
            Регистрация
          </h1>
          <p class="text-sm text-zinc-400">
            Минимум 8 символов в пароле.
          </p>
        </div>
      </div>

      <form class="space-y-4" @submit.prevent="submit">
        <div>
          <label for="reg-username" class="mb-1.5 block text-xs font-medium text-zinc-400">
            Имя
          </label>
          <input
            id="reg-username"
            v-model="username"
            type="text"
            autocomplete="nickname"
            required
            minlength="1"
            maxlength="100"
            class="w-full rounded-xl border border-white/10 bg-zinc-950/80 px-4 py-2.5 text-zinc-100 outline-none ring-emerald-500/0 transition focus:border-emerald-500/50 focus:ring-2 focus:ring-emerald-500/25"
          />
        </div>
        <div>
          <label for="reg-password" class="mb-1.5 block text-xs font-medium text-zinc-400">
            Пароль
          </label>
          <input
            id="reg-password"
            v-model="password"
            type="password"
            autocomplete="new-password"
            required
            minlength="8"
            maxlength="256"
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
          class="flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-teal-500 to-emerald-600 py-3 text-sm font-semibold text-white shadow-lg shadow-teal-900/30 transition hover:brightness-110 disabled:opacity-60"
        >
          <Loader2 v-if="loading" class="size-4 animate-spin" aria-hidden="true" />
          Создать аккаунт
        </button>

        <p class="text-center text-sm text-zinc-500">
          Уже есть аккаунт?
          <RouterLink to="/login" class="font-medium text-emerald-400 hover:text-emerald-300">
            Войти
          </RouterLink>
        </p>
      </form>
    </div>
  </div>
</template>
