<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/stores';
	import { createApiClient } from '$lib/api/client';
	import { auth } from '$lib/stores/auth';

	const api = $derived(createApiClient($page.params.tenant || ''));

	let user = $state<any>(null);
	let loading = $state(true);
	let error = $state('');

	// 編集モード
	let editing = $state(false);
	let displayName = $state('');

	async function loadUser() {
		loading = true;
		error = '';

		try {
			user = await api.get(`/users/${$page.params.id}`);
			displayName = user.displayName || '';
		} catch (e: any) {
			error = e.message || 'ユーザー情報の読み込みに失敗しました';
		} finally {
			loading = false;
		}
	}

	async function saveUser() {
		try {
			await api.put(`/users/${user.id}`, { displayName });
			user.displayName = displayName;
			editing = false;
		} catch (e: any) {
			error = e.message || '更新に失敗しました';
		}
	}

	function cancelEdit() {
		displayName = user.displayName || '';
		editing = false;
	}

	onMount(() => {
		loadUser();
	});
</script>

<svelte:head>
	<title>{user?.displayName || 'ユーザー詳細'} - Open Library Rent</title>
</svelte:head>

<div class="user-detail">
	{#if loading}
		<p class="loading">読み込み中...</p>
	{:else if error}
		<p class="error">{error}</p>
	{:else if user}
		<div class="header">
			<a href="/{$page.params.tenant}/users" class="back-link">← ユーザー一覧</a>
			<h1>{user.displayName || user.email || 'Unknown User'}</h1>
			{#if user.isBanned}
				<span class="status banned">BAN中</span>
			{/if}
		</div>

		<div class="content">
			<section class="card">
				<h2>基本情報</h2>

				{#if editing}
					<div class="form-group">
						<label>表示名</label>
						<input type="text" bind:value={displayName} />
					</div>
					<div class="form-group">
						<label>メール</label>
						<input type="text" value={user.email || '-'} disabled />
					</div>
					<div class="actions">
						<button class="btn" onclick={cancelEdit}>キャンセル</button>
						<button class="btn primary" onclick={saveUser}>保存</button>
					</div>
				{:else}
					<dl class="info-list">
						<dt>表示名</dt>
						<dd>{user.displayName || '-'}</dd>

						<dt>メール</dt>
						<dd>{user.email || '-'}</dd>

						<dt>登録日</dt>
						<dd>{new Date(user.createdAt).toLocaleDateString('ja-JP')}</dd>

						<dt>ロール</dt>
						<dd>
							{#each user.roles as role}
								<span class="role-badge">{role}</span>
							{/each}
						</dd>

						{#if user.isBanned}
							<dt>BAN理由</dt>
							<dd class="ban-reason">{user.banReason || '理由なし'}</dd>
						{/if}
					</dl>
					{#if $auth.user?.userId === user.id || $auth.user?.roles?.includes('Admin')}
						<button class="btn" onclick={() => editing = true}>編集</button>
					{/if}
				{/if}
			</section>

			<section class="card">
				<h2>統計</h2>
				<div class="stats">
					<div class="stat">
						<span class="stat-value">{user.currentRentals?.length || 0}</span>
						<span class="stat-label">現在の貸出</span>
					</div>
					<div class="stat">
						<span class="stat-value">{user.totalRentals || 0}</span>
						<span class="stat-label">総貸出数</span>
					</div>
				</div>
			</section>

			{#if user.currentRentals && user.currentRentals.length > 0}
				<section class="card">
					<h2>現在の貸出</h2>
					<table class="rentals-table">
						<thead>
							<tr>
								<th>書籍</th>
								<th>借出日</th>
								<th>返却期限</th>
								<th>状態</th>
							</tr>
						</thead>
						<tbody>
							{#each user.currentRentals as rental}
								<tr>
									<td>
										<a href="/{$page.params.tenant}/books/{rental.book.id}">
											{rental.book.title}
										</a>
									</td>
									<td>{new Date(rental.borrowedAt).toLocaleDateString('ja-JP')}</td>
									<td>{new Date(rental.dueDate).toLocaleDateString('ja-JP')}</td>
									<td>
										{#if rental.status === 'Overdue'}
											<span class="status overdue">延滞</span>
										{:else}
											<span class="status active">貸出中</span>
										{/if}
									</td>
								</tr>
							{/each}
						</tbody>
					</table>
				</section>
			{/if}
		</div>
	{/if}
</div>

<style>
	.user-detail {
		padding: 1rem;
	}

	.header {
		margin-bottom: 1.5rem;
	}

	.back-link {
		color: #3b82f6;
		text-decoration: none;
		font-size: 0.875rem;
	}

	.back-link:hover {
		text-decoration: underline;
	}

	h1 {
		font-size: 1.5rem;
		color: #1f2937;
		margin: 0.5rem 0;
		display: inline-flex;
		align-items: center;
		gap: 0.75rem;
	}

	.content {
		display: grid;
		gap: 1.5rem;
	}

	.card {
		background: white;
		border-radius: 8px;
		padding: 1.5rem;
		box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
	}

	.card h2 {
		font-size: 1.125rem;
		margin: 0 0 1rem;
		color: #374151;
	}

	.info-list {
		display: grid;
		gap: 0.75rem;
	}

	.info-list dt {
		font-size: 0.875rem;
		color: #6b7280;
	}

	.info-list dd {
		margin: 0;
		font-size: 1rem;
		color: #1f2937;
	}

	.form-group {
		margin-bottom: 1rem;
	}

	.form-group label {
		display: block;
		font-size: 0.875rem;
		color: #6b7280;
		margin-bottom: 0.25rem;
	}

	.form-group input {
		width: 100%;
		padding: 0.5rem;
		border: 1px solid #d1d5db;
		border-radius: 4px;
		font-size: 1rem;
	}

	.form-group input:disabled {
		background: #f3f4f6;
	}

	.role-badge {
		display: inline-block;
		padding: 0.25rem 0.5rem;
		background: #dbeafe;
		color: #1d4ed8;
		border-radius: 4px;
		font-size: 0.75rem;
		margin-right: 0.25rem;
	}

	.status {
		padding: 0.25rem 0.5rem;
		border-radius: 4px;
		font-size: 0.75rem;
		font-weight: 500;
	}

	.status.active {
		background: #dcfce7;
		color: #166534;
	}

	.status.banned {
		background: #fee2e2;
		color: #991b1b;
	}

	.status.overdue {
		background: #fef3c7;
		color: #92400e;
	}

	.ban-reason {
		color: #dc2626;
	}

	.actions {
		display: flex;
		gap: 0.5rem;
		margin-top: 1rem;
	}

	.btn {
		padding: 0.5rem 1rem;
		border-radius: 6px;
		border: 1px solid #d1d5db;
		background: white;
		cursor: pointer;
		font-size: 0.875rem;
	}

	.btn.primary {
		background: #3b82f6;
		color: white;
		border-color: #3b82f6;
	}

	.stats {
		display: flex;
		gap: 2rem;
	}

	.stat {
		text-align: center;
	}

	.stat-value {
		display: block;
		font-size: 2rem;
		font-weight: bold;
		color: #3b82f6;
	}

	.stat-label {
		font-size: 0.875rem;
		color: #6b7280;
	}

	.rentals-table {
		width: 100%;
		border-collapse: collapse;
	}

	.rentals-table th,
	.rentals-table td {
		padding: 0.75rem;
		text-align: left;
		border-bottom: 1px solid #e5e7eb;
	}

	.rentals-table th {
		font-size: 0.875rem;
		color: #6b7280;
		font-weight: 500;
	}

	.rentals-table a {
		color: #3b82f6;
		text-decoration: none;
	}

	.rentals-table a:hover {
		text-decoration: underline;
	}

	.loading, .error {
		text-align: center;
		padding: 2rem;
		color: #6b7280;
	}

	.error {
		color: #dc2626;
	}
</style>
