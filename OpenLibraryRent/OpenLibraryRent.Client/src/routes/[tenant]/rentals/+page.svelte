<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/stores';
	import { createApiClient } from '$lib/api/client';
	import { auth } from '$lib/stores/auth';

	const api = $derived(createApiClient($page.params.tenant || ''));

	let rentals = $state<any[]>([]);
	let loading = $state(true);
	let error = $state('');

	async function loadRentals() {
		loading = true;
		error = '';

		try {
			rentals = await api.get('/rentals/my');
		} catch (e: any) {
			error = e.message || '貸出情報の読み込みに失敗しました';
		} finally {
			loading = false;
		}
	}

	async function returnBook(rentalId: string) {
		if (!confirm('この書籍を返却しますか？')) return;

		try {
			await api.post(`/rentals/my/${rentalId}/return`);
			await loadRentals();
		} catch (e: any) {
			alert(e.message || '返却に失敗しました');
		}
	}

	function formatDate(date: string) {
		return new Date(date).toLocaleDateString('ja-JP');
	}

	function isOverdue(dueDate: string) {
		return new Date(dueDate) < new Date();
	}

	onMount(() => {
		loadRentals();
	});
</script>

<svelte:head>
	<title>マイ貸出 - Open Library Rent</title>
</svelte:head>

<div class="rentals-page">
	<h1>マイ貸出</h1>

	{#if loading}
		<p class="loading">読み込み中...</p>
	{:else if error}
		<p class="error">{error}</p>
	{:else if rentals.length === 0}
		<p class="empty">現在借りている書籍はありません</p>
	{:else}
		<div class="rentals-list">
			{#each rentals as rental}
				<div class="rental-card" class:overdue={isOverdue(rental.dueDate)}>
					{#if rental.book.coverImageUrl}
						<img src={rental.book.coverImageUrl} alt={rental.book.title} class="cover" />
					{:else}
						<div class="cover placeholder">No Image</div>
					{/if}

					<div class="info">
						<h3>{rental.book.title}</h3>
						<p class="isbn">ISBN: {rental.book.isbn}</p>
						<p class="dates">
							借りた日: {formatDate(rental.borrowedAt)}<br>
							返却期限: {formatDate(rental.dueDate)}
							{#if rental.overdueDays > 0}
								<span class="overdue-badge">（{rental.overdueDays}日延滞）</span>
							{/if}
						</p>
					</div>

					<div class="actions">
						<button onclick={() => returnBook(rental.id)} class="btn return">
							返却
						</button>
					</div>
				</div>
			{/each}
		</div>
	{/if}
</div>

<style>
	.rentals-page {
		max-width: 800px;
		margin: 0 auto;
	}

	h1 {
		margin-bottom: 1.5rem;
	}

	.rentals-list {
		display: flex;
		flex-direction: column;
		gap: 1rem;
	}

	.rental-card {
		display: flex;
		gap: 1rem;
		background: white;
		padding: 1rem;
		border-radius: 8px;
		box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
	}

	.rental-card.overdue {
		border-left: 4px solid #dc2626;
	}

	.cover {
		width: 80px;
		height: 120px;
		object-fit: cover;
		border-radius: 4px;
		flex-shrink: 0;
	}

	.cover.placeholder {
		display: flex;
		align-items: center;
		justify-content: center;
		background: #f3f4f6;
		color: #9ca3af;
		font-size: 0.75rem;
	}

	.info {
		flex: 1;
	}

	.info h3 {
		margin: 0 0 0.5rem;
		font-size: 1.1rem;
	}

	.isbn {
		color: #6b7280;
		font-size: 0.875rem;
		margin: 0.25rem 0;
	}

	.dates {
		color: #4b5563;
		font-size: 0.875rem;
		margin: 0.5rem 0 0;
	}

	.overdue-badge {
		color: #dc2626;
		font-weight: 500;
	}

	.actions {
		display: flex;
		align-items: center;
	}

	.btn {
		padding: 0.5rem 1rem;
		border-radius: 6px;
		border: none;
		cursor: pointer;
		font-weight: 500;
	}

	.btn.return {
		background: #3b82f6;
		color: white;
	}

	.btn.return:hover {
		background: #2563eb;
	}

	.loading, .error, .empty {
		text-align: center;
		padding: 2rem;
		color: #6b7280;
	}

	.error {
		color: #dc2626;
	}
</style>
