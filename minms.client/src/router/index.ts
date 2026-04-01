import { createRouter, createWebHistory } from 'vue-router'
import { AUTH_STORAGE_KEY } from '@/composables/useAuth'
import HomeView from '../views/HomeView.vue'
import UsersSearchView from '../views/UsersSearchView.vue'
import LoginView from '../views/LoginView.vue'
import ProfileView from '../views/ProfileView.vue'
import RegisterView from '../views/RegisterView.vue'
import MessagesView from '../views/MessagesView.vue'
import ContactsView from '../views/ContactsView.vue'
import SettingsLayout from '../views/SettingsLayout.vue'
import SettingsProfileView from '../views/SettingsProfileView.vue'
import SettingsSecurityView from '../views/SettingsSecurityView.vue'
import SettingsPrivacyView from '../views/SettingsPrivacyView.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'home',
      component: HomeView
    },
    {
      path: '/users',
      name: 'users-search',
      component: UsersSearchView
    },
    {
      path: '/messages',
      name: 'messages',
      component: MessagesView,
      meta: { requiresAuth: true }
    },
    {
      path: '/contacts',
      name: 'contacts',
      component: ContactsView,
      meta: { requiresAuth: true }
    },
    {
      path: '/settings',
      component: SettingsLayout,
      meta: { requiresAuth: true },
      redirect: { name: 'settings-profile' },
      children: [
        {
          path: 'profile',
          name: 'settings-profile',
          component: SettingsProfileView
        },
        {
          path: 'security',
          name: 'settings-security',
          component: SettingsSecurityView
        },
        {
          path: 'privacy',
          name: 'settings-privacy',
          component: SettingsPrivacyView
        }
      ]
    },
    {
      path: '/profile',
      name: 'profile',
      component: ProfileView,
      meta: { requiresAuth: true }
    },
    {
      path: '/login',
      name: 'login',
      component: LoginView
    },
    {
      path: '/register',
      name: 'register',
      component: RegisterView
    }
  ]
})

router.beforeEach((to) => {
  if (!to.meta.requiresAuth) return true
  if (typeof localStorage === 'undefined') return true
  if (localStorage.getItem(AUTH_STORAGE_KEY)) return true
  return { name: 'login', query: { redirect: to.fullPath } }
})

export default router
