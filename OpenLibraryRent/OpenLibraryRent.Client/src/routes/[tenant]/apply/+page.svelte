<script lang="ts">
	import { page } from '$app/stores';

	let tenant = $derived($page.params.tenant);

	let loading = $state(true);
	let submitting = $state(false);
	let error = $state('');
	let success = $state(false);
	let existingStatus = $state<string | null>(null);

	// フォームデータ
	let email = $state('');
	let displayName = $state('');
	let formData = $state<Record<string, string>>({});

	// テナント設定
	let requireApproval = $state(false);
	let formFields = $state<any[]>([]);
	let instructions = $state('');

	$effect(() => {
		loadSettings();
		checkStatus();
	});

	async function loadSettings() {
		try {
			const res = await fetch(`/${tenant}/api/userapproval/settings`, {
				credentials: 'include'
			});

			if (res.ok) {
				const data = await res.json();
				requireApproval = data.requireApproval;
				instructions = data.approvalInstructions || '';

				if (data.approvalFormFields) {
					formFields = JSON.parse(data.approvalFormFields);
				}
			}
		} catch (e) {
			console.error('Failed to load settings:', e);
		} finally {
			loading = false;
		}
	}

	async function checkStatus() {
		// ログイン済みの場合はメールアドレスを取得
		try {
			const authRes = await fetch(`/${tenant}/auth/me`, {
				credentials: 'include'
			});

			if (authRes.ok) {
				const authData = await authRes.json();
				email = authData.email || '';
				displayName = authData.name || '';

				if (email) {
					const statusRes = await fetch(`/${tenant}/api/userapproval/status?email=${encodeURIComponent(email)}`);
					if (statusRes.ok) {
						const statusData = await statusRes.json();
						existingStatus = statusData.status;
					}
				}
			}
		} catch (e) {
			console.error('Failed to check status:', e);
		}
	}

	async function submitApplication() {
		if (!email.trim()) {
			error = 'メールアドレスを入力してください';
			return;
		}

		submitting = true;
		error = '';

		try {
			const applicationData = formFields.length > 0 ? JSON.stringify(formData) : null;

			const res = await fetch(`/${tenant}/api/userapproval/apply`, {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				credentials: 'include',
				body: JSON.stringify({
					email: email.trim(),
					displayName: displayName.trim() || null,
					applicationData
				})
			});

			const data = await res.json();

			if (!res.ok) {
				throw new Error(data.message || '申請に失敗しました');
			}

			success = true;
			existingStatus = 'pending';
		} catch (e: any) {
			error = e.message || '申請に失敗しました';
		} finally {
			submitting = false;
		}
	}
</script>

<svelte:head>
	<title>参加申請 - {tenant}</title>
</svelte:head>

