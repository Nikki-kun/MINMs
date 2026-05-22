import { getAccessToken, useAuth } from "@/composables/useAuth";
import { ref, onMounted, onUnmounted } from "vue";
import * as signalR from "@microsoft/signalr";
import { useToast } from "vue-toastification";

export interface Contact {
  login: string;
  username: string;
  contactName: string;
  contactAddedAt: string;
}

export interface Message {
  messageId: number;
  senderId: number;
  senderLogin: string;
  senderUsername: string;
  chatId: number;
  content: string;
  messageCreatedAt: string;
  status: number;
  type: number;
}

export interface ChatMessage {
  chatId: number;
  message: string;
  senderId: number;
  messageId?: number;
  createdAt?: string;
  content?: string;
  senderLogin?: string;
  senderUsername?: string;
  messageCreatedAt?: string;
  status?: number;
  type?: number;
}

export interface ChatInfo {
  chatId: number;
  type: number;
  chatCreatedAt: string;
  name?: string;
  participants: ChatParticipant[];
}

export interface ChatParticipant {
  userId: number;
  login: string;
  username: string;
  avatarId?: number;
  role: number;
  joinedAt: string;
}

export interface ChatPreview {
  chatId: number;
  type: number;
  chatCreatedAt: string;
  lastMessageContent?: string;
  lastMessageAt?: string;
  unreadCount: number;
  name?: string;
}

// API методы
export async function apiFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const headers = new Headers(init?.headers);
  const t = getAccessToken();

  if (t) {
    headers.set("Authorization", `Bearer ${t}`);
  }

  return fetch(input, { ...init, headers });
}

