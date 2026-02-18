<script lang="ts">
	let identifier = $state('');
	let name = $state('');
	let loading = $state(false);
	let error = $state('');
	let success = $state(false);
	let createdTenant = $state<any>(null);

	// 認証状態
	let isAuthenticated = $state(false);
	let userEmail = $state('');
	let creationLimit = $state<any>(null);
	let checkingAuth = $state(true);

	// ページ読み込み時に認証状態を確認
	$effect(() => {
		checkAuth();
	});

	async function checkAuth() {
		try {
			const res = await fetch('/api/systemauth/check', {
				credentials: 'include'
			});
			const data = await res.json();
			isAuthenticated = data.isAuthenticated;
			userEmail = data.email || '';

			if (isAuthenticated) {
				// 作成制限を取得
				const limitRes = await fetch('/api/tenants/creation-limit', {
					credentials: 'include'
				});
				if (limitRes.ok) {
					creationLimit = await limitRes.json();
				}
			}
		} catch (e) {
			console.error('Auth check failed:', e);
		} finally {
			checkingAuth = false;
		}
	}

	function loginWithMicrosoft() {
		window.location.href = '/api/systemauth/microsoft-login?returnUrl=/create-tenant';
	}

	async function createTenant() {
		if (!identifier.trim()) {
			error = 'テナントIDを入力してください';
			return;
		}

		if (!/^[a-zA-Z0-9\-_]+$/.test(identifier)) {
			error = 'テナントIDは英数字、ハイフン、アンダースコアのみ使用できます';
			return;
		}

		loading = true;
		error = '';

		try {
			const res = await fetch('/api/tenants/create', {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				credentials: 'include',
				body: JSON.stringify({
					identifier: identifier.trim().toLowerCase(),
					name: name.trim() || identifier.trim()
				})
			});

			const data = await res.json();

			if (!res.ok) {
				throw new Error(data.message || 'テナントの作成に失敗しました');
			}

			createdTenant = data;
			success = true;
		} catch (e: any) {
			error = e.message || 'テナントの作成に失敗しました';
		} finally {
			loading = false;
		}
	}
</script>

<svelte:head>
	<title>テナント作成 - Open Library Rent</title>
</svelte:head>

