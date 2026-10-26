<script setup lang="ts">
import AddGroupMemberForm from '@/components/dev/forms/AddGroupMemberForm.vue'
import CreateAttachmentForm from '@/components/dev/forms/CreateAttachmentForm.vue'
import CreateCommentForm from '@/components/dev/forms/CreateCommentForm.vue'
import CreateContentForm from '@/components/dev/forms/CreateContentForm.vue'
import CreateCourseForm from '@/components/dev/forms/CreateCourseForm.vue'
import CreateGroupForm from '@/components/dev/forms/CreateGroupForm.vue'
import CreateModuleForm from '@/components/dev/forms/CreateModuleForm.vue'
import CreateSchoolForm from '@/components/dev/forms/CreateSchoolForm.vue'
import CreateSubmissionForm from '@/components/dev/forms/CreateSubmissionForm.vue'
import CreateUserForm from '@/components/dev/forms/CreateUserForm.vue'
import GradeSubmissionForm from '@/components/dev/forms/GradeSubmissionForm.vue'
import LinkCourseToGroupForm from '@/components/dev/forms/LinkCourseToGroupForm.vue'
import PublishContentForm from '@/components/dev/forms/PublishContentForm.vue'
</script>

<template>
  <main class="mx-auto flex max-w-2xl flex-col gap-6 p-6">
    <header class="flex flex-col gap-2">
      <h1 class="text-2xl font-semibold">Dev: create test data</h1>
      <p class="text-muted-foreground text-sm">
        Not linked anywhere. The forms talk to the API, except the ones marked "not sent yet",
        which only show the body until their endpoint exists. Dropdowns load on page load, use
        the refresh button after creating something in another tab or in Swagger.
      </p>
    </header>

    <section class="flex flex-col gap-4">
      <h2 class="text-lg font-semibold">Organisation</h2>
      <CreateSchoolForm/>
      <CreateUserForm/>
      <CreateGroupForm/>
      <CreateCourseForm/>
      <CreateModuleForm/>
      <LinkCourseToGroupForm/>
      <AddGroupMemberForm/>
    </section>

    <section class="flex flex-col gap-4">
      <h2 class="text-lg font-semibold">Content</h2>
      <CreateContentForm/>
      <PublishContentForm/>
      <CreateCommentForm/>
      <CreateAttachmentForm/>
    </section>

    <section class="flex flex-col gap-4">
      <h2 class="text-lg font-semibold">Submissions</h2>
      <CreateSubmissionForm/>
      <GradeSubmissionForm/>
    </section>
  </main>
</template>
