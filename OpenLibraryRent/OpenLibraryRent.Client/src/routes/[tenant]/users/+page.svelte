<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/stores';
	import { createApiClient } from '$lib/api/client';
	import { auth } from '$lib/stores/auth';

	const api = $derived(createApiClient($page.params.tenant || ''));

	let users = $state<any[]>([]);
	let total = $state(0);
	let page_num = $state(1);
	let search = $state('');
	let loading = $state(true);
	let error = $state('');

	// モーダル
	let showRoleModal = $state(false);
	let showBanModal = $state(false);
	let selectedUser = $state<any>(null);
	let selectedRole = $state('');
	let banReason = $state('');

	// ロール一覧
	let roles = $state<any[]>([]);

	async function loadUsers() {
		loading = true;
		error = '';

		try {
			const params = new URLSearchParams();
			if (search) params.set('search', search);
			params.set('page', page_num.toString());
			params.set('pageSize', '20');

			const result = await api.get(`/users?${params}`);
			users = result.users;
			total = result.total;
		} catch (e: any) {
			error = e.message || 'ユーザーの読み込みに失敗しました';
		} finally {
			loading = false;
		}
	}

	async function loadRoles() {
		try {
			const result = await api.get('/users/roles');
			roles = result;
		} catch (e) {
			console.error('Failed to load roles', e);
		}
	}

	function handleSearch() {
		page_num = 1;
		loadUsers();
	}

	async function openRoleModal(user: any) {
		selectedUser = user;
		selectedRole = '';
		showRoleModal = true;
	}

	async function assignRole() {
		if (!selectedUser || !selectedRole) return;

		try {
			await api.post(`/users/${selectedUser.id}/roles`, { role: selectedRole });
			showRoleModal = false;
			loadUsers();
		} catch (e: any) {
			error = e.message || 'ロールの割り当てに失敗しました';
		}
	}

	async function removeRole(user: any, role: string) {
		if (!confirm(`${user.displayName || user.email} から ${role} ロールを削除しますか？`)) return;

		try {
			await api.delete(`/users/${user.id}/roles/${encodeURIComponent(role)}`);
			loadUsers();
		} catch (e: any) {
			error = e.message || 'ロールの削除に失敗しました';
		}
	}

	function openBanModal(user: any) {
		selectedUser = user;
		banReason = '';
		showBanModal = true;
	}

	async function banUser() {
		if (!selectedUser) return;

		try {
			await api.post(`/users/${selectedUser.id}/ban`, { reason: banReason });
			showBanModal = false;
			loadUsers();
		} catch (e: any) {
			error = e.message || 'BANに失敗しました';
		}
	}

	async function unbanUser(user: any) {
		if (!confirm(`${user.displayName || user.email} のBANを解除しますか？`)) return;

		try {
			await api.delete(`/users/${user.id}/ban`);
			loadUsers();
		} catch (e: any) {
			error = e.message || 'BAN解除に失敗しました';
		}
	}

	onMount(() => {
		loadUsers();
		loadRoles();
	});
</script>

<svelte:head>
	<title>ユーザー管理 - Open Library Rent</title>
</svelte:head>

