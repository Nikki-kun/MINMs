<script setup lang="ts">
import { apiFetch } from '@/api/client'
import { Check, Loader2, Pencil, Plus, Trash2, User, Users, X } from 'lucide-vue-next'
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'

type ContactPublic = {
  login: string
  username: string
  contactName: string
  contactAddedAt: string
}

type UserPublic = {
  login: string
  username: string
}

const contacts = ref<ContactPublic[]>([])
const loadingContacts = ref(false)
const contactsError = ref<string | null>(null)

const modalOpen = ref(false)
const submitLoading = ref(false)
const formError = ref<string | null>(null)
const editingLogin = ref<string | null>(null)
const editingName = ref('')
const rowActionLoading = ref<string | null>(null)
const rowActionError = ref<string | null>(null)

const contactName = ref('')
const loginQuery = ref('')
const suggestions = ref<UserPublic[]>([])
const loadingSuggestions = ref(false)

let suggestionsTimer: ReturnType<typeof setTimeout> | null = null

const normalizedLogin = computed(() => loginQuery.value.trim().replace(/^@+/, ''))
const bestLoginSuggestion = computed(() => {
  const query = normalizedLogin.value.toLowerCase()
  if (!query || suggestions.value.length === 0) return ''
  const best = suggestions.value.find((x) => x.login.toLowerCase().startsWith(query))
  return best?.login ?? ''
})
const loginGhostText = computed(() => {
  const query = normalizedLogin.value
  const best = bestLoginSuggestion.value
  if (!query || !best || best.toLowerCase() === query.toLowerCase()) return ''
  return best
})

async function loadContacts() {
  loadingContacts.value = true
  contactsError.value = null
  try {
    const res = await apiFetch('/api/contacts')
    if (!res.ok) throw new Error(`Request failed (${res.status})`)
    contacts.value = (await res.json()) as ContactPublic[]
  } catch (e) {
    contactsError.value = e instanceof Error ? e.message : 'Не удалось загрузить контакты'
  } finally {
    loadingContacts.value = false
  }
}

async function loadSuggestions(term: string) {
  const q = term.trim()
  if (q.length < 2) {
    suggestions.value = []
    loadingSuggestions.value = false
    return
  }

  loadingSuggestions.value = true
  try {
    const params = new URLSearchParams({ q, limit: '8' })
    const res = await apiFetch(`/api/users/search?${params}`)
    if (!res.ok) throw new Error(`Request failed (${res.status})`)
    suggestions.value = (await res.json()) as UserPublic[]
  } catch {
    suggestions.value = []
  } finally {
    loadingSuggestions.value = false
  }
}

function openModal() {
  modalOpen.value = true
  formError.value = null
  rowActionError.value = null
}

function closeModal() {
  modalOpen.value = false
  formError.value = null
  submitLoading.value = false
  loginQuery.value = ''
  contactName.value = ''
  suggestions.value = []
}

function applyLoginAutocomplete() {
  const best = bestLoginSuggestion.value
  if (!best) return
  loginQuery.value = best
  if (!contactName.value.trim()) {
    const picked = suggestions.value.find((x) => x.login === best)
    if (picked) contactName.value = picked.username
  }
}

function onGhostSuggestionClick() {
  applyLoginAutocomplete()
}

function handleLoginKeydown(event: KeyboardEvent) {
  const input = event.target as HTMLInputElement | null
  const canApply = !!loginGhostText.value
  if (!canApply) return

  if (event.key === 'Tab') {
    event.preventDefault()
    applyLoginAutocomplete()
    return
  }

  if (event.key === 'ArrowRight' && input && input.selectionStart === input.value.length) {
    event.preventDefault()
    applyLoginAutocomplete()
  }
}

