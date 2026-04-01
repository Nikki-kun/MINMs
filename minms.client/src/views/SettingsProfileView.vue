<script setup lang="ts">
import { useAuth } from '@/composables/useAuth'

const { user } = useAuth()
</script>

<template>
  <div class="space-y-4 text-sm text-zinc-300">
    <p class="leading-relaxed text-zinc-400">
      Основные данные профиля. Редактирование появится в следующих версиях.
    </p>
    <dl class="grid gap-3 rounded-2xl border border-white/10 bg-zinc-950/50 p-4">
      <div class="flex flex-wrap items-baseline justify-between gap-2">
        <dt class="text-zinc-500">
          Имя
        </dt>
        <dd class="font-medium text-zinc-100">
          {{ user?.username ?? '—' }}
        </dd>
      </div>
      <div class="flex flex-wrap items-baseline justify-between gap-2">
        <dt class="text-zinc-500">
          Логин
        </dt>
        <dd class="font-mono text-emerald-400/90">
          @{{ user?.login ?? '—' }}
        </dd>
      </div>
    </dl>
  </div>
</template>