<div class="create-tenant-page">
	<a href="/" class="back-link">← トップに戻る</a>

	{#if checkingAuth}
		<div class="loading-card">
			<div class="spinner"></div>
			<p>読み込み中...</p>
		</div>
	{:else if !isAuthenticated}
		<div class="auth-card">
			<h1>テナントを作成</h1>
			<p class="description">
				テナントを作成するには、Microsoft アカウントでログインしてください。
			</p>
			<p class="note">
				※ Microsoft ログインは濫用防止のための認証です。<br>
				テナントごとのOIDC設定は別途行います。
			</p>
			<button class="btn microsoft" onclick={loginWithMicrosoft}>
				<svg viewBox="0 0 24 24" width="24" height="24">
					<path fill="#F25022" d="M1 1h10v10H1z"/>
					<path fill="#00A4EF" d="M1 13h10v10H1z"/>
					<path fill="#7FBA00" d="M13 1h10v10H13z"/>
					<path fill="#FFB900" d="M13 13h10v10H13z"/>
				</svg>
				Microsoft でログイン
			</button>
		</div>
	{:else if creationLimit && !creationLimit.canCreate}
		<div class="limit-card">
			<div class="limit-icon">⚠️</div>
			<h2>作成制限に達しました</h2>
			<p>
				<strong>{userEmail}</strong> で作成できるテナント数の上限（{creationLimit.maxCount}件）に達しています。
			</p>
			<p class="hint">
				既存のテナントを削除すると、新しいテナントを作成できるようになります。
			</p>
		</div>
	{:else if success}
		<div class="success-card">
			<div class="success-icon">✓</div>
			<h2>テナントを作成しました！</h2>
			<p class="tenant-info">
				<strong>{createdTenant?.name}</strong> (@{createdTenant?.identifier})
			</p>
			<div class="next-steps">
				<p>次のステップ：</p>
				<ol>
					<li>OIDC設定を構成する</li>
					<li>ユーザーを招待する</li>
					<li>書籍を登録する</li>
				</ol>
			</div>
			<a href="/{createdTenant?.identifier}" class="btn primary">
				テナントに移動 →
			</a>
		</div>
	{:else}
		<div class="form-card">
			<h1>新しいテナントを作成</h1>
			<p class="description">
				組織やグループごとに独立した図書管理環境を作成できます。
			</p>

			{#if creationLimit}
				<p class="limit-info">
					ログイン中: {userEmail}<br>
					作成可能: 残り {creationLimit.remaining} 件（最大 {creationLimit.maxCount} 件）
				</p>
			{/if}

			{#if error}
				<p class="error">{error}</p>
			{/if}

			<form onsubmit={(e) => { e.preventDefault(); createTenant(); }}>
				<div class="form-group">
					<label for="identifier">テナントID</label>
					<input
						id="identifier"
						type="text"
						bind:value={identifier}
						placeholder="例: my-library"
						disabled={loading}
					/>
					<span class="hint">URLで使用されます: /{identifier || 'my-library'}/books</span>
				</div>

				<div class="form-group">
					<label for="name">テナント名</label>
					<input
						id="name"
						type="text"
						bind:value={name}
						placeholder="例: 〇〇市立図書館"
						disabled={loading}
					/>
					<span class="hint">省略可。テナントIDが使用されます。</span>
				</div>

				<button type="submit" class="btn primary" disabled={loading}>
					{loading ? '作成中...' : 'テナントを作成'}
				</button>
			</form>
		</div>
	{/if}
</div>

<style>
	.create-tenant-page {
		min-height: 100vh;
		padding: 2rem;
		background: #f9fafb;
	}

	.back-link {
		display: inline-block;
		margin-bottom: 1.5rem;
		color: #6b7280;
		text-decoration: none;
	}

	.back-link:hover {
		color: #3b82f6;
	}

	.loading-card, .auth-card, .limit-card, .form-card, .success-card {
		max-width: 500px;
		margin: 0 auto;
		padding: 2rem;
		background: white;
		border-radius: 12px;
		box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
		text-align: center;
	}

	.spinner {
		width: 40px;
		height: 40px;
		margin: 0 auto 1rem;
		border: 3px solid #e5e7eb;
		border-top-color: #3b82f6;
		border-radius: 50%;
		animation: spin 1s linear infinite;
	}

	@keyframes spin {
		to { transform: rotate(360deg); }
	}

	h1 {
		font-size: 1.5rem;
		margin: 0 0 0.5rem;
		color: #1f2937;
	}

	h2 {
		font-size: 1.25rem;
		margin: 0 0 0.5rem;
		color: #1f2937;
	}

	.description {
		color: #6b7280;
		margin: 0 0 1.5rem;
	}

	.note {
		font-size: 0.875rem;
		color: #9ca3af;
		margin-bottom: 1.5rem;
	}

	.limit-info {
		font-size: 0.875rem;
		color: #6b7280;
		background: #f3f4f6;
		padding: 0.75rem;
		border-radius: 6px;
		margin-bottom: 1.5rem;
		text-align: left;
	}

	.form-card {
		text-align: left;
	}

	.form-group {
		margin-bottom: 1.5rem;
	}

	.form-group label {
		display: block;
		font-weight: 500;
		margin-bottom: 0.5rem;
		color: #374151;
	}

	.form-group input {
		width: 100%;
		padding: 0.75rem;
		border: 1px solid #d1d5db;
		border-radius: 6px;
		font-size: 1rem;
		box-sizing: border-box;
	}

	.form-group input:focus {
		outline: none;
		border-color: #3b82f6;
		box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
	}

	.hint {
		display: block;
		margin-top: 0.5rem;
		font-size: 0.75rem;
		color: #9ca3af;
	}

	.btn {
		display: flex;
		align-items: center;
		justify-content: center;
		gap: 0.75rem;
		width: 100%;
		padding: 0.875rem;
		font-size: 1rem;
		font-weight: 600;
		text-decoration: none;
		border: none;
		border-radius: 6px;
		cursor: pointer;
		transition: all 0.2s;
	}

	.btn.primary {
		background: #3b82f6;
		color: white;
	}

	.btn.primary:hover:not(:disabled) {
		background: #2563eb;
	}

	.btn.microsoft {
		background: #00a4ef;
		color: white;
		border: none;
	}

	.btn.microsoft:hover {
		background: #0078d4;
	}

	.btn:disabled {
		opacity: 0.6;
		cursor: not-allowed;
	}

	.error {
		padding: 0.75rem;
		background: #fee2e2;
		color: #991b1b;
		border-radius: 6px;
		margin-bottom: 1rem;
	}

	.limit-card {
		background: #fef3c7;
		border: 1px solid #fcd34d;
	}

	.limit-icon {
		font-size: 3rem;
		margin-bottom: 1rem;
	}

	.success-card {
		text-align: center;
	}

	.success-icon {
		display: inline-flex;
		align-items: center;
		justify-content: center;
		width: 4rem;
		height: 4rem;
		background: #dcfce7;
		color: #166534;
		border-radius: 50%;
		font-size: 2rem;
		margin-bottom: 1rem;
	}

	.tenant-info {
		font-size: 1.125rem;
		color: #374151;
		margin-bottom: 1.5rem;
	}

	.next-steps {
		text-align: left;
		background: #f3f4f6;
		padding: 1rem;
		border-radius: 6px;
		margin-bottom: 1.5rem;
	}

	.next-steps p {
		margin: 0 0 0.5rem;
		font-weight: 500;
	}

	.next-steps ol {
		margin: 0;
		padding-left: 1.5rem;
		color: #6b7280;
	}

	.next-steps li {
		margin: 0.25rem 0;
	}
</style>