<div class="apply-page">
	{#if loading}
		<div class="loading">
			<div class="spinner"></div>
			<p>読み込み中...</p>
		</div>
	{:else if !requireApproval}
		<div class="card">
			<h1>このテナントは承認不要です</h1>
			<p>直接ログインしてください。</p>
			<a href="/{tenant}/auth/login" class="btn primary">ログイン</a>
		</div>
	{:else if existingStatus === 'approved'}
		<div class="card success">
			<div class="icon">✓</div>
			<h1>承認済み</h1>
			<p>あなたの申請は承認されました。</p>
			<a href="/{tenant}/auth/login" class="btn primary">ログイン</a>
		</div>
	{:else if existingStatus === 'pending'}
		<div class="card pending">
			<div class="icon">⏳</div>
			<h1>承認待ち</h1>
			<p>あなたの申請は現在審査中です。承認されるまでお待ちください。</p>
		</div>
	{:else if success}
		<div class="card success">
			<div class="icon">✓</div>
			<h1>申請を送信しました</h1>
			<p>管理者が承認するとログインできるようになります。</p>
		</div>
	{:else}
		<div class="card">
			<h1>参加申請</h1>
			<p class="tenant-name">{tenant}</p>

			{#if instructions}
				<div class="instructions">
					{instructions}
				</div>
			{/if}

			{#if existingStatus === 'rejected'}
				<div class="warning">
					以前の申請が却下されました。再度申請してください。
				</div>
			{/if}

			{#if error}
				<div class="error">{error}</div>
			{/if}

			<form onsubmit={(e) => { e.preventDefault(); submitApplication(); }}>
				<div class="form-group">
					<label>メールアドレス *</label>
					<input type="email" bind:value={email} required disabled={submitting} />
				</div>

				<div class="form-group">
					<label>表示名</label>
					<input type="text" bind:value={displayName} disabled={submitting} />
				</div>

				{#each formFields as field}
					<div class="form-group">
						<label>
							{field.label || field.name}
							{#if field.required}*{/if}
						</label>
						{#if field.type === 'textarea'}
							<textarea
								bind:value={formData[field.name]}
								disabled={submitting}
								required={field.required}
							></textarea>
						{:else if field.type === 'select'}
							<select
								bind:value={formData[field.name]}
								disabled={submitting}
								required={field.required}
							>
								<option value="">選択してください</option>
								{#each field.options || [] as option}
									<option value={option}>{option}</option>
								{/each}
							</select>
						{:else}
							<input
								type={field.type || 'text'}
								bind:value={formData[field.name]}
								disabled={submitting}
								required={field.required}
							/>
						{/if}
					</div>
				{/each}

				<button type="submit" class="btn primary" disabled={submitting}>
					{submitting ? '送信中...' : '申請を送信'}
				</button>
			</form>
		</div>
	{/if}
</div>

<style>
	.apply-page {
		min-height: 100vh;
		padding: 2rem;
		background: #f9fafb;
		display: flex;
		align-items: flex-start;
		justify-content: center;
	}

	.loading {
		text-align: center;
		padding: 3rem;
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

	.card {
		max-width: 500px;
		width: 100%;
		padding: 2rem;
		background: white;
		border-radius: 12px;
		box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
	}

	.card.success {
		text-align: center;
	}

	.card.pending {
		text-align: center;
	}

	.icon {
		font-size: 3rem;
		margin-bottom: 1rem;
	}

	h1 {
		font-size: 1.5rem;
		margin: 0 0 0.5rem;
		color: #1f2937;
	}

	.tenant-name {
		color: #6b7280;
		margin: 0 0 1.5rem;
	}

	.instructions {
		background: #f3f4f6;
		padding: 1rem;
		border-radius: 8px;
		margin-bottom: 1.5rem;
		white-space: pre-wrap;
	}

	.warning {
		background: #fef3c7;
		color: #92400e;
		padding: 0.75rem;
		border-radius: 8px;
		margin-bottom: 1rem;
	}

	.error {
		background: #fee2e2;
		color: #991b1b;
		padding: 0.75rem;
		border-radius: 8px;
		margin-bottom: 1rem;
	}

	.form-group {
		margin-bottom: 1rem;
	}

	.form-group label {
		display: block;
		font-weight: 500;
		margin-bottom: 0.5rem;
		color: #374151;
	}

	.form-group input,
	.form-group textarea,
	.form-group select {
		width: 100%;
		padding: 0.75rem;
		border: 1px solid #d1d5db;
		border-radius: 6px;
		font-size: 1rem;
		box-sizing: border-box;
	}

	.form-group input:focus,
	.form-group textarea:focus,
	.form-group select:focus {
		outline: none;
		border-color: #3b82f6;
		box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
	}

	.form-group textarea {
		min-height: 100px;
		resize: vertical;
	}

	.btn {
		display: block;
		width: 100%;
		padding: 0.875rem;
		font-size: 1rem;
		font-weight: 600;
		text-align: center;
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

	.btn:disabled {
		opacity: 0.6;
		cursor: not-allowed;
	}
</style>