export const contactsApi = {
  async getContacts(): Promise<Contact[]> {
    const response = await apiFetch("/api/contacts");
    if (!response.ok) throw new Error("Failed to fetch contacts");
    return response.json();
  },

  async addContact(login: string, contactName: string): Promise<void> {
    const response = await apiFetch("/api/contacts", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ login, contactName }),
    });
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || "Failed to add contact");
    }
  },

  async updateContact(login: string, contactName: string): Promise<void> {
    const response = await apiFetch(`/api/contacts/${encodeURIComponent(login)}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ contactName }),
    });
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || "Failed to update contact");
    }
  },

  async deleteContact(login: string): Promise<void> {
    const response = await apiFetch(`/api/contacts/${encodeURIComponent(login)}`, {
      method: "DELETE",
    });
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || "Failed to delete contact");
    }
  },
};

export function useSignalR(hubUrl: string) {
  const connection = ref<signalR.HubConnection | null>(null);
  const isConnected = ref(false);
  const isConnecting = ref(false);
  const reconnectAttempts = ref(0);

  let reconnectTimer: number | null = null;

  const { token } = useAuth();
  const toast = useToast();

  let messageCallbacks: ((message: any) => void)[] = [];
  let chatMessagesCallbacks: ((messages: any[]) => void)[] = [];
  let errorCallbacks: ((error: string) => void)[] = [];
  let connectionCallbacks: ((connected: boolean) => void)[] = [];
  let joinedChatCallbacks: ((chatId: number) => void)[] = [];
  let leftChatCallbacks: ((chatId: number) => void)[] = [];
  let userChatsCallbacks: ((chats: ChatPreview[]) => void)[] = [];
  let chatInfoCallbacks: ((chat: ChatInfo) => void)[] = [];
  let groupChatCreatedCallbacks: ((chat: ChatInfo) => void)[] = [];
  let participantAddedCallbacks: ((data: { chatId: number; userLogin: string }) => void)[] = [];
  let participantRemovedCallbacks: ((data: { chatId: number; userLogin: string }) => void)[] = [];
  let addedToChatCallbacks: ((chatId: number) => void)[] = [];
  let removedFromChatCallbacks: ((chatId: number) => void)[] = [];
  let chatDeletedCallbacks: ((chatId: number) => void)[] = [];
  let leftGroupCallbacks: ((chatId: number) => void)[] = [];

  const connect = async () => {
    if (isConnected.value || isConnecting.value) {
      return;
    }

    try {
      isConnecting.value = true;

      console.log("🔄 Connecting to SignalR hub at:", hubUrl);
      const accessToken = getAccessToken();
      if (!accessToken) {
        console.error("❌ No access token available");
        toast.error("Необходимо авторизоваться");
        isConnecting.value = false;
        return;
      }

      console.log("🔄 Connecting to SignalR hub with auth token");

      connection.value = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl, {
          accessTokenFactory: () => {
            const currentToken = getAccessToken();
            console.log("Token provided to SignalR:", !!currentToken);
            return currentToken || "";
          },
          transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
        })
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            if (retryContext.previousRetryCount >= 5) {
              return null;
            }
            return Math.min(5000 * Math.pow(1.5, retryContext.previousRetryCount), 30000);
          },
        })
        .configureLogging(signalR.LogLevel.Information)
        .build();

      connection.value.on("newMessage", (data: any) => {
        console.log("📨 New message received:", data);
        messageCallbacks.forEach((cb) => cb(data));
      });

      connection.value.on("chatMessages", (messages: any[]) => {
        console.log("📚 Chat messages received:", messages);
        chatMessagesCallbacks.forEach((cb) => cb(messages));
      });

      connection.value.on("error", (error: string) => {
        console.error("❌ SignalR error:", error);
        errorCallbacks.forEach((cb) => cb(error));
        toast.error(error);
      });

      connection.value.on("messageDeleted", (messageId: number) => {
        console.log("🗑️ Message deleted:", messageId);
      });

      connection.value.on("joinedChat", (data: any) => {
        const chatId = typeof data === "number" ? data : data?.chatId;
        console.log("✅ Joined chat:", chatId);
        joinedChatCallbacks.forEach((cb) => cb(chatId));
      });

      connection.value.on("leftChat", (chatId: number) => {
        console.log("👋 Left chat:", chatId);
        leftChatCallbacks.forEach((cb) => cb(chatId));
      });

      connection.value.on("connected", (message: string) => {
        console.log("✅", message);
      });

      connection.value.on("userChats", (chats: ChatPreview[]) => {
        console.log("📋 User chats received:", chats);
        userChatsCallbacks.forEach((cb) => cb(chats));
      });

      connection.value.on("chatInfo", (chat: ChatInfo) => {
        console.log("ℹ️ Chat info received:", chat);
        chatInfoCallbacks.forEach((cb) => cb(chat));
      });

      connection.value.on("groupChatCreated", (chat: ChatInfo) => {
        console.log("👥 Group chat created:", chat);
        groupChatCreatedCallbacks.forEach((cb) => cb(chat));
      });

      connection.value.on("participantAdded", (data: { chatId: number; userLogin: string }) => {
        console.log("➕ Participant added:", data);
        participantAddedCallbacks.forEach((cb) => cb(data));
      });

      connection.value.on("participantRemoved", (data: { chatId: number; userLogin: string }) => {
        console.log("➖ Participant removed:", data);
        participantRemovedCallbacks.forEach((cb) => cb(data));
      });

      connection.value.on("addedToChat", (chatId: number) => {
        console.log("🎉 Added to chat:", chatId);
        addedToChatCallbacks.forEach((cb) => cb(chatId));
      });

      connection.value.on("removedFromChat", (chatId: number) => {
        console.log("🚫 Removed from chat:", chatId);
        removedFromChatCallbacks.forEach((cb) => cb(chatId));
      });

      connection.value.on("chatDeleted", (chatId: number) => {
        console.log("🗑️ Chat deleted:", chatId);
        chatDeletedCallbacks.forEach((cb) => cb(chatId));
      });

      connection.value.on("leftGroup", (chatId: number) => {
        console.log("👋 Left group:", chatId);
        leftGroupCallbacks.forEach((cb) => cb(chatId));
      });

      connection.value.onclose((error) => {
        console.error("❌ SignalR connection closed:", error);
        isConnected.value = false;
        isConnecting.value = false;
        connectionCallbacks.forEach((cb) => cb(false));
        startReconnectTimer();
      });

      connection.value.onreconnecting((error) => {
        console.warn("🔄 SignalR reconnecting:", error);
        isConnected.value = false;
        connectionCallbacks.forEach((cb) => cb(false));
      });

      connection.value.onreconnected((connectionId) => {
        console.log("✅ Reconnected:", connectionId);
        isConnected.value = true;
        isConnecting.value = false;
        reconnectAttempts.value = 0;
        toast.success("Соединение восстановлено");
        connectionCallbacks.forEach((cb) => cb(true));
      });

      await connection.value.start();

      isConnected.value = true;
      isConnecting.value = false;
      reconnectAttempts.value = 0;

      console.log("✅ SignalR connected successfully");
      connectionCallbacks.forEach((cb) => cb(true));
    } catch (error) {
      console.error("❌ SignalR connection error:", error);
      isConnected.value = false;
      isConnecting.value = false;
      startReconnectTimer();
    }
  };

  const startReconnectTimer = () => {
    if (reconnectTimer) {
      clearTimeout(reconnectTimer);
    }

    reconnectTimer = window.setTimeout(() => {
      if (!isConnected.value && !isConnecting.value) {
        reconnectAttempts.value++;
        console.log(`🔄 Reconnect attempt ${reconnectAttempts.value}`);
        connect();
      }
    }, 5000);
  };

  // Отправить сообщение в чат
  const sendMessageToChat = async (chatId: number, message: string, type: number = 0) => {
    if (!connection.value || !isConnected.value) {
      toast.error("Нет соединения с сервером");
      return false;
    }

    try {
      await connection.value.invoke("SendMessageToChat", chatId, message, type);
      console.log("📤 Message sent to chat:", chatId);
      return true;
    } catch (error) {
      console.error("❌ Failed to send message:", error);
      toast.error("Не удалось отправить сообщение");
      return false;
    }
  };

  // Отправить сообщение пользователю
  const sendMessageToUser = async (userLogin: string, message: string) => {
    if (!connection.value || !isConnected.value) {
      toast.error("Нет соединения с сервером");
      return false;
    }

    try {
      await connection.value.invoke("SendMessageToUser", userLogin, message);
      console.log("📤 Message sent to user:", userLogin);
      return true;
    } catch (error) {
      console.error("❌ Failed to send message to user:", error);
      toast.error("Не удалось отправить сообщение");
      return false;
    }
  };

  // Присоединиться к чату
  const joinChat = async (chatId: number) => {
    if (!connection.value || !isConnected.value) {
      console.warn("Cannot join chat: not connected");
      return false;
    }

    try {
      await connection.value.invoke("JoinChatGroup", chatId);
      console.log("📡 Join chat invoked:", chatId);
      return true;
    } catch (error) {
      console.error("❌ Failed to join chat:", error);
      return false;
    }
  };

  // Покинуть чат
  const leaveChat = async (chatId: number) => {
    if (!connection.value || !isConnected.value) {
      return false;
    }

    try {
      await connection.value.invoke("LeaveChatGroup", chatId);
      return true;
    } catch (error) {
      console.error("❌ Failed to leave chat:", error);
      return false;
    }
  };

  // Получить сообщения чата
  const getChatMessages = async (chatId: number, offset: number = 0, limit: number = 50) => {
    if (!connection.value || !isConnected.value) {
      console.warn("Cannot get messages: not connected");
      return;
    }

    try {
      await connection.value.invoke("GetChatMessages", chatId, offset, limit);
      console.log("📡 GetChatMessages invoked:", chatId, offset, limit);
    } catch (error) {
      console.error("❌ Failed to get chat messages:", error);
    }
  };

  // Удалить сообщение
  const deleteMessage = async (messageId: number) => {
    if (!connection.value || !isConnected.value) {
      return false;
    }

    try {
      await connection.value.invoke("DeleteMessage", messageId);
      return true;
    } catch (error) {
      console.error("❌ Failed to delete message:", error);
      return false;
    }
  };

  // Получить чаты пользователя
  const getUserChats = async () => {
    if (!connection.value || !isConnected.value) {
      console.warn("Cannot get user chats: not connected");
      return;
    }

    try {
      await connection.value.invoke("GetUserChats");
      console.log("📡 GetUserChats invoked");
    } catch (error) {
      console.error("❌ Failed to get user chats:", error);
    }
  };

  // Получить информацию о чате
  const getChatInfo = async (chatId: number) => {
    if (!connection.value || !isConnected.value) {
      console.warn("Cannot get chat info: not connected");
      return;
    }

    try {
      await connection.value.invoke("GetChatInfo", chatId);
      console.log("📡 GetChatInfo invoked:", chatId);
    } catch (error) {
      console.error("❌ Failed to get chat info:", error);
    }
  };

  // Создать групповой чат
  const createGroupChat = async (
    chatName: string,
    participantLogins: string[],
    initialMessage?: string,
  ) => {
    if (!connection.value || !isConnected.value) {
      toast.error("Нет соединения с сервером");
      return false;
    }

    try {
      await connection.value.invoke("CreateGroupChat", chatName, participantLogins, initialMessage);
      console.log("👥 CreateGroupChat invoked");
      return true;
    } catch (error) {
      console.error("❌ Failed to create group chat:", error);
      toast.error("Не удалось создать групповой чат");
      return false;
    }
  };

  // Добавить участника в группу
  const addParticipantToGroup = async (chatId: number, userLogin: string) => {
    if (!connection.value || !isConnected.value) {
      toast.error("Нет соединения с сервером");
      return false;
    }

    try {
      await connection.value.invoke("AddParticipantToGroup", chatId, userLogin);
      console.log("➕ AddParticipantToGroup invoked");
      return true;
    } catch (error) {
      console.error("❌ Failed to add participant:", error);
      toast.error("Не удалось добавить участника");
      return false;
    }
  };

  // Удалить участника из группы
  const removeParticipantFromGroup = async (chatId: number, userLogin: string) => {
    if (!connection.value || !isConnected.value) {
      toast.error("Нет соединения с сервером");
      return false;
    }

    try {
      await connection.value.invoke("RemoveParticipantFromGroup", chatId, userLogin);
      console.log("➖ RemoveParticipantFromGroup invoked");
      return true;
    } catch (error) {
      console.error("❌ Failed to remove participant:", error);
      toast.error("Не удалось удалить участника");
      return false;
    }
  };

  // Покинуть группу
  const leaveGroup = async (chatId: number) => {
    if (!connection.value || !isConnected.value) {
      return false;
    }

    try {
      await connection.value.invoke("LeaveGroup", chatId);
      return true;
    } catch (error) {
      console.error("❌ Failed to leave group:", error);
      return false;
    }
  };

  // Удалить чат
  const deleteChat = async (chatId: number) => {
    if (!connection.value || !isConnected.value) {
      return false;
    }

    try {
      await connection.value.invoke("DeleteChat", chatId);
      return true;
    } catch (error) {
      console.error("❌ Failed to delete chat:", error);
      return false;
    }
  };

  // Регистрация коллбеков
  const onNewMessage = (callback: (message: any) => void) => {
    messageCallbacks.push(callback);
    return () => {
      messageCallbacks = messageCallbacks.filter((cb) => cb !== callback);
    };
  };

  const onChatMessages = (callback: (messages: any[]) => void) => {
    chatMessagesCallbacks.push(callback);
    return () => {
      chatMessagesCallbacks = chatMessagesCallbacks.filter((cb) => cb !== callback);
    };
  };

  const onError = (callback: (error: string) => void) => {
    errorCallbacks.push(callback);
    return () => {
      errorCallbacks = errorCallbacks.filter((cb) => cb !== callback);
    };
  };

  const onConnectionChange = (callback: (connected: boolean) => void) => {
    connectionCallbacks.push(callback);
    return () => {
      connectionCallbacks = connectionCallbacks.filter((cb) => cb !== callback);
    };
  };

  const onJoinedChat = (callback: (chatId: number) => void) => {
    joinedChatCallbacks.push(callback);
    return () => {
      joinedChatCallbacks = joinedChatCallbacks.filter((cb) => cb !== callback);
    };
  };

  const onLeftChat = (callback: (chatId: number) => void) => {
    leftChatCallbacks.push(callback);
    return () => {
      leftChatCallbacks = leftChatCallbacks.filter((cb) => cb !== callback);
    };
  };

  const onUserChats = (callback: (chats: ChatPreview[]) => void) => {
    userChatsCallbacks.push(callback);
    return () => {
      userChatsCallbacks = userChatsCallbacks.filter((cb) => cb !== callback);
    };
  };

  const onChatInfo = (callback: (chat: ChatInfo) => void) => {
    chatInfoCallbacks.push(callback);
    return () => {
      chatInfoCallbacks = chatInfoCallbacks.filter((cb) => cb !== callback);
    };
  };

  const onGroupChatCreated = (callback: (chat: ChatInfo) => void) => {
    groupChatCreatedCallbacks.push(callback);
    return () => {
      groupChatCreatedCallbacks = groupChatCreatedCallbacks.filter((cb) => cb !== callback);
    };
  };

  const onParticipantAdded = (callback: (data: { chatId: number; userLogin: string }) => void) => {
    participantAddedCallbacks.push(callback);
    return () => {
      participantAddedCallbacks = participantAddedCallbacks.filter((cb) => cb !== callback);
    };
  };

  const onParticipantRemoved = (
    callback: (data: { chatId: number; userLogin: string }) => void,
  ) => {
    participantRemovedCallbacks.push(callback);
    return () => {
      participantRemovedCallbacks = participantRemovedCallbacks.filter((cb) => cb !== callback);
    };
  };

  const onAddedToChat = (callback: (chatId: number) => void) => {
    addedToChatCallbacks.push(callback);
    return () => {
      addedToChatCallbacks = addedToChatCallbacks.filter((cb) => cb !== callback);
    };
  };

  const onRemovedFromChat = (callback: (chatId: number) => void) => {
    removedFromChatCallbacks.push(callback);
    return () => {
      removedFromChatCallbacks = removedFromChatCallbacks.filter((cb) => cb !== callback);
    };
  };

  const onChatDeleted = (callback: (chatId: number) => void) => {
    chatDeletedCallbacks.push(callback);
    return () => {
      chatDeletedCallbacks = chatDeletedCallbacks.filter((cb) => cb !== callback);
    };
  };

  const onLeftGroup = (callback: (chatId: number) => void) => {
    leftGroupCallbacks.push(callback);
    return () => {
      leftGroupCallbacks = leftGroupCallbacks.filter((cb) => cb !== callback);
    };
  };

  const disconnect = async () => {
    if (reconnectTimer) {
      clearTimeout(reconnectTimer);
      reconnectTimer = null;
    }

    if (connection.value) {
      try {
        await connection.value.stop();
      } catch (error) {
        console.error("❌ Error stopping connection:", error);
      }
      connection.value = null;
    }

    isConnected.value = false;
    isConnecting.value = false;
  };

  return {
    connection,
    isConnected,
    isConnecting,
    reconnectAttempts,
    connect,
    disconnect,
    sendMessageToChat,
    sendMessageToUser,
    joinChat,
    leaveChat,
    getChatMessages,
    deleteMessage,
    getUserChats,
    getChatInfo,
    createGroupChat,
    addParticipantToGroup,
    removeParticipantFromGroup,
    leaveGroup,
    deleteChat,
    onNewMessage,
    onChatMessages,
    onError,
    onConnectionChange,
    onJoinedChat,
    onLeftChat,
    onUserChats,
    onChatInfo,
    onGroupChatCreated,
    onParticipantAdded,
    onParticipantRemoved,
    onAddedToChat,
    onRemovedFromChat,
    onChatDeleted,
    onLeftGroup,
  };
}
