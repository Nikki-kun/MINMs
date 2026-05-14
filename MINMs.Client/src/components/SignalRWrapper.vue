<script setup lang="ts">
import { useSignalR } from '@/api/client'
import { useToast } from 'vue-toastification'
import { watch, onUnmounted } from 'vue'

const toast = useToast()
const hubUrl = `${window.location.protocol}//${window.location.host}/notification`

const { 
  isConnected,
  isConnecting,
  onReceiveMessage,
  sendMessage
} = useSignalR(hubUrl)

watch(isConnected, (connected) => {
  if (connected) {
    console.log('✅ SignalR Connected')
  } else {
    console.log('❌ SignalR Disconnected')
  }
})

onReceiveMessage((message: string) => {
    toast.success(`📨 ${message}`, {
      timeout: 5000,
      closeOnClick: true
    })
})

defineExpose({
  sendMessage,
  isConnected
})
</script>

<template>
  <div v-if="isConnecting" class="fixed bottom-4 right-4 z-50">
    <div class="flex items-center gap-2 rounded-lg bg-zinc-900 px-3 py-2 text-sm text-zinc-300 shadow-lg">
      <div class="size-2 animate-pulse rounded-full bg-emerald-500"></div>
      <span>Подключение к серверу...</span>
    </div>
  </div>
  <div v-else-if="!isConnected && !isConnecting" class="fixed bottom-4 right-4 z-50">
    <div class="flex items-center gap-2 rounded-lg bg-red-900/90 px-3 py-2 text-sm text-red-100 shadow-lg">
      <div class="size-2 rounded-full bg-red-500"></div>
      <span>Нет соединения. Переподключение через 5с...</span>
    </div>
  </div>
</template>