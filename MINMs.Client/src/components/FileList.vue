<template>
  <div class="container mx-auto max-w-4xl px-4 py-8">
    <div class="mb-8 rounded-lg border border-white/10 bg-zinc-900/50 p-6">
      <h1 class="mb-4 text-2xl font-bold text-white">File Management</h1>

      <!-- Upload Section -->
      <div class="mb-8">
        <h2 class="mb-3 text-xl font-semibold text-zinc-200">Upload File</h2>
        <div class="flex flex-col gap-4 sm:flex-row sm:items-end">
          <div class="flex-1">
            <input
              ref="fileInput"
              type="file"
              accept="*/*"
              class="hidden"
              @change="handleFileSelect"
            />
            <button
              type="button"
              class="w-full rounded-lg border border-white/10 bg-zinc-800 px-4 py-2 text-white transition hover:bg-zinc-700 sm:w-auto"
              @click="triggerFileSelect"
            >
              Choose File
            </button>
            <p v-if="selectedFile" class="mt-2 text-sm text-emerald-400">
              Selected: {{ selectedFile.name }} ({{ formatFileSize(selectedFile.size) }})
            </p>
          </div>
          <button
            type="button"
            :disabled="!selectedFile || uploading"
            class="rounded-lg bg-emerald-600 px-6 py-2 font-medium text-white transition hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-50"
            @click="uploadFile"
          >
            <span v-if="uploading" class="flex items-center gap-2">
              <span
                class="size-4 animate-spin rounded-full border-2 border-white border-t-transparent"
              ></span>
              Uploading...
            </span>
            <span v-else>Upload</span>
          </button>
        </div>
        <p
          v-if="uploadMessage"
          class="mt-3 text-sm"
          :class="uploadError ? 'text-red-400' : 'text-emerald-400'"
        >
          {{ uploadMessage }}
        </p>
      </div>

      <!-- Files List Section -->
      <div>
        <div class="mb-4 flex items-center justify-between">
          <h2 class="text-xl font-semibold text-zinc-200">Your Files</h2>
          <button
            type="button"
            :disabled="loading"
            class="rounded-lg bg-zinc-800 px-4 py-2 text-sm text-white transition hover:bg-zinc-700 disabled:opacity-50"
            @click="loadFiles"
          >
            <span
              v-if="loading"
              class="inline-block size-4 animate-spin rounded-full border-2 border-white border-t-transparent"
            ></span>
            <span v-else>Refresh</span>
          </button>
        </div>

        <div v-if="loading && files.length === 0" class="py-12 text-center">
          <div
            class="inline-block size-8 animate-spin rounded-full border-4 border-emerald-500 border-t-transparent"
          ></div>
          <p class="mt-3 text-zinc-500">Loading files...</p>
        </div>

        <div
          v-else-if="files.length === 0"
          class="rounded-lg border border-white/10 bg-zinc-900/30 py-12 text-center"
        >
          <p class="text-zinc-500">No files uploaded yet</p>
        </div>

        <div v-else class="space-y-2">
          <div
            v-for="file in files"
            :key="file.id"
            class="group flex items-center justify-between rounded-lg border border-white/10 bg-zinc-900/30 p-4 transition hover:border-white/20 hover:bg-zinc-900/50"
          >
            <div class="flex min-w-0 flex-1 items-center gap-3">
              <div class="flex size-10 shrink-0 items-center justify-center rounded-lg bg-zinc-800">
                <component :is="getFileIcon(file.name)" class="size-5 text-zinc-400" />
              </div>

              <div class="min-w-0 flex-1">
                <p class="truncate font-medium text-white" :title="file.name">
                  {{ file.name }}
                </p>
                <p class="text-xs text-zinc-500">
                  {{ formatDate(file.uploadedAt) }} • {{ formatFileSize(file.size) }}
                </p>
              </div>
            </div>

            <div class="flex shrink-0 items-center gap-1">
              <button
                type="button"
                class="rounded-lg p-2 text-zinc-400 transition hover:bg-emerald-600/20 hover:text-emerald-400"
                title="Download"
                @click="downloadFile(file)"
              >
                <Download class="size-5" />
              </button>
              <button
                type="button"
                class="rounded-lg p-2 text-zinc-400 transition hover:bg-red-600/20 hover:text-red-400"
                title="Delete"
                @click="confirmDelete(file)"
              >
                <Trash2 class="size-5" />
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Delete Confirmation Modal -->
    <div
      v-if="deleteModal.show"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/70 px-4"
      @click.self="closeDeleteModal"
    >
      <div class="w-full max-w-md rounded-lg border border-white/10 bg-zinc-900 p-6 shadow-xl">
        <h3 class="mb-2 text-lg font-semibold text-white">Delete File</h3>
        <p class="mb-6 text-zinc-400">
          Are you sure you want to delete "{{ deleteModal.file?.name }}"? This action cannot be
          undone.
        </p>
        <div class="flex justify-end gap-3">
          <button
            type="button"
            class="rounded-lg border border-white/10 bg-zinc-800 px-4 py-2 text-white transition hover:bg-zinc-700"
            @click="closeDeleteModal"
          >
            Cancel
          </button>
          <button
            type="button"
            :disabled="deleting"
            class="rounded-lg bg-red-600 px-4 py-2 font-medium text-white transition hover:bg-red-700 disabled:opacity-50"
            @click="deleteFile"
          >
            <span v-if="deleting" class="flex items-center gap-2">
              <span
                class="size-4 animate-spin rounded-full border-2 border-white border-t-transparent"
              ></span>
              Deleting...
            </span>
            <span v-else>Delete</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from "vue";