<div class="users-page">
	<div class="page-header">
		<h1>ユーザー管理</h1>
	</div>

	<div class="search-box">
		<input
			type="text"
			bind:value={search}
			placeholder="名前、メールで検索..."
			onkeydown={(e) => e.key === 'Enter' && handleSearch()}
		/>
		<button onclick={handleSearch}>検索</button>
	</div>

	{#if loading}
		<p class="loading">読み込み中...</p>
	{:else if error}
		<p class="error">{error}</p>
	{:else if users.length === 0}
		<p class="empty">ユーザーが見つかりません</p>
	{:else}
		<table class="users-table">
			<thead>
				<tr>
					<th>ユーザー</th>
					<th>メール</th>
					<th>ロール</th>
					<th>貸出中</th>
					<th>状態</th>
					<th>操作</th>
				</tr>
			</thead>
			<tbody>
				{#each users as user}
					<tr class:disabled={user.isBanned}>
						<td>
							<a href="/{$page.params.tenant}/users/{user.id}">
								{user.displayName || user.userName || 'Unknown'}
							</a>
						</td>
						<td>{user.email || '-'}</td>
						<td>
							<div class="roles">
								{#each user.roles as role}
									<span class="role-badge">
										{role}
										{#if $auth.user?.roles?.includes('Admin') && role !== 'Admin'}
											<button class="remove-role" onclick={() => removeRole(user, role)}>×</button>
										{/if}
									</span>
								{/each}
								{#if $auth.user?.roles?.includes('Admin')}
									<button class="add-role-btn" onclick={() => openRoleModal(user)}>+</button>
								{/if}
							</div>
						</td>
						<td>{user.currentRentals}</td>
						<td>
							{#if user.isBanned}
								<span class="status banned">BAN</span>
							{:else}
								<span class="status active">有効</span>
							{/if}
						</td>
						<td class="actions">
							{#if $auth.user?.roles?.includes('Admin')}
								{#if user.isBanned}
									<button class="btn small" onclick={() => unbanUser(user)}>BAN解除</button>
								{:else}
									<button class="btn small danger" onclick={() => openBanModal(user)}>BAN</button>
								{/if}
							{/if}
						</td>
					</tr>
				{/each}
			</tbody>
		</table>

		{#if total > 20}
			<div class="pagination">
				<button disabled={page_num === 1} onclick={() => { page_num--; loadUsers(); }}>
					前へ
				</button>
				<span>{page_num} / {Math.ceil(total / 20)}</span>
				<button disabled={page_num * 20 >= total} onclick={() => { page_num++; loadUsers(); }}>
					次へ
				</button>
			</div>
		{/if}
	{/if}
</div>

<!-- ロール割り当てモーダル -->
{#if showRoleModal}
	<div class="modal-overlay" onclick={() => showRoleModal = false}>
		<div class="modal" onclick={(e) => e.stopPropagation()}>
			<h2>ロール割り当て</h2>
			<p>{selectedUser?.displayName || selectedUser?.email}</p>

			<div class="form-group">
				<label>ロール</label>
				<select bind:value={selectedRole}>
					<option value="">選択してください</option>
					{#each roles as role}
						<option value={role.name}>{role.name}</option>
					{/each}
				</select>
			</div>

			<div class="modal-actions">
				<button class="btn" onclick={() => showRoleModal = false}>キャンセル</button>
				<button class="btn primary" onclick={assignRole} disabled={!selectedRole}>割り当て</button>
			</div>
		</div>
	</div>
{/if}

<!-- BAN モーダル -->
{#if showBanModal}
	<div class="modal-overlay" onclick={() => showBanModal = false}>
		<div class="modal" onclick={(e) => e.stopPropagation()}>
			<h2>ユーザーBAN</h2>
			<p>{selectedUser?.displayName || selectedUser?.email}</p>

			<div class="form-group">
				<label>BAN理由</label>
				<textarea bind:value={banReason} placeholder="BAN理由を入力..."></textarea>
			</div>

			<div class="modal-actions">
				<button class="btn" onclick={() => showBanModal = false}>キャンセル</button>
				<button class="btn danger" onclick={banUser}>BANする</button>
			</div>
		</div>
	</div>
{/if}

<style>
	.users-page {
		padding: 1rem;
	}

	.page-header {
		display: flex;
		justify-content: space-between;
		align-items: center;
		margin-bottom: 1.5rem;
	}

	h1 {
		font-size: 1.5rem;
		color: #1f2937;
	}

	.search-box {
		display: flex;
		gap: 0.5rem;
		margin-bottom: 1.5rem;
	}

	.search-box input {
		flex: 1;
		padding: 0.75rem;
		border: 1px solid #d1d5db;
		border-radius: 6px;
		font-size: 1rem;
	}

	.search-box button {
		padding: 0.75rem 1.5rem;
		background: #3b82f6;
		color: white;
		border: none;
		border-radius: 6px;
		cursor: pointer;
	}

	.users-table {
		width: 100%;
		border-collapse: collapse;
		background: white;
		border-radius: 8px;
		overflow: hidden;
		box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
	}

	.users-table th,
	.users-table td {
		padding: 0.75rem 1rem;
		text-align: left;
		border-bottom: 1px solid #e5e7eb;
	}

	.users-table th {
		background: #f9fafb;
		font-weight: 600;
		color: #374151;
	}

	.users-table tr.disabled {
		opacity: 0.6;
	}

	.users-table a {
		color: #3b82f6;
		text-decoration: none;
	}

	.users-table a:hover {
		text-decoration: underline;
	}

	.roles {
		display: flex;
		flex-wrap: wrap;
		gap: 0.25rem;
		align-items: center;
	}

	.role-badge {
		display: inline-flex;
		align-items: center;
		gap: 0.25rem;
		padding: 0.25rem 0.5rem;
		background: #dbeafe;
		color: #1d4ed8;
		border-radius: 4px;
		font-size: 0.75rem;
	}

	.remove-role {
		background: none;
		border: none;
		color: #1d4ed8;
		cursor: pointer;
		padding: 0;
		font-size: 1rem;
		line-height: 1;
	}

	.remove-role:hover {
		color: #dc2626;
	}

	.add-role-btn {
		background: none;
		border: 1px dashed #d1d5db;
		border-radius: 4px;
		padding: 0.125rem 0.5rem;
		cursor: pointer;
		color: #6b7280;
		font-size: 0.875rem;
	}

	.add-role-btn:hover {
		border-color: #3b82f6;
		color: #3b82f6;
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

	.actions {
		display: flex;
		gap: 0.5rem;
	}

	.btn {
		padding: 0.5rem 1rem;
		border-radius: 6px;
		border: 1px solid #d1d5db;
		background: white;
		cursor: pointer;
		font-size: 0.875rem;
	}

	.btn.small {
		padding: 0.25rem 0.5rem;
		font-size: 0.75rem;
	}

	.btn.primary {
		background: #3b82f6;
		color: white;
		border-color: #3b82f6;
	}

	.btn.danger {
		background: #dc2626;
		color: white;
		border-color: #dc2626;
	}

	.btn:disabled {
		opacity: 0.5;
		cursor: not-allowed;
	}

	.pagination {
		display: flex;
		justify-content: center;
		align-items: center;
		gap: 1rem;
		margin-top: 2rem;
	}

	.pagination button {
		padding: 0.5rem 1rem;
		background: #e5e7eb;
		border: none;
		border-radius: 4px;
		cursor: pointer;
	}

	.pagination button:disabled {
		opacity: 0.5;
		cursor: not-allowed;
	}

	.loading, .error, .empty {
		text-align: center;
		padding: 2rem;
		color: #6b7280;
	}

	.error {
		color: #dc2626;
	}

	/* Modal styles */
	.modal-overlay {
		position: fixed;
		top: 0;
		left: 0;
		right: 0;
		bottom: 0;
		background: rgba(0, 0, 0, 0.5);
		display: flex;
		align-items: center;
		justify-content: center;
		z-index: 1000;
	}

	.modal {
		background: white;
		padding: 1.5rem;
		border-radius: 8px;
		min-width: 400px;
		max-width: 90vw;
	}

	.modal h2 {
		margin: 0 0 1rem;
		font-size: 1.25rem;
	}

	.form-group {
		margin-bottom: 1rem;
	}

	.form-group label {
		display: block;
		margin-bottom: 0.5rem;
		font-weight: 500;
	}

	.form-group select,
	.form-group textarea {
		width: 100%;
		padding: 0.5rem;
		border: 1px solid #d1d5db;
		border-radius: 4px;
		font-size: 1rem;
	}

	.form-group textarea {
		min-height: 80px;
		resize: vertical;
	}

	.modal-actions {
		display: flex;
		justify-content: flex-end;
		gap: 0.5rem;
		margin-top: 1.5rem;
	}
</style>