async function submitAddContact() {
  const login = normalizedLogin.value.trim()
  const name = contactName.value.trim()
  if (login.length < 2 || name.length < 2) {
    formError.value = 'Укажите корректные имя и логин (минимум 2 символа).'
    return
  }

  submitLoading.value = true
  formError.value = null
  try {
    const res = await apiFetch('/api/contacts', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ login, contactName: name })
    })
    if (!res.ok) {
      const body = await res.json().catch(() => null)
      const message = typeof body?.message === 'string' ? body.message : `Request failed (${res.status})`
      throw new Error(message)
    }

    await loadContacts()
    closeModal()
  } catch (e) {
    formError.value = e instanceof Error ? e.message : 'Не удалось добавить контакт'
  } finally {
    submitLoading.value = false
  }
}

function startEditing(contact: ContactPublic) {
  editingLogin.value = contact.login
  editingName.value = contact.contactName
  rowActionError.value = null
}

function cancelEditing() {
  editingLogin.value = null
  editingName.value = ''
}

async function saveContactName(login: string) {
  const name = editingName.value.trim()
  if (name.length < 2) {
    rowActionError.value = 'Имя контакта должно быть минимум 2 символа.'
    return
  }

  rowActionLoading.value = login
  rowActionError.value = null
  try {
    const res = await apiFetch(`/api/contacts/${encodeURIComponent(login)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ contactName: name })
    })
    if (!res.ok) {
      const body = await res.json().catch(() => null)
      const message = typeof body?.message === 'string' ? body.message : `Request failed (${res.status})`
      throw new Error(message)
    }
    const row = contacts.value.find((x) => x.login === login)
    if (row) row.contactName = name
    cancelEditing()
  } catch (e) {
    rowActionError.value = e instanceof Error ? e.message : 'Не удалось сохранить контакт'
  } finally {
    rowActionLoading.value = null
  }
}

async function deleteContact(login: string) {
  rowActionLoading.value = login
  rowActionError.value = null
  try {
    const res = await apiFetch(`/api/contacts/${encodeURIComponent(login)}`, { method: 'DELETE' })
    if (!res.ok) {
      const body = await res.json().catch(() => null)
      const message = typeof body?.message === 'string' ? body.message : `Request failed (${res.status})`
      throw new Error(message)
    }
    contacts.value = contacts.value.filter((x) => x.login !== login)
    if (editingLogin.value === login) cancelEditing()
  } catch (e) {
    rowActionError.value = e instanceof Error ? e.message : 'Не удалось удалить контакт'
  } finally {
    rowActionLoading.value = null
  }
}

watch(loginQuery, (value) => {
  if (suggestionsTimer) clearTimeout(suggestionsTimer)
  suggestionsTimer = setTimeout(() => {
    suggestionsTimer = null
    void loadSuggestions(value)
  }, 250)
})

onMounted(() => {
  void loadContacts()
})

onUnmounted(() => {
  if (suggestionsTimer) clearTimeout(suggestionsTimer)
})
</script>

<template>
  <div class="mx-auto max-w-4xl px-5 py-10 sm:py-14">
    <div
      class="overflow-hidden rounded-3xl border border-white/10 bg-gradient-to-b from-zinc-900/90 to-zinc-950/95 p-6 shadow-2xl shadow-black/40 backdrop-blur-xl sm:p-8"
    >
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div class="flex size-14 items-center justify-center rounded-2xl bg-violet-500/15 text-violet-300 ring-1 ring-white/10">
            <Users class="size-7" aria-hidden="true" />
          </div>
          <h1 class="mt-6 text-2xl font-bold tracking-tight text-white">
            Контакты
          </h1>
          <p class="mt-2 text-sm text-zinc-400">
            Добавляйте, переименовывайте и удаляйте контакты в один клик.
          </p>
        </div>
        <div class="inline-flex items-center gap-2 rounded-full border border-white/10 bg-white/[0.03] px-3 py-1.5 text-xs text-zinc-300">
          <Users class="size-3.5 text-violet-300" aria-hidden="true" />
          <span>Всего: {{ contacts.length }}</span>
        </div>
      </div>

      <p
        v-if="contactsError"
        class="mt-5 rounded-xl border border-red-500/30 bg-red-950/50 px-4 py-3 text-sm text-red-200"
      >
        {{ contactsError }}
      </p>

      <div
        v-else-if="loadingContacts"
        class="mt-6 flex items-center gap-2 text-sm text-zinc-400"
        aria-live="polite"
      >
        <Loader2 class="size-4 animate-spin text-violet-300" aria-hidden="true" />
        Загружаем контакты…
      </div>

      <ul v-else-if="contacts.length > 0" class="mt-7 space-y-3" role="list">
        <li
          v-for="c in contacts"
          :key="c.login"
          class="group flex items-center gap-3 rounded-2xl border border-white/10 bg-zinc-950/60 px-4 py-3.5 shadow-lg shadow-black/15 transition hover:border-violet-400/30 hover:bg-zinc-900/70"
        >
          <div
            class="flex size-11 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-zinc-700 to-zinc-800 text-zinc-300 ring-1 ring-white/10"
          >
            <User class="size-5" aria-hidden="true" />
          </div>
          <div class="min-w-0 flex-1">
            <div class="flex flex-wrap items-center gap-2">
              <template v-if="editingLogin === c.login">
                <input
                  v-model="editingName"
                  type="text"
                  maxlength="20"
                  class="h-9 w-44 rounded-lg border border-white/15 bg-zinc-900 px-2.5 text-sm text-zinc-100 outline-none focus:border-violet-400/60"
                />
              </template>
              <span v-else class="inline-flex h-9 w-44 items-center truncate font-medium text-zinc-100">{{ c.contactName }}</span>
            </div>
            <p class="mt-0.5 flex flex-wrap items-center gap-x-2 text-xs text-zinc-500">
              <span>Пользователь: {{ c.username }}</span>
              <span class="font-mono text-emerald-400/90">@{{ c.login }}</span>
            </p>
          </div>
          <div class="flex items-center gap-1.5 rounded-xl border border-white/10 bg-zinc-900/70 p-1">
            <button
              v-if="editingLogin === c.login"
              type="button"
              class="inline-flex size-9 items-center justify-center rounded-lg text-emerald-300 transition hover:bg-emerald-500/15"
              :disabled="rowActionLoading === c.login"
              @click="saveContactName(c.login)"
            >
              <Check class="size-4" aria-hidden="true" />
            </button>
            <button
              v-else
              type="button"
              class="inline-flex size-9 items-center justify-center rounded-lg text-zinc-300 transition hover:bg-white/10 group-hover:text-zinc-100"
              :disabled="rowActionLoading === c.login"
              @click="startEditing(c)"
            >
              <Pencil class="size-4" aria-hidden="true" />
            </button>
            <button
              type="button"
              class="inline-flex size-9 items-center justify-center rounded-lg text-rose-300 transition hover:bg-rose-500/15"
              :disabled="rowActionLoading === c.login"
              @click="deleteContact(c.login)"
            >
              <Loader2
                v-if="rowActionLoading === c.login"
                class="size-4 animate-spin"
                aria-hidden="true"
              />
              <Trash2 v-else class="size-4" aria-hidden="true" />
            </button>
          </div>
        </li>
      </ul>

      <p
        v-if="rowActionError"
        class="mt-4 rounded-xl border border-red-500/30 bg-red-950/50 px-3 py-2 text-sm text-red-200"
      >
        {{ rowActionError }}
      </p>

      <p v-if="!contactsError && !loadingContacts && contacts.length === 0" class="mt-6 text-sm text-zinc-500">
        Пока нет контактов. Добавьте первый через кнопку +.
      </p>
    </div>
  </div>

  <button
    type="button"
    class="fixed bottom-8 right-8 inline-flex size-14 items-center justify-center rounded-full bg-violet-500 text-white shadow-xl shadow-violet-900/60 transition hover:bg-violet-400 focus:outline-none focus:ring-2 focus:ring-violet-400/70"
    @click="openModal"
    aria-label="Добавить контакт"
  >
    <Plus class="size-7" aria-hidden="true" />
  </button>

  <div
    v-if="modalOpen"
    class="fixed inset-0 z-40 flex items-center justify-center bg-zinc-950/70 p-4 backdrop-blur-md"
    @click.self="closeModal"
  >
    <div class="w-full max-w-md overflow-hidden rounded-3xl border border-white/15 bg-gradient-to-b from-zinc-900 to-zinc-950 p-5 shadow-2xl shadow-black/60">
      <div class="flex items-center justify-between">
        <h2 class="text-lg font-semibold text-white">Добавить контакт</h2>
        <button
          type="button"
          class="rounded-lg p-1.5 text-zinc-400 transition hover:bg-white/10 hover:text-zinc-200"
          @click="closeModal"
          aria-label="Закрыть"
        >
          <X class="size-5" aria-hidden="true" />
        </button>
      </div>

      <form class="mt-4 space-y-4" @submit.prevent="submitAddContact">
        <label class="block">
          <span class="mb-1 block text-sm text-zinc-300">Имя в контактах</span>
          <input
            v-model="contactName"
            type="text"
            autocomplete="off"
            maxlength="20"
            placeholder="Например, Анна"
            class="w-full rounded-xl border border-white/10 bg-zinc-950/80 px-3 py-2.5 text-zinc-100 outline-none ring-violet-500/0 transition placeholder:text-zinc-600 focus:border-violet-400/50 focus:ring-2 focus:ring-violet-500/25"
          />
        </label>

        <label class="block">
          <span class="mb-1 block text-sm text-zinc-300">Логин</span>
          <div class="relative">
            <div
              v-if="loginGhostText"
              class="absolute inset-y-0 left-0 flex cursor-text items-center px-3 font-mono text-zinc-500"
              @mousedown.prevent="onGhostSuggestionClick"
            >
              <span class="opacity-0">{{ normalizedLogin }}</span>{{ loginGhostText.slice(normalizedLogin.length) }}
            </div>
            <input
              v-model="loginQuery"
              type="text"
              autocomplete="off"
              maxlength="20"
              placeholder="@login"
              class="relative z-10 w-full rounded-xl border border-white/10 bg-zinc-950/80 px-3 py-2.5 font-mono text-zinc-100 outline-none ring-violet-500/0 transition placeholder:text-zinc-600 focus:border-violet-400/50 focus:ring-2 focus:ring-violet-500/25"
              @keydown="handleLoginKeydown"
            />
          </div>
        </label>

        <div class="relative h-5 text-xs text-zinc-500">
          <p class="absolute inset-0 flex items-center">
            <span
              class="inline-flex items-center gap-2 transition-opacity duration-150"
              :class="loginGhostText ? 'opacity-100' : 'opacity-0'"
            >
              <Loader2 v-if="loadingSuggestions" class="size-3.5 animate-spin text-zinc-400" aria-hidden="true" />
              Нажмите `Tab` или стрелку вправо, чтобы принять подсказку.
            </span>
          </p>
        </div>

        <p
          v-if="formError"
          class="rounded-xl border border-red-500/30 bg-red-950/50 px-3 py-2 text-sm text-red-200"
        >
          {{ formError }}
        </p>

        <button
          type="submit"
          class="inline-flex w-full items-center justify-center gap-2 rounded-xl bg-violet-500 px-4 py-2.5 text-sm font-medium text-white transition hover:bg-violet-400 disabled:cursor-not-allowed disabled:opacity-70"
          :disabled="submitLoading"
        >
          <Loader2 v-if="submitLoading" class="size-4 animate-spin" aria-hidden="true" />
          <span>{{ submitLoading ? 'Добавляем…' : 'Добавить контакт' }}</span>
        </button>
      </form>
    </div>
  </div>
</template>
