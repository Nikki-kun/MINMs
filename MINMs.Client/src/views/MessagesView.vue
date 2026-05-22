<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, nextTick, watch } from "vue";
import {
  MessagesSquare,
  Send,
  User,
  Check,
  CheckCheck,
  MoreVertical,
  Phone,
  Video,
  Search,
  ArrowLeft,
} from "lucide-vue-next";
import {
  useSignalR,
  contactsApi,
  type Contact,
  type ChatMessage,
  type ChatPreview,
} from "@/api/client.ts";
import { useAuth } from "@/composables/useAuth";
import { useToast } from "vue-toastification";

const { user, isAuthenticated } = useAuth();
const toast = useToast();

const hubUrl = `${window.location.protocol}//${window.location.host}/messageHub`;
const signalR = useSignalR(hubUrl);

const contacts = ref<Contact[]>([]);
const chats = ref<ChatPreview[]>([]);
const selectedChat = ref<{
  chatId: number | null;
  contactLogin: string;
  contactName: string;
  contactUsername: string;
} | null>(null);

const messages = ref<ChatMessage[]>([]);
const newMessage = ref("");
const messagesContainer = ref<HTMLElement | null>(null);
const isLoadingMessages = ref(false);
const isSending = ref(false);
const searchQuery = ref("");
const showMobileChat = ref(false);
const pendingMessages = ref<Map<number, number>>(new Map()); // Map временный ID -> реальный ID

// Map для быстрого поиска chatId по логину контакта
const contactChatMap = ref<Map<string, number>>(new Map());

const filteredContacts = computed(() => {
  if (!searchQuery.value) return contacts.value;
  const query = searchQuery.value.toLowerCase();
  return contacts.value.filter(
    (c) =>
      c.login.toLowerCase().includes(query) ||
      c.username.toLowerCase().includes(query) ||
      c.contactName.toLowerCase().includes(query),
  );
});

async function loadContacts() {
  if (!isAuthenticated.value) return;

  try {
    contacts.value = await contactsApi.getContacts();
    console.log("📞 Contacts loaded:", contacts.value.length);
  } catch (error) {
    console.error("Failed to load contacts:", error);
    toast.error("Не удалось загрузить список контактов");
  }
}

function onUserChats(chatsList: ChatPreview[]) {
  console.log("📋 User chats received:", chatsList);
  chats.value = chatsList;

  // Обновляем мапу соответствия логин -> chatId для личных чатов
  contactChatMap.value.clear();
  for (const chat of chatsList) {
    if (chat.type === 1) {
      // Personal chat
      signalR.getChatInfo(chat.chatId);
    }
  }
}

function onChatInfo(chat: any) {
  console.log("ℹ️ Chat info received:", chat);

  if (chat.type === 1 && chat.participants) {
    // Находим другого участника (не текущего пользователя)
    const otherParticipant = chat.participants.find((p: any) => p.login !== user.value?.login);
    if (otherParticipant) {
      contactChatMap.value.set(otherParticipant.login, chat.chatId);
      console.log(`✅ Mapped ${otherParticipant.login} -> chat ${chat.chatId}`);

      // Если этот чат выбран, обновляем selectedChat
      if (
        selectedChat.value &&
        selectedChat.value.contactLogin === otherParticipant.login &&
        selectedChat.value.chatId === null
      ) {
        selectedChat.value.chatId = chat.chatId;
        loadChatMessages(chat.chatId);
      }
    }
  }
}

async function selectContact(contact: Contact) {
  isLoadingMessages.value = true;

  // Проверяем, есть ли уже существующий чат с этим контактом
  let existingChatId = contactChatMap.value.get(contact.login);

  if (existingChatId) {
    // Чат существует - присоединяемся
    console.log(`📱 Existing chat found for ${contact.login}: ${existingChatId}`);
    selectedChat.value = {
      chatId: existingChatId,
      contactLogin: contact.login,
      contactName: contact.contactName,
      contactUsername: contact.username,
    };
    await loadChatMessages(existingChatId);
  } else {
    // Чата еще нет - создаем временное состояние
    console.log(`🆕 No existing chat for ${contact.login}, will create on first message`);
    selectedChat.value = {
      chatId: null,
      contactLogin: contact.login,
      contactName: contact.contactName,
      contactUsername: contact.username,
    };
    messages.value = [];
    isLoadingMessages.value = false;
  }

  if (window.innerWidth < 768) {
    showMobileChat.value = true;
  }
}

