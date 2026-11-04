<script setup lang="ts">
import {Field, FieldDescription, FieldError, FieldLabel} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import {type CreateUserForm} from '@/composables/useCreateUserForm'
import {type Schema} from '@/lib/api'
import {userRoles} from '@/lib/userRoles'

// Fälten i Skapa användare. Tillståndet och kontrollerna finns i useCreateUserForm, som den som visar fälten äger.
// roles väljer vilka skolroller som går att välja. Med en enda roll visas inget val.
const props = defineProps<{state: CreateUserForm, roles: Schema<'UserRole'>[]}>()

// Formuläret är ett reactive-objekt från useCreateUserForm och är gjort för att fyllas i här.
const {form, nameError, emailError, passwordError, microsoftSignIn} = props.state
const roleOptions = userRoles.filter((option) => props.roles.includes(option.value))
</script>

<template>
  <Field
    :data-invalid="nameError
      ? true
      : undefined"
  >
    <FieldLabel for="new-user-name">Namn</FieldLabel>
    <Input
      id="new-user-name"
      v-model="form.name"
      class="w-full"
      autocomplete="off"
      :aria-invalid="nameError
        ? true
        : undefined"
      data-test="new-user-name"
    />
    <FieldError v-if="nameError" data-test="new-user-name-error">{{ nameError }}</FieldError>
  </Field>
  <Field
    :data-invalid="emailError
      ? true
      : undefined"
  >
    <FieldLabel for="new-user-email">E-post</FieldLabel>
    <Input
      id="new-user-email"
      v-model="form.email"
      type="email"
      class="w-full"
      autocomplete="off"
      :aria-invalid="emailError
        ? true
        : undefined"
      data-test="new-user-email"
    />
    <FieldError v-if="emailError" data-test="new-user-email-error">{{ emailError }}</FieldError>
  </Field>
  <Field v-if="roleOptions.length > 1">
    <FieldLabel for="new-user-role">Roll i skolan</FieldLabel>
    <NativeSelect
      id="new-user-role"
      v-model="form.role"
      class="w-full"
      data-test="new-user-role"
    >
      <NativeSelectOption v-for="option in roleOptions" :key="option.value" :value="option.value">
        {{ option.label }}
      </NativeSelectOption>
    </NativeSelect>
  </Field>
  <p v-if="microsoftSignIn" class="text-muted-foreground text-sm" data-test="new-user-microsoft">
    Hen loggar in med skolans Microsoft-konto, med samma e-postadress.
  </p>
  <Field
    v-else
    :data-invalid="passwordError
      ? true
      : undefined"
  >
    <FieldLabel for="new-user-password">Tillfälligt lösenord (valfritt)</FieldLabel>
    <Input
      id="new-user-password"
      v-model="form.password"
      type="text"
      class="w-full"
      autocomplete="off"
      :aria-invalid="passwordError
        ? true
        : undefined"
      data-test="new-user-password"
    />
    <FieldDescription>Minst 8 tecken. Utan lösenord kan användaren inte logga in ännu.</FieldDescription>
    <FieldError v-if="passwordError" data-test="new-user-password-error">
      {{ passwordError }}
    </FieldError>
  </Field>
</template>
