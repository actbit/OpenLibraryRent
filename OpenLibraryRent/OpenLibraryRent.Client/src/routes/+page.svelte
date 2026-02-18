<script lang="ts">
	import { onMount } from 'svelte';

	let tenants = $state<any[]>([]);
	let loading = $state(true);

	async function loadTenants() {
		try {
			const res = await fetch('/api/tenants');
			if (res.ok) {
				tenants = await res.json();
			}
		} catch (e) {
			console.error('Failed to load tenants', e);
		} finally {
			loading = false;
		}
	}

	onMount(() => {
		loadTenants();
	});
</script>

<svelte:head>
	<title>Open Library Rent - 図書貸出管理システム</title>
</svelte:head>

<div class="landing">
	<header class="hero">
		<h1>📚 Open Library Rent</h1>
		<p class="tagline">マルチテナント対応の図書貸出管理システム</p>
	</header>

	<section class="features">
		<div class="feature">
			<span class="icon">📖</span>
			<h3>書籍管理</h3>
			<p>ISBNスキャンで簡単登録。Open Library API連携で自動的に書籍情報を取得。</p>
		</div>
		<div class="feature">
			<span class="icon">📱</span>
			<h3>貸出管理</h3>
			<p>貸出・返却の記録、延滞管理、貸出履歴の追跡。</p>
		</div>
		<div class="feature">
			<span class="icon">🏢</span>
			<h3>マルチテナント</h3>
			<p>組織ごとに独立した環境。OIDC連携でセキュアな認証。</p>
		</div>
	</section>

	<section class="actions">
		<a href="/create-tenant" class="btn primary">
			新しいテナントを作成
		</a>
	</section>

	<section class="tenants-section">
		<h2>テナント一覧</h2>
		{#if loading}
			<p class="loading">読み込み中...</p>
		{:else if tenants.length === 0}
			<p class="empty">テナントがありません。最初のテナントを作成してください。</p>
		{:else}
			<div class="tenants-grid">
				{#each tenants as tenant}
					<a href="/{tenant.identifier}" class="tenant-card">
						<h3>{tenant.name || tenant.identifier}</h3>
						<span class="tenant-id">@{tenant.identifier}</span>
						<div class="tenant-stats">
							<span>👥 {tenant.userCount}ユーザー</span>
							<span>📚 {tenant.bookCount}書籍</span>
						</div>
					</a>
				{/each}
			</div>
		{/if}
	</section>

	<footer class="footer">
		<p>Powered by Open Library API</p>
	</footer>
</div>

<style>
	.landing {
		min-height: 100vh;
		display: flex;
		flex-direction: column;
	}

	.hero {
		text-align: center;
		padding: 4rem 1rem;
		background: linear-gradient(135deg, #1f2937 0%, #374151 100%);
		color: white;
	}

	.hero h1 {
		font-size: 2.5rem;
		margin: 0 0 0.5rem;
	}

	.tagline {
		font-size: 1.125rem;
		color: #9ca3af;
		margin: 0;
	}

	.features {
		display: grid;
		grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
		gap: 2rem;
		padding: 3rem 2rem;
		max-width: 1000px;
		margin: 0 auto;
	}

	.feature {
		text-align: center;
		padding: 1.5rem;
	}

	.feature .icon {
		font-size: 3rem;
		display: block;
		margin-bottom: 1rem;
	}

	.feature h3 {
		font-size: 1.25rem;
		margin: 0 0 0.5rem;
		color: #1f2937;
	}

	.feature p {
		color: #6b7280;
		margin: 0;
		line-height: 1.6;
	}

	.actions {
		text-align: center;
		padding: 2rem;
	}

	.btn {
		display: inline-block;
		padding: 1rem 2rem;
		font-size: 1.125rem;
		font-weight: 600;
		text-decoration: none;
		border-radius: 8px;
		transition: all 0.2s;
	}

	.btn.primary {
		background: #3b82f6;
		color: white;
	}

	.btn.primary:hover {
		background: #2563eb;
		transform: translateY(-2px);
	}

	.tenants-section {
		flex: 1;
		padding: 2rem;
		max-width: 1000px;
		margin: 0 auto;
		width: 100%;
		box-sizing: border-box;
	}

	.tenants-section h2 {
		font-size: 1.5rem;
		color: #1f2937;
		margin: 0 0 1.5rem;
	}

	.tenants-grid {
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
		gap: 1rem;
	}

	.tenant-card {
		display: block;
		padding: 1.25rem;
		background: white;
		border-radius: 8px;
		box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
		text-decoration: none;
		color: inherit;
		transition: all 0.2s;
	}

	.tenant-card:hover {
		transform: translateY(-2px);
		box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
	}

	.tenant-card h3 {
		font-size: 1rem;
		margin: 0 0 0.25rem;
		color: #1f2937;
	}

	.tenant-id {
		font-size: 0.875rem;
		color: #6b7280;
	}

	.tenant-stats {
		display: flex;
		gap: 1rem;
		margin-top: 0.75rem;
		font-size: 0.75rem;
		color: #9ca3af;
	}

	.loading, .empty {
		text-align: center;
		padding: 2rem;
		color: #6b7280;
	}

	.footer {
		text-align: center;
		padding: 2rem;
		background: #f3f4f6;
		color: #6b7280;
		font-size: 0.875rem;
	}
</style>