async function loadChatMessages(chatId: number) {
  isLoadingMessages.value = true;
  messages.value = [];

  // Присоединяемся к группе чата для получения реального времени
  await signalR.joinChat(chatId);

  // Загружаем историю сообщений
  await signalR.getChatMessages(chatId, 0, 50);
}

function backToContacts() {
  showMobileChat.value = false;
}

async function sendMessage() {
  if (!newMessage.value.trim()) return;
  if (!selectedChat.value) return;
  if (!signalR.isConnected.value) {
    toast.error("Нет соединения с сервером");
    return;
  }

  isSending.value = true;
  const messageContent = newMessage.value.trim();
  newMessage.value = "";

  // Временное сообщение для оптимистичного обновления UI
  const tempId = Date.now(); // Используем положительный timestamp для уникальности
  const tempMessage: ChatMessage = {
    messageId: tempId,
    senderId: parseInt(user.value?.userId || "0"),
    senderLogin: user.value?.login || "",
    senderUsername: user.value?.username || "",
    chatId: selectedChat.value.chatId || tempId,
    content: messageContent,
    messageCreatedAt: new Date().toISOString(),
    status: 0, // Отправляется
    type: 0,
  };
  messages.value.push(tempMessage);
  scrollToBottom();

  try {
    let realMessageId: number | null = null;

    if (selectedChat.value.chatId === null) {
      // Новый чат - отправляем через SendMessageToUser
      console.log(`📤 Creating new chat with ${selectedChat.value.contactLogin}`);
      const success = await signalR.sendMessageToUser(
        selectedChat.value.contactLogin,
        messageContent,
      );

      if (!success) {
        throw new Error("Failed to send message");
      }

      // Ждем создания чата
      await new Promise((resolve) => setTimeout(resolve, 500));

      if (selectedChat.value.chatId === null) {
        await signalR.getUserChats();
      }
    } else {
      // Существующий чат - отправляем и получаем ID сообщения
      realMessageId = await signalR.sendMessageToChat(selectedChat.value.chatId, messageContent);

      // Если получили реальный ID, обновляем временное сообщение
      if (realMessageId && realMessageId !== tempId) {
        const tempIndex = messages.value.findIndex((m) => m.messageId === tempId);
        if (tempIndex !== -1) {
          messages.value[tempIndex].messageId = realMessageId;
          messages.value[tempIndex].status = 1; // Доставлено
          // Сохраняем соответствие для возможного обновления через SignalR
          pendingMessages.value.set(tempId, realMessageId);
        }
      }
    }
  } catch (error) {
    console.error("Failed to send message:", error);
    toast.error("Не удалось отправить сообщение");
    // Удаляем временное сообщение при ошибке
    const tempIndex = messages.value.findIndex((m) => m.messageId === tempId);
    if (tempIndex !== -1) {
      messages.value.splice(tempIndex, 1);
    }
  } finally {
    isSending.value = false;
  }
}

