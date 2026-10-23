<!--
  Lägger sidans knappar i headern, till höger om brödsmulorna:

  <PageActions>
    <Button variant="outline">Avbryt</Button>
    <Button>Publicera</Button>
  </PageActions>
-->
<template>
  <Teleport defer to="#page-actions">
    <slot/>
  </Teleport>
</template>
