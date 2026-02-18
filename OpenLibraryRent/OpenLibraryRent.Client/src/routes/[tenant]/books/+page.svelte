<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/stores';
	import { createApiClient } from '$lib/api/client';
	import { auth } from '$lib/stores/auth';

	const api = $derived(createApiClient($page.params.tenant || ''));

	let books = $state<any[]>([]);
	let total = $state(0);
	let page_num = $state(1);
	let search = $state('');
	let loading = $state(true);
	let error = $state('');

	async function loadBooks() {
		loading = true;
		error = '';

		try {
			const params = new URLSearchParams();
			if (search) params.set('search', search);
			params.set('page', page_num.toString());
			params.set('pageSize', '20');

			const result = await api.get<{ books: any[]; total: number }>(`/books?${params}`);
			books = result.books;
			total = result.total;
		} catch (e: any) {
			error = e.message || '書籍の読み込みに失敗しました';
		} finally {
			loading = false;
		}
	}

	function handleSearch() {
		page_num = 1;
		loadBooks();
	}

	onMount(() => {
		loadBooks();
	});
</script>

<svelte:head>
	<title>書籍一覧 - Open Library Rent</title>
</svelte:head>

<div class="books-page">
	<div class="page-header">
		<h1>書籍一覧</h1>
		{#if $auth.user?.roles?.includes('Admin') || $auth.user?.roles?.includes('Librarian')}
			<a href="/{$page.params.tenant}/books/add" class="btn primary">書籍追加</a>
		{/if}
	</div>

	<div class="search-box">
		<input
			type="text"
			bind:value={search}
			placeholder="ISBN、タイトル、著者で検索..."
			onkeydown={(e) => e.key === 'Enter' && handleSearch()}
		/>
		<button onclick={handleSearch}>検索</button>
	</div>

	{#if loading}
		<p class="loading">読み込み中...</p>
	{:else if error}
		<p class="error">{error}</p>
	{:else if books.length === 0}
		<p class="empty">書籍が見つかりません</p>
	{:else}
		<div class="books-grid">
			{#each books as book}
				<a href="/{$page.params.tenant}/books/{book.id}" class="book-card">
					{#if book.coverImageUrl}
						<img src={book.coverImageUrl} alt={book.title} class="cover" />
					{:else}
						<div class="cover placeholder">No Image</div>
					{/if}
					<div class="info">
						<h3>{book.title}</h3>
						<p class="authors">{book.authors || '著者不明'}</p>
						<p class="isbn">ISBN: {book.isbn}</p>
						<p class="availability">
							{book.availableCopies > 0
								? `利用可能: ${book.availableCopies} / ${book.totalCopies}冊`
								: '貸出不可'}
						</p>
					</div>
				</a>
			{/each}
		</div>

		{#if total > 20}
			<div class="pagination">
				<button disabled={page_num === 1} onclick={() => { page_num--; loadBooks(); }}>
					前へ
				</button>
				<span>{page_num} / {Math.ceil(total / 20)}</span>
				<button disabled={page_num * 20 >= total} onclick={() => { page_num++; loadBooks(); }}>
					次へ
				</button>
			</div>
		{/if}
	{/if}
</div>

<style>
	.books-page {
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

	.btn {
		padding: 0.5rem 1rem;
		border-radius: 6px;
		text-decoration: none;
		font-weight: 500;
	}

	.btn.primary {
		background: #3b82f6;
		color: white;
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

	.books-grid {
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
		gap: 1.5rem;
	}

	.book-card {
		display: flex;
		flex-direction: column;
		background: white;
		border-radius: 8px;
		overflow: hidden;
		box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
		text-decoration: none;
		color: inherit;
		transition: transform 0.2s, box-shadow 0.2s;
	}

	.book-card:hover {
		transform: translateY(-2px);
		box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
	}

	.cover {
		width: 100%;
		height: 200px;
		object-fit: cover;
	}

	.cover.placeholder {
		display: flex;
		align-items: center;
		justify-content: center;
		background: #f3f4f6;
		color: #9ca3af;
	}

	.info {
		padding: 1rem;
	}

	.info h3 {
		font-size: 1rem;
		margin: 0 0 0.5rem;
		white-space: nowrap;
		overflow: hidden;
		text-overflow: ellipsis;
	}

	.authors, .isbn {
		font-size: 0.875rem;
		color: #6b7280;
		margin: 0.25rem 0;
	}

	.availability {
		font-size: 0.875rem;
		color: #059669;
		margin-top: 0.5rem;
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
</style>