import { Download, Trash2, File, FileImage, FileVideo, FileAudio, FileText } from "lucide-vue-next";
import { apiFetch } from "@/api/client";

interface FileItem {
  id: number;
  name: string;
  objectName: string;
  size: number;
  uploadedAt: string;
}

const fileInput = ref<HTMLInputElement>();
const selectedFile = ref<File | null>(null);
const uploading = ref(false);
const loading = ref(false);
const deleting = ref(false);
const uploadMessage = ref("");
const uploadError = ref(false);
const files = ref<FileItem[]>([]);

const deleteModal = ref({
  show: false,
  file: null as FileItem | null,
});

function triggerFileSelect() {
  fileInput.value?.click();
}

function handleFileSelect(event: Event) {
  const input = event.target as HTMLInputElement;
  if (input.files && input.files.length > 0) {
    selectedFile.value = input.files[0];
    uploadMessage.value = "";
  }
}

function getFileIcon(filename: string) {
  const ext = filename.split(".").pop()?.toLowerCase();

  if (["jpg", "jpeg", "png", "gif", "webp", "svg"].includes(ext || "")) {
    return FileImage;
  } else if (["mp4", "webm", "avi", "mov"].includes(ext || "")) {
    return FileVideo;
  } else if (["mp3", "wav", "ogg", "flac"].includes(ext || "")) {
    return FileAudio;
  } else if (["pdf", "doc", "docx", "txt", "md"].includes(ext || "")) {
    return FileText;
  } else {
    return File;
  }
}

function formatFileSize(bytes: number): string {
  if (bytes === 0) return "0 B";
  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
}

function formatDate(dateString: string): string {
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

  if (diffDays === 0) {
    return `Today at ${date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}`;
  } else if (diffDays === 1) {
    return "Yesterday";
  } else if (diffDays < 7) {
    return `${diffDays} days ago`;
  } else {
    return date.toLocaleDateString();
  }
}

async function uploadFile() {
  if (!selectedFile.value) return;

  uploading.value = true;
  uploadMessage.value = "";
  uploadError.value = false;

  const formData = new FormData();
  formData.append("file", selectedFile.value);

  try {
    // Используем apiFetch вместо обычного fetch
    const response = await apiFetch("/api/Files/upload", {
      method: "POST",
      body: formData,
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || "Upload failed");
    }

    const result = await response.json();
    uploadMessage.value = result.message || "File uploaded successfully!";
    uploadError.value = false;
    selectedFile.value = null;
    if (fileInput.value) fileInput.value.value = "";

    // Refresh file list
    await loadFiles();

    // Clear message after 3 seconds
    setTimeout(() => {
      uploadMessage.value = "";
    }, 3000);
  } catch (error) {
    console.error("Upload error:", error);
    uploadMessage.value = error instanceof Error ? error.message : "Upload failed";
    uploadError.value = true;
  } finally {
    uploading.value = false;
  }
}

async function loadFiles() {
  loading.value = true;
  try {
    const response = await apiFetch("/api/Files/user/files", {
      method: "GET",
    });

    if (!response.ok) {
      throw new Error("Failed to load files");
    }

    files.value = await response.json();
  } catch (error) {
    console.error("Load files error:", error);
    files.value = [];
  } finally {
    loading.value = false;
  }
}

async function downloadFile(file: FileItem) {
  try {
    // Для скачивания используем apiFetch с blob
    const response = await apiFetch(
      `/api/Files/download?objectName=${encodeURIComponent(file.objectName)}`,
      {
        method: "GET",
      },
    );

    if (!response.ok) {
      throw new Error("Download failed");
    }

    // Получаем blob и создаем ссылку для скачивания
    const blob = await response.blob();
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = file.name;
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    document.body.removeChild(a);
  } catch (error) {
    console.error("Download error:", error);
    alert("Failed to download file");
  }
}

function confirmDelete(file: FileItem) {
  deleteModal.value = {
    show: true,
    file,
  };
}

function closeDeleteModal() {
  deleteModal.value = {
    show: false,
    file: null,
  };
}

async function deleteFile() {
  if (!deleteModal.value.file) return;

  deleting.value = true;
  const file = deleteModal.value.file;

  try {
    const response = await apiFetch(
      `/api/Files/${file.id}?objectName=${encodeURIComponent(file.objectName)}`,
      {
        method: "DELETE",
      },
    );

    if (!response.ok) {
      throw new Error("Delete failed");
    }

    // Remove file from list
    files.value = files.value.filter((f) => f.id !== file.id);
    closeDeleteModal();
  } catch (error) {
    console.error("Delete error:", error);
    alert("Failed to delete file");
  } finally {
    deleting.value = false;
  }
}

onMounted(() => {
  loadFiles();
});
</script>

<style scoped>
.container {
  max-width: 1280px;
}
</style>
