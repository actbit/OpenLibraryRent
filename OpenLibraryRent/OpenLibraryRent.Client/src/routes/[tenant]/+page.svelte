<script lang="ts">
	import { page } from '$app/stores';
	import { auth } from '$lib/stores/auth';

	$: tenant = $page.params.tenant || '';
</script>

<svelte:head>
	<title>Open Library Rent</title>
</svelte:head>

<div class="home">
	<h1>Open Library Rent へようこそ</h1>

	{#if $auth.isLoading}
		<p>読み込み中...</p>
	{:else if $auth.user?.isAuthenticated}
		<div class="welcome">
			<p>こんにちは、{$auth.user.name || $auth.user.email}さん</p>
			<div class="actions">
				<a href="/{tenant}/books" class="btn primary">書籍一覧を見る</a>
				<a href="/{tenant}/books/scan" class="btn secondary">ISBNスキャン</a>
				<a href="/{tenant}/rentals" class="btn secondary">マイ貸出</a>
			</div>
		</div>
	{:else}
		<div class="login-prompt">
			<p>図書館サービスを利用するにはログインしてください</p>
			<a href="/{tenant}/auth/login" class="btn primary">ログイン</a>
		</div>
	{/if}
</div>

<style>
	.home {
		text-align: center;
		padding: 4rem 2rem;
	}

	h1 {
		font-size: 2rem;
		margin-bottom: 2rem;
		color: #1f2937;
	}

	.welcome {
		margin-bottom: 2rem;
	}

	.actions {
		display: flex;
		gap: 1rem;
		justify-content: center;
		flex-wrap: wrap;
		margin-top: 1.5rem;
	}

	.login-prompt {
		margin-top: 2rem;
	}

	.btn {
		display: inline-block;
		padding: 0.75rem 1.5rem;
		border-radius: 8px;
		text-decoration: none;
		font-weight: 500;
		transition: all 0.2s;
	}

	.btn.primary {
		background: #3b82f6;
		color: white;
	}

	.btn.primary:hover {
		background: #2563eb;
	}

	.btn.secondary {
		background: #e5e7eb;
		color: #374151;
	}

	.btn.secondary:hover {
		background: #d1d5db;
	}
</style>
