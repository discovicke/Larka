<script setup lang="ts">
import {computed} from 'vue'
import {useRoute} from 'vue-router'

import {useCurrentUser} from '@/composables/useCurrentUser'
import CourseContentView from '@/views/course/CourseContentView.vue'
import StudentCourseContentView from '@/views/student/StudentCourseContentView.vue'

// Kursens startsida har två vyer. Eleven får elevvyn med lås och klarmarkering, och den som kan
// redigera kursen (lärare, admin) får redigeraren. Användaren är redan hämtad när routen öppnas.
const route = useRoute()
const courseId = computed(() => String(route.params.courseId))
const {me, can} = useCurrentUser()

const isStudent = computed(() =>
  !can('editContent', courseId.value)
  && (me.value?.courses ?? []).some((course) => course.courseId === courseId.value && course.role === 'Student'))
</script>

<template>
  <StudentCourseContentView v-if="isStudent" :course-id="courseId"/>
  <CourseContentView v-else/>
</template>