function onNewMessage(message: any) {
  console.log("📨 New message received:", message);

  // Обновляем мапу чатов
  if (message.chatId && message.senderLogin) {
    if (
      !contactChatMap.value.has(message.senderLogin) &&
      message.senderLogin !== user.value?.login
    ) {
      contactChatMap.value.set(message.senderLogin, message.chatId);
      console.log(`✅ Mapped ${message.senderLogin} -> ${message.chatId} from message`);
    }
  }

  // Проверяем, относится ли сообщение к текущему чату
  if (selectedChat.value && message.chatId === selectedChat.value.chatId) {
    const messageId = message.messageId;
    const senderLogin = message.senderLogin;

    // Проверяем, не является ли это сообщение уже существующим (включая временные)
    const existingIndex = messages.value.findIndex(
      (m) =>
        m.messageId === messageId ||
        (m.messageId < 0 &&
          m.content === message.content &&
          Math.abs(new Date(m.messageCreatedAt).getTime() - new Date(message.createdAt).getTime()) <
            2000),
    );

    if (existingIndex !== -1) {
      // Обновляем существующее сообщение (меняем статус или ID)
      console.log(`🔄 Updating existing message ${messageId}`);
      messages.value[existingIndex] = {
        ...messages.value[existingIndex],
        messageId: messageId,
        status: 1, // Доставлено
      };
      // Обновляем массив для реактивности
      messages.value = [...messages.value];
    } else if (senderLogin !== user.value?.login) {
      // Новое сообщение от собеседника
      const newMsg: ChatMessage = {
        messageId: messageId || Date.now(),
        senderId: message.senderId,
        senderLogin: message.senderLogin,
        senderUsername: message.senderUsername,
        chatId: message.chatId,
        content: message.content || message.message,
        messageCreatedAt: message.createdAt || message.messageCreatedAt || new Date().toISOString(),
        status: 1, // Доставлено
        type: message.type || 0,
      };
      messages.value.push(newMsg);
      scrollToBottom();
    }
  }

  // Обновляем список чатов для отображения последнего сообщения
  signalR.getUserChats();
}

function onChatMessages(messagesList: any[]) {
  console.log("📚 Chat messages history:", messagesList.length);
  // Сортируем сообщения по возрастанию даты (старые -> новые)
  const sortedMessages = [...messagesList].sort(
    (a, b) => new Date(a.messageCreatedAt).getTime() - new Date(b.messageCreatedAt).getTime(),
  );
  messages.value = sortedMessages.map((msg) => ({
    messageId: msg.messageId,
    senderId: msg.senderId,
    senderLogin: msg.senderLogin,
    senderUsername: msg.senderUsername,
    chatId: msg.chatId,
    content: msg.content,
    messageCreatedAt: msg.messageCreatedAt,
    status: msg.status || 1,
    type: msg.type || 0,
  }));
  isLoadingMessages.value = false;
  nextTick(() => scrollToBottom());
}

function onJoinedChat(chatId: number) {
  console.log("✅ Successfully joined chat:", chatId);
}

function onConnectionChange(connected: boolean) {
  if (connected) {
    console.log("✅ SignalR connected, loading chats...");
    // Загружаем чаты при подключении
    signalR.getUserChats();

    // Если выбран чат, переподключаемся к нему
    if (selectedChat.value && selectedChat.value.chatId) {
      signalR.joinChat(selectedChat.value.chatId);
      signalR.getChatMessages(selectedChat.value.chatId, 0, 50);
    }
  } else {
    //toast.warning("Потеряно соединение с сервером");
  }
}

function scrollToBottom() {
  nextTick(() => {
    if (messagesContainer.value) {
      messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight;
    }
  });
}

