<script lang="ts">
	import { page } from '$app/stores';

	let tenant = $derived($page.params.tenant);

	let loading = $state(true);
	let requests = $state<any[]>([]);
	let selectedRequest = $state<any | null>(null);
	let error = $state('');

	// モーダル用
	let showModal = $state(false);
	let processing = $state(false);
	let rejectionReason = $state('');

	$effect(() => {
		loadRequests();
	});

	async function loadRequests() {
		loading = true;
		try {
			const res = await fetch(`/${tenant}/api/userapproval/requests`, {
				credentials: 'include'
			});

			if (res.ok) {
				requests = await res.json();
			} else if (res.status === 401) {
				window.location.href = `/${tenant}/auth/login?returnUrl=/${tenant}/admin/approvals`;
			}
		} catch (e) {
			console.error('Failed to load requests:', e);
		} finally {
			loading = false;
		}
	}

	function openDetail(request: any) {
		selectedRequest = request;
		showModal = true;
		rejectionReason = '';
	}

	function closeModal() {
		showModal = false;
		selectedRequest = null;
	}

	async function approve() {
		if (!selectedRequest) return;

		processing = true;
		try {
			const res = await fetch(`/${tenant}/api/userapproval/requests/${selectedRequest.id}/approve`, {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				credentials: 'include'
			});

			if (res.ok) {
				await loadRequests();
				closeModal();
			} else {
				const data = await res.json();
				error = data.message || '承認に失敗しました';
			}
		} catch (e) {
			error = '承認に失敗しました';
		} finally {
			processing = false;
		}
	}

	async function reject() {
		if (!selectedRequest) return;

		processing = true;
		try {
			const res = await fetch(`/${tenant}/api/userapproval/requests/${selectedRequest.id}/reject`, {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				credentials: 'include',
				body: JSON.stringify({ reason: rejectionReason || null })
			});

			if (res.ok) {
				await loadRequests();
				closeModal();
			} else {
				const data = await res.json();
				error = data.message || '却下に失敗しました';
			}
		} catch (e) {
			error = '却下に失敗しました';
		} finally {
			processing = false;
		}
	}

	function formatDate(dateStr: string) {
		return new Date(dateStr).toLocaleString('ja-JP');
	}

	function getStatusBadge(status: string) {
		switch (status) {
			case 'Pending': return 'badge-pending';
			case 'Approved': return 'badge-approved';
			case 'Rejected': return 'badge-rejected';
			default: return '';
		}
	}

	function getStatusText(status: string) {
		switch (status) {
			case 'Pending': return '承認待ち';
			case 'Approved': return '承認済み';
			case 'Rejected': return '却下';
			default: return status;
		}
	}
</script>

<svelte:head>
	<title>承認管理 - {tenant}</title>
</svelte:head>

