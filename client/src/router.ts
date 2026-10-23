import {createRouter, createWebHistory} from 'vue-router'

import HomeView from '@/views/HomeView.vue'

export const routes = [
  {path: '/', name: 'home', component: HomeView},
  // Verktyg för att skapa testdata. Länkas inte i navigationen.
  {path: '/dev', name: 'dev', component: () => import('@/views/DevView.vue')},
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
})