function formatMessageTime(dateString: string): string {
  const date = new Date(dateString);
  const now = new Date();
  const diff = now.getTime() - date.getTime();

  if (diff < 24 * 60 * 60 * 1000) {
    return date.toLocaleTimeString("ru-RU", { hour: "2-digit", minute: "2-digit" });
  } else if (diff < 7 * 24 * 60 * 60 * 1000) {
    return date.toLocaleDateString("ru-RU", {
      weekday: "short",
      hour: "2-digit",
      minute: "2-digit",
    });
  } else {
    return date.toLocaleDateString("ru-RU", {
      day: "2-digit",
      month: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
  }
}

function getMessageGroups() {
  const groups: { date: string; messages: ChatMessage[] }[] = [];

  for (const msg of messages.value) {
    const date = new Date(msg.messageCreatedAt).toLocaleDateString("ru-RU");
    const lastGroup = groups[groups.length - 1];

    if (lastGroup && lastGroup.date === date) {
      lastGroup.messages.push(msg);
    } else {
      groups.push({ date, messages: [msg] });
    }
  }

  return groups;
}

function isOwnMessage(message: ChatMessage): boolean {
  return message.senderLogin === user.value?.login;
}

function onMessageKeydown(event: KeyboardEvent) {
  if (event.key === "Enter" && !event.shiftKey) {
    event.preventDefault();
    sendMessage();
  }
}

onMounted(async () => {
  if (isAuthenticated.value) {
    await loadContacts();

    // Регистрируем обработчики SignalR
    signalR.onNewMessage(onNewMessage);
    signalR.onChatMessages(onChatMessages);
    signalR.onConnectionChange(onConnectionChange);
    signalR.onJoinedChat(onJoinedChat);
    signalR.onUserChats(onUserChats);
    signalR.onChatInfo(onChatInfo);

    // Подключаемся к SignalR
    await signalR.connect();
  }
});

onUnmounted(async () => {
  await signalR.disconnect();
});

watch(isAuthenticated, async (authenticated) => {
  if (authenticated) {
    await loadContacts();
    await signalR.connect();
  } else {
    await signalR.disconnect();
    contacts.value = [];
    chats.value = [];
    selectedChat.value = null;
    messages.value = [];
    contactChatMap.value.clear();
    pendingMessages.value.clear();
  }
});
</script>

<template>
  <!-- template часть остается без изменений -->
  <div
    class="flex h-[calc(100vh-4rem)] flex-col overflow-hidden bg-gradient-to-br from-zinc-950 via-zinc-900 to-zinc-950"
  >
    <div class="flex h-full flex-1 overflow-hidden">
      <!-- Contacts Sidebar -->
      <div
        :class="[
          'flex w-full flex-col border-r border-white/10 bg-zinc-900/50 backdrop-blur-sm transition-all duration-300 md:w-80',
          showMobileChat ? 'hidden md:flex' : 'flex',
        ]"
      >
        <!-- Sidebar Header -->
        <div class="border-b border-white/10 px-4 py-4">
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-3">
              <div class="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-500/20">
                <MessagesSquare class="h-5 w-5 text-emerald-400" />
              </div>
              <div>
                <h2 class="font-semibold text-white">Сообщения</h2>
                <p class="text-xs text-white/50">{{ contacts.length }} контактов</p>
              </div>
            </div>
          </div>

          <!-- Search -->
          <div class="mt-4 relative">
            <Search class="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-white/40" />
            <input
              v-model="searchQuery"
              type="text"
              placeholder="Поиск контактов..."
              class="w-full rounded-xl border border-white/10 bg-white/5 py-2 pl-9 pr-4 text-sm text-white placeholder:text-white/40 focus:border-emerald-500/50 focus:outline-none focus:ring-1 focus:ring-emerald-500/50"
            />
          </div>
        </div>

        <!-- Contacts List -->
        <div class="flex-1 overflow-y-auto">
          <div
            v-if="contacts.length === 0"
            class="flex flex-col items-center justify-center py-12 text-center"
          >
            <div class="mb-3 rounded-full bg-white/5 p-3">
              <User class="h-6 w-6 text-white/30" />
            </div>
            <p class="text-sm text-white/40">Нет контактов</p>
            <p class="text-xs text-white/30">Добавьте пользователей в контакты</p>
          </div>

          <div
            v-else-if="filteredContacts.length === 0"
            class="flex flex-col items-center justify-center py-12 text-center"
          >
            <p class="text-sm text-white/40">Ничего не найдено</p>
          </div>

          <div v-else class="divide-y divide-white/5">
            <button
              v-for="contact in filteredContacts"
              :key="contact.login"
              @click="selectContact(contact)"
              class="flex w-full items-center gap-3 px-4 py-3 transition-colors hover:bg-white/5"
            >
              <div class="relative">
                <div
                  class="flex h-12 w-12 items-center justify-center rounded-full bg-gradient-to-br from-emerald-500 to-teal-500"
                >
                  <span class="text-sm font-medium text-white">
                    {{ (contact.contactName || contact.username).charAt(0).toUpperCase() }}
                  </span>
                </div>
                <div
                  class="absolute bottom-0 right-0 h-3 w-3 rounded-full bg-emerald-500 ring-2 ring-zinc-900"
                ></div>
              </div>
              <div class="flex-1 text-left">
                <div class="flex items-center justify-between">
                  <h3 class="font-medium text-white">
                    {{ contact.contactName || contact.username }}
                  </h3>
                  <span class="text-xs text-white/40">—</span>
                </div>
                <p class="text-sm text-white/50">@{{ contact.login }}</p>
              </div>
            </button>
          </div>
        </div>
      </div>

      <!-- Chat Area -->
      <div
        :class="[
          'flex flex-1 flex-col bg-zinc-950/30',
          !showMobileChat ? 'hidden md:flex' : 'flex',
        ]"
      >
        <!-- Chat Header -->
        <div
          v-if="selectedChat"
          class="flex items-center justify-between border-b border-white/10 bg-zinc-900/30 px-4 py-3"
        >
          <div class="flex items-center gap-3">
            <button
              @click="backToContacts"
              class="flex h-8 w-8 items-center justify-center rounded-lg text-white/60 transition-colors hover:bg-white/10 hover:text-white md:hidden"
            >
              <ArrowLeft class="h-5 w-5" />
            </button>

            <div class="relative">
              <div
                class="flex h-10 w-10 items-center justify-center rounded-full bg-gradient-to-br from-emerald-500 to-teal-500"
              >
                <span class="text-sm font-medium text-white">
                  {{ selectedChat.contactName.charAt(0).toUpperCase() }}
                </span>
              </div>
              <div
                class="absolute bottom-0 right-0 h-2.5 w-2.5 rounded-full bg-emerald-500 ring-2 ring-zinc-900"
              ></div>
            </div>
            <div>
              <h2 class="font-semibold text-white">{{ selectedChat.contactName }}</h2>
              <p class="text-xs text-white/50">@{{ selectedChat.contactLogin }}</p>
            </div>
          </div>

          <div class="flex items-center gap-1">
            <button
              class="rounded-lg p-2 text-white/60 transition-colors hover:bg-white/10 hover:text-white"
            >
              <Phone class="h-5 w-5" />
            </button>
            <button
              class="rounded-lg p-2 text-white/60 transition-colors hover:bg-white/10 hover:text-white"
            >
              <Video class="h-5 w-5" />
            </button>
            <button
              class="rounded-lg p-2 text-white/60 transition-colors hover:bg-white/10 hover:text-white"
            >
              <MoreVertical class="h-5 w-5" />
            </button>
          </div>
        </div>

        <!-- No Chat Selected -->
        <div v-else class="flex flex-1 flex-col items-center justify-center p-8 text-center">
          <div class="mb-4 rounded-full bg-white/5 p-4">
            <MessagesSquare class="h-10 w-10 text-white/30" />
          </div>
          <h3 class="text-lg font-medium text-white">Выберите чат</h3>
          <p class="mt-1 text-sm text-white/40">
            Выберите контакт из списка слева, чтобы начать общение
          </p>
        </div>

        <!-- Messages Area -->
        <template v-if="selectedChat">
          <!-- Messages Container -->
          <div ref="messagesContainer" class="flex-1 overflow-y-auto px-4 py-4">
            <div v-if="isLoadingMessages" class="flex justify-center py-8">
              <div class="flex items-center gap-2 text-white/40">
                <div
                  class="h-4 w-4 animate-spin rounded-full border-2 border-white/40 border-t-transparent"
                ></div>
                <span class="text-sm">Загрузка сообщений...</span>
              </div>
            </div>

            <div
              v-else-if="messages.length === 0"
              class="flex flex-col items-center justify-center py-12 text-center"
            >
              <div class="mb-3 rounded-full bg-white/5 p-3">
                <MessagesSquare class="h-6 w-6 text-white/30" />
              </div>
              <p class="text-sm text-white/40">Нет сообщений</p>
              <p class="text-xs text-white/30">
                {{
                  selectedChat.chatId === null
                    ? "Напишите первое сообщение"
                    : "Напишите что-нибудь, чтобы начать диалог"
                }}
              </p>
            </div>

            <div v-else>
              <template v-for="group in getMessageGroups()" :key="group.date">
                <div class="relative my-4 text-center">
                  <span
                    class="relative inline-block rounded-full bg-white/10 px-3 py-1 text-xs text-white/50 backdrop-blur-sm"
                  >
                    {{ group.date }}
                  </span>
                </div>

                <div
                  v-for="message in group.messages"
                  :key="message.messageId"
                  :class="['mb-3 flex', isOwnMessage(message) ? 'justify-end' : 'justify-start']"
                >
                  <div
                    :class="[
                      'max-w-[80%] rounded-2xl px-4 py-2',
                      isOwnMessage(message)
                        ? 'bg-emerald-500/20 text-white'
                        : 'bg-white/10 text-white',
                    ]"
                  >
                    <div class="text-sm break-words whitespace-pre-wrap">
                      {{ message.content }}
                    </div>
                    <div class="mt-1 flex items-center justify-end gap-1">
                      <span class="text-[10px] text-white/40">
                        {{ formatMessageTime(message.messageCreatedAt) }}
                      </span>
                      <Check
                        v-if="isOwnMessage(message) && message.status === 0"
                        class="h-3 w-3 text-white/40"
                      />
                      <CheckCheck
                        v-else-if="isOwnMessage(message) && message.status === 1"
                        class="h-3 w-3 text-emerald-400"
                      />
                      <CheckCheck
                        v-else-if="isOwnMessage(message) && message.status === 2"
                        class="h-3 w-3 text-emerald-400"
                      />
                    </div>
                  </div>
                </div>
              </template>
            </div>
          </div>

          <!-- Message Input -->
          <div class="border-t border-white/10 bg-zinc-900/30 p-4">
            <div class="flex items-end gap-2">
              <div class="flex-1">
                <textarea
                  v-model="newMessage"
                  @keydown="onMessageKeydown"
                  placeholder="Напишите сообщение..."
                  rows="1"
                  class="max-h-32 w-full resize-none rounded-2xl border border-white/10 bg-white/5 px-4 py-2.5 text-sm text-white placeholder:text-white/40 focus:border-emerald-500/50 focus:outline-none focus:ring-1 focus:ring-emerald-500/50"
                  :disabled="isSending"
                ></textarea>
              </div>
              <button
                @click="sendMessage"
                :disabled="!newMessage.trim() || isSending || !signalR.isConnected.value"
                class="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-full bg-emerald-500 text-white transition-all hover:bg-emerald-600 disabled:opacity-50 disabled:hover:bg-emerald-500"
              >
                <Send class="h-5 w-5" />
              </button>
            </div>
            <div v-if="!signalR.isConnected.value" class="mt-2 text-center text-xs text-amber-400">
              ⚠️ Нет соединения с сервером
            </div>
          </div>
        </template>
      </div>
    </div>
  </div>
</template>

<style scoped>
textarea {
  scrollbar-width: thin;
  scrollbar-color: rgba(255, 255, 255, 0.2) transparent;
}

textarea::-webkit-scrollbar {
  width: 4px;
}

textarea::-webkit-scrollbar-track {
  background: transparent;
}

textarea::-webkit-scrollbar-thumb {
  background-color: rgba(255, 255, 255, 0.2);
  border-radius: 4px;
}

/* Messages scrollbar */
div[ref="messagesContainer"]::-webkit-scrollbar {
  width: 4px;
}

div[ref="messagesContainer"]::-webkit-scrollbar-track {
  background: transparent;
}

div[ref="messagesContainer"]::-webkit-scrollbar-thumb {
  background-color: rgba(255, 255, 255, 0.2);
  border-radius: 4px;
}
</style>