<div class="approvals-page">
	<div class="header">
		<h1>承認管理</h1>
		<a href="/{tenant}" class="back-link">← ダッシュボード</a>
	</div>

	{#if error}
		<div class="error">{error}</div>
	{/if}

	{#if loading}
		<div class="loading">
			<div class="spinner"></div>
			<p>読み込み中...</p>
		</div>
	{:else if requests.length === 0}
		<div class="empty">
			<p>申請はありません</p>
		</div>
	{:else}
		<div class="table-container">
			<table>
				<thead>
					<tr>
						<th>メールアドレス</th>
						<th>表示名</th>
						<th>ステータス</th>
						<th>申請日時</th>
						<th></th>
					</tr>
				</thead>
				<tbody>
					{#each requests as request}
						<tr>
							<td>{request.email}</td>
							<td>{request.displayName || '-'}</td>
							<td>
								<span class="badge {getStatusBadge(request.status)}">
									{getStatusText(request.status)}
								</span>
							</td>
							<td>{formatDate(request.requestedAt)}</td>
							<td>
								<button class="btn-small" onclick={() => openDetail(request)}>
									詳細
								</button>
							</td>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>
	{/if}
</div>

{#if showModal && selectedRequest}
	<div class="modal-overlay" onclick={closeModal}>
		<div class="modal" onclick={(e) => e.stopPropagation()}>
			<div class="modal-header">
				<h2>申請詳細</h2>
				<button class="close-btn" onclick={closeModal}>×</button>
			</div>

			<div class="modal-body">
				<div class="detail-row">
					<label>メールアドレス</label>
					<span>{selectedRequest.email}</span>
				</div>

				<div class="detail-row">
					<label>表示名</label>
					<span>{selectedRequest.displayName || '-'}</span>
				</div>

				<div class="detail-row">
					<label>ステータス</label>
					<span class="badge {getStatusBadge(selectedRequest.status)}">
						{getStatusText(selectedRequest.status)}
					</span>
				</div>

				<div class="detail-row">
					<label>申請日時</label>
					<span>{formatDate(selectedRequest.requestedAt)}</span>
				</div>

				{#if selectedRequest.applicationData}
					<div class="detail-row">
						<label>申請データ</label>
						<pre class="json-data">
{JSON.stringify(JSON.parse(selectedRequest.applicationData), null, 2)}
						</pre>
					</div>
				{/if}

				{#if selectedRequest.rejectionReason}
					<div class="detail-row">
						<label>却下理由</label>
						<span class="rejection-reason">{selectedRequest.rejectionReason}</span>
					</div>
				{/if}

				{#if selectedRequest.status === 'Pending'}
					<div class="detail-row">
						<label>却下理由（任意）</label>
						<textarea bind:value={rejectionReason} placeholder="却下する場合は理由を入力"></textarea>
					</div>
				{/if}
			</div>

			{#if selectedRequest.status === 'Pending'}
				<div class="modal-footer">
					<button class="btn reject" onclick={reject} disabled={processing}>
						{processing ? '処理中...' : '却下'}
					</button>
					<button class="btn approve" onclick={approve} disabled={processing}>
						{processing ? '処理中...' : '承認'}
					</button>
				</div>
			{/if}
		</div>
	</div>
{/if}

<style>
	.approvals-page {
		padding: 2rem;
		max-width: 1000px;
		margin: 0 auto;
	}

	.header {
		display: flex;
		justify-content: space-between;
		align-items: center;
		margin-bottom: 2rem;
	}

	h1 {
		font-size: 1.5rem;
		margin: 0;
		color: #1f2937;
	}

	.back-link {
		color: #6b7280;
		text-decoration: none;
	}

	.back-link:hover {
		color: #3b82f6;
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

	.empty {
		text-align: center;
		padding: 3rem;
		color: #6b7280;
	}

	.error {
		background: #fee2e2;
		color: #991b1b;
		padding: 0.75rem;
		border-radius: 8px;
		margin-bottom: 1rem;
	}

	.table-container {
		background: white;
		border-radius: 8px;
		box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
		overflow: hidden;
	}

	table {
		width: 100%;
		border-collapse: collapse;
	}

	th, td {
		padding: 0.875rem 1rem;
		text-align: left;
		border-bottom: 1px solid #e5e7eb;
	}

	th {
		background: #f9fafb;
		font-weight: 600;
		color: #374151;
	}

	tr:last-child td {
		border-bottom: none;
	}

	.badge {
		display: inline-block;
		padding: 0.25rem 0.75rem;
		border-radius: 9999px;
		font-size: 0.75rem;
		font-weight: 500;
	}

	.badge-pending {
		background: #fef3c7;
		color: #92400e;
	}

	.badge-approved {
		background: #dcfce7;
		color: #166534;
	}

	.badge-rejected {
		background: #fee2e2;
		color: #991b1b;
	}

	.btn-small {
		padding: 0.375rem 0.75rem;
		font-size: 0.875rem;
		background: #f3f4f6;
		border: 1px solid #d1d5db;
		border-radius: 6px;
		cursor: pointer;
	}

	.btn-small:hover {
		background: #e5e7eb;
	}

	/* Modal */
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
		border-radius: 12px;
		width: 90%;
		max-width: 500px;
		max-height: 90vh;
		overflow-y: auto;
	}

	.modal-header {
		display: flex;
		justify-content: space-between;
		align-items: center;
		padding: 1rem 1.5rem;
		border-bottom: 1px solid #e5e7eb;
	}

	.modal-header h2 {
		margin: 0;
		font-size: 1.125rem;
	}

	.close-btn {
		background: none;
		border: none;
		font-size: 1.5rem;
		cursor: pointer;
		color: #6b7280;
	}

	.modal-body {
		padding: 1.5rem;
	}

	.detail-row {
		margin-bottom: 1rem;
	}

	.detail-row label {
		display: block;
		font-size: 0.75rem;
		font-weight: 500;
		color: #6b7280;
		margin-bottom: 0.25rem;
	}

	.detail-row span {
		color: #1f2937;
	}

	.json-data {
		background: #f3f4f6;
		padding: 0.75rem;
		border-radius: 6px;
		font-size: 0.75rem;
		overflow-x: auto;
		margin: 0;
	}

	.rejection-reason {
		color: #991b1b;
	}

	.detail-row textarea {
		width: 100%;
		padding: 0.5rem;
		border: 1px solid #d1d5db;
		border-radius: 6px;
		font-size: 0.875rem;
		box-sizing: border-box;
		min-height: 80px;
	}

	.modal-footer {
		display: flex;
		justify-content: flex-end;
		gap: 0.75rem;
		padding: 1rem 1.5rem;
		border-top: 1px solid #e5e7eb;
	}

	.btn {
		padding: 0.5rem 1rem;
		font-size: 0.875rem;
		font-weight: 500;
		border: none;
		border-radius: 6px;
		cursor: pointer;
	}

	.btn.approve {
		background: #3b82f6;
		color: white;
	}

	.btn.approve:hover:not(:disabled) {
		background: #2563eb;
	}

	.btn.reject {
		background: #ef4444;
		color: white;
	}

	.btn.reject:hover:not(:disabled) {
		background: #dc2626;
	}

	.btn:disabled {
		opacity: 0.6;
		cursor: not-allowed;
	}
</style>
