<script setup lang="ts">
import StatusBadge from '@/components/StatusBadge.vue'
import type {CourseStatus} from '@/lib/courseStatus'
import {courseStatusKey} from '@/lib/status'

defineProps<{status: CourseStatus | null}>()
</script>

<template>
  <StatusBadge v-if="status" :status="courseStatusKey(status)" data-test="course-status"/>
</template>
