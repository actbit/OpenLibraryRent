<script lang="ts">
	import '../app.css';
	import { onMount } from 'svelte';
	import { page } from '$app/stores';
	import { auth } from '$lib/stores/auth';

	let { children } = $props();

	$: tenant = $page.params.tenant || '';

	onMount(() => {
		if (tenant) {
			auth.checkAuth(tenant);
		}
	});
</script>

<div class="app">
	<header class="header">
		<nav class="nav">
			<a href="/{tenant}" class="logo">Open Library Rent</a>

			<div class="nav-links">
				{#if $auth.user?.isAuthenticated}
					<a href="/{tenant}/books">書籍一覧</a>
					<a href="/{tenant}/rentals">貸出状況</a>
					{#if $auth.user.roles?.includes('Admin') || $auth.user.roles?.includes('Librarian')}
						<a href="/{tenant}/rentals/overdue">延滞一覧</a>
					{/if}
					<span class="user-name">{$auth.user.name || $auth.user.email}</span>
					<button onclick={() => auth.logout(tenant)}>ログアウト</button>
				{:else}
					<a href="/{tenant}/auth/login">ログイン</a>
				{/if}
			</div>
		</nav>
	</header>

	<main class="main">
		{@render children()}
	</main>
</div>

<style>
	.app {
		min-height: 100vh;
		display: flex;
		flex-direction: column;
	}

	.header {
		background: #1f2937;
		color: white;
		padding: 1rem 2rem;
	}

	.nav {
		display: flex;
		justify-content: space-between;
		align-items: center;
		max-width: 1200px;
		margin: 0 auto;
	}

	.logo {
		font-size: 1.25rem;
		font-weight: bold;
		color: white;
		text-decoration: none;
	}

	.nav-links {
		display: flex;
		gap: 1rem;
		align-items: center;
	}

	.nav-links a {
		color: #d1d5db;
		text-decoration: none;
		transition: color 0.2s;
	}

	.nav-links a:hover {
		color: white;
	}

	.user-name {
		color: #9ca3af;
	}

	.nav-links button {
		background: transparent;
		color: #d1d5db;
		border: 1px solid #4b5563;
		padding: 0.5rem 1rem;
		border-radius: 4px;
		cursor: pointer;
		transition: all 0.2s;
	}

	.nav-links button:hover {
		background: #374151;
		color: white;
	}

	.main {
		flex: 1;
		padding: 2rem;
		max-width: 1200px;
		margin: 0 auto;
		width: 100%;
	}
</style>
