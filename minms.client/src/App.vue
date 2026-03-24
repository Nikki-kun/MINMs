<template>
  <div class="app-shell">
    <header class="app-header">
      <nav class="nav">
        <RouterLink to="/">Home</RouterLink>
        <RouterLink to="/about">About</RouterLink>
      </nav>
    </header>

    <main class="content">
      <RouterView />
    </main>
  </div>
</template>

<style scoped>
.app-shell {
  max-width: 960px;
  margin: 0 auto;
  padding: 1.5rem;
}

.app-header {
  margin-bottom: 1rem;
}

.nav {
  display: flex;
  gap: 1rem;
}

.content {
  padding-top: 0.5rem;
}
</style>
