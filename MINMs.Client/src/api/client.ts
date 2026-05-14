import { getAccessToken, useAuth } from '@/composables/useAuth'
import { ref, onMounted, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'
import { useToast } from 'vue-toastification'

export async function apiFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const headers = new Headers(init?.headers)
  const t = getAccessToken()

  if (t) {
    headers.set('Authorization', `Bearer ${t}`)
  }

  return fetch(input, { ...init, headers })
}

export function useSignalR(hubUrl: string) {
  const connection = ref<signalR.HubConnection | null>(null)

  const isConnected = ref(false)
  const isConnecting = ref(false)
  const reconnectAttempts = ref(0)

  let reconnectTimer: number | null = null
  let heartbeatInterval: number | null = null

  const { token } = useAuth()
  const toast = useToast()

  const connect = async () => {
    if (isConnected.value || isConnecting.value) {
      return
    }

    try {
      isConnecting.value = true

      connection.value = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl, {
          accessTokenFactory: () => token.value || '',
          transport: signalR.HttpTransportType.WebSockets,
        })
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            if (retryContext.previousRetryCount >= 5) {
              return null
            }

            return 5000
          }
        })
        .configureLogging(signalR.LogLevel.Information)
        .build()

      connection.value.on('connected', (message: string) => {
        console.log('✅', message)
      })

      connection.value.on('heartbeatResponse', () => {
        console.log('heartbeatResponse')
      })

      connection.value.onclose((error) => {
        console.error('❌ SignalR connection closed:', error)

        isConnected.value = false
        isConnecting.value = false

        stopHeartbeat()
        startReconnectTimer()
      })

      connection.value.onreconnecting((error) => {
        console.warn('🔄 SignalR reconnecting:', error)

        isConnected.value = false
      })

      connection.value.onreconnected((connectionId) => {
        console.log('✅ Reconnected:', connectionId)

        isConnected.value = true
        isConnecting.value = false
        reconnectAttempts.value = 0

        toast.success('Соединение восстановлено')

        startHeartbeat()
      })

      await connection.value.start()

      isConnected.value = true
      isConnecting.value = false
      reconnectAttempts.value = 0

      console.log('✅ SignalR connected successfully')

      startHeartbeat()

    } catch (error) {
      console.error('❌ SignalR connection error:', error)

      isConnected.value = false
      isConnecting.value = false

      startReconnectTimer()
    }
  }

  const startReconnectTimer = () => {
    if (reconnectTimer) {
      clearTimeout(reconnectTimer)
    }

    reconnectTimer = window.setTimeout(() => {
      if (!isConnected.value && !isConnecting.value) {
        reconnectAttempts.value++

        console.log(`🔄 Reconnect attempt ${reconnectAttempts.value}`)

        connect()
      }
    }, 5000)
  }

  const startHeartbeat = () => {
    stopHeartbeat()

    heartbeatInterval = window.setInterval(() => {
      sendHeartbeat().catch((err) => {
        console.error('❌ Failed to send heartbeat:', err)
      })
    }, 10000)
  }

  const stopHeartbeat = () => {
    if (heartbeatInterval) {
      clearInterval(heartbeatInterval)
      heartbeatInterval = null
    }
  }

  const sendHeartbeat = async () => {
    if (!connection.value || !isConnected.value) {
      return
    }

    try {
      await connection.value.invoke('heartbeat')
    } catch (error) {
      console.error('❌ Heartbeat send failed:', error)
    }
  }

  const sendMessage = async (message: string) => {
    if (!connection.value || !isConnected.value) {
      toast.error('Нет соединения с сервером')
      return false
    }

    try {
      await connection.value.invoke('sendMessageToCaller',message)

      console.log('📤 Message sent:', message)

      return true
    } catch (error) {

      console.error('❌ Failed to send message:', error)

      return false
    }
  }

  const onReceiveMessage = (callback: (message: string) => void) => {
    if (!connection.value) {
      console.log('error sub on receiv message')
      return
    }

    connection.value.on('receiveMessage', (message: string) => {
      callback(message)
    })
  }

  const disconnect = async () => {
    if (reconnectTimer) {
      clearTimeout(reconnectTimer)
      reconnectTimer = null
    }

    stopHeartbeat()

    if (connection.value) {
      try {
        await connection.value.stop()
      } catch (error) {
        console.error('❌ Error stopping connection:', error)
      }

      connection.value = null
    }

    isConnected.value = false
    isConnecting.value = false
  }

  onMounted(() => {
    connect()
  })

  onUnmounted(() => {
    disconnect()
  })

  return {
    connection,
    isConnected,
    isConnecting,
    reconnectAttempts,
    connect,
    disconnect,
    sendMessage,
    sendHeartbeat,
    onReceiveMessage
  }
}