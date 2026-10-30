<script setup lang="ts">
import {computed, watch} from 'vue'
import {useRoute, useRouter} from 'vue-router'

import {useCurrentUser} from '@/composables/useCurrentUser'
import CourseContentView from '@/views/course/CourseContentView.vue'
import StudentCourseContentView from '@/views/student/StudentCourseContentView.vue'

// Kursens startsida har två vyer. Eleven får elevvyn med lås och klarmarkering, och den som kan
// redigera kursen (lärare, admin) får redigeraren. Användaren är redan hämtad när routen öppnas.
const route = useRoute()
const router = useRouter()
const courseId = computed(() => String(route.params.courseId))
const {me, can} = useCurrentUser()

const isStudent = computed(() =>
  !can('editContent', courseId.value)
  && (me.value?.courses ?? []).some((course) => course.courseId === courseId.value && course.role === 'Student'))

// Länkar till en del (?del=) läses bara av elevvyn. Alla andra hamnar på delens egen sida i stället
// för överst i kursen. Det gäller kalendern, inkorgen, Idag och sparade länkar.
const wantedPart = computed(() => !isStudent.value && typeof route.query.del === 'string' && route.query.del
  ? route.query.del
  : null)

watch(wantedPart, (linkId) => {
  if (linkId) {
    router.replace({name: 'part', params: {courseId: courseId.value, linkId}})
  }
}, {immediate: true})
</script>

<template>
  <StudentCourseContentView v-if="isStudent" :course-id="courseId"/>
  <CourseContentView v-else-if="!wantedPart"/>
</template>
