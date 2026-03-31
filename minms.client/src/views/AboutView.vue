<script setup lang="ts">
import { Code2, Database, Layout } from 'lucide-vue-next'
</script>

<template>
  <div class="mx-auto max-w-3xl px-5 py-12 sm:py-16">
    <div
      class="overflow-hidden rounded-3xl border border-white/10 bg-gradient-to-b from-zinc-900/90 to-zinc-950/95 p-8 shadow-2xl shadow-black/40 backdrop-blur-xl sm:p-10"
    >
      <h1 class="text-3xl font-bold tracking-tight text-white">
        О проекте MINMs
      </h1>
      <p class="mt-4 text-base leading-relaxed text-zinc-400">
        MINMs (условно «messenger») — учебный/продуктовый каркас мессенджера: сервер на
        <strong class="font-medium text-zinc-300">ASP.NET Core</strong>
        с API и раздачей SPA, клиент на
        <strong class="font-medium text-zinc-300">Vue 3</strong>
        с маршрутизацией и современной вёрсткой (Tailwind CSS). Данные лежат в
        <strong class="font-medium text-zinc-300">MySQL</strong>
        — пользователи, контакты, чаты, участники, сообщения и блокировки описаны миграциями и сущностями на стороне .NET.
      </p>

      <ul class="mt-8 space-y-4">
        <li class="flex gap-4 rounded-2xl border border-white/5 bg-zinc-900/50 p-4">
          <span class="flex size-10 shrink-0 items-center justify-center rounded-xl bg-sky-500/15 text-sky-300">
            <Code2 class="size-5" aria-hidden="true" />
          </span>
          <div>
            <h2 class="font-semibold text-zinc-100">
              Репозиторий
            </h2>
            <p class="mt-1 text-sm text-zinc-500">
              Решение объединяет проект <code class="rounded bg-zinc-800 px-1.5 py-0.5 text-zinc-300">MINMs.Server</code>
              и фронтенд <code class="rounded bg-zinc-800 px-1.5 py-0.5 text-zinc-300">minms.client</code> (Vite, TypeScript).
            </p>
          </div>
        </li>
        <li class="flex gap-4 rounded-2xl border border-white/5 bg-zinc-900/50 p-4">
          <span class="flex size-10 shrink-0 items-center justify-center rounded-xl bg-emerald-500/15 text-emerald-300">
            <Database class="size-5" aria-hidden="true" />
          </span>
          <div>
            <h2 class="font-semibold text-zinc-100">
              Данные
            </h2>
            <p class="mt-1 text-sm text-zinc-500">
              Подключение через фабрику соединений и миграции SQL; поиск пользователей доступен через REST
              <code class="rounded bg-zinc-800 px-1.5 py-0.5 text-zinc-300">/api/users/search</code>.
            </p>
          </div>
        </li>
        <li class="flex gap-4 rounded-2xl border border-white/5 bg-zinc-900/50 p-4">
          <span class="flex size-10 shrink-0 items-center justify-center rounded-xl bg-violet-500/15 text-violet-300">
            <Layout class="size-5" aria-hidden="true" />
          </span>
          <div>
            <h2 class="font-semibold text-zinc-100">
              Интерфейс
            </h2>
            <p class="mt-1 text-sm text-zinc-500">
              Главная страница с живым градиентом, разделы «Люди» и «О проекте», иконки Lucide — база для дальнейших экранов чатов и профиля.
            </p>
          </div>
        </li>
      </ul>
    </div>
  </div>
</template>
