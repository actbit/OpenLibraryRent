<script lang="ts">
	import { page } from '$app/stores';
	import IsbnScanner from '$lib/components/IsbnScanner.svelte';
	import { createApiClient } from '$lib/api/client';

	const api = $derived(createApiClient($page.params.tenant || ''));

	let scanResult = $state<{ isbn: string; book?: any; error?: string } | null>(null);
	let searchIsbn = $state('');
	let loading = $state(false);

	async function handleScan(isbn: string) {
		searchIsbn = isbn;
		await searchBook(isbn);
	}

	async function handleScanError(error: string) {
		scanResult = { isbn: '', error };
	}

	async function searchBook(isbn: string) {
		if (!isbn) return;

		loading = true;
		scanResult = null;

		try {
			// まずOpen Libraryから検索
			const bookData = await api.get(`/books/fetch-from-openlibrary/${isbn}`);
			scanResult = { isbn, book: bookData };
		} catch (e: any) {
			if (e.message?.includes('not found')) {
				scanResult = { isbn, error: '書籍が見つかりませんでした' };
			} else {
				scanResult = { isbn, error: e.message || '検索に失敗しました' };
			}
		} finally {
			loading = false;
		}
	}

	function handleManualSearch() {
		if (searchIsbn) {
			searchBook(searchIsbn);
		}
	}

	async function registerBook() {
		if (!scanResult?.isbn) return;

		loading = true;
		try {
			await api.post(`/books/register-from-openlibrary/${scanResult.isbn}`);
			alert('書籍を登録しました');
			scanResult = null;
		} catch (e: any) {
			alert(e.message || '登録に失敗しました');
		} finally {
			loading = false;
		}
	}
</script>

<svelte:head>
	<title>ISBNスキャン - Open Library Rent</title>
</svelte:head>

<div class="scan-page">
	<h1>ISBNスキャン</h1>

	<div class="scanner-section">
		<IsbnScanner onScan={handleScan} onError={handleScanError} />
	</div>

	<div class="manual-search">
		<h2>または ISBN を直接入力</h2>
		<div class="search-input">
			<input
				type="text"
				bind:value={searchIsbn}
				placeholder="ISBN (10桁または13桁)"
			/>
			<button onclick={handleManualSearch} disabled={loading}>
				検索
			</button>
		</div>
	</div>

	{#if loading}
		<p class="loading">読み込み中...</p>
	{/if}

	{#if scanResult}
		<div class="result">
			{#if scanResult.error}
				<p class="error">{scanResult.error}</p>
				<p>ISBN: {scanResult.isbn}</p>
			{:else if scanResult.book}
				<div class="book-info">
					{#if scanResult.book.coverImageUrl}
						<img src={scanResult.book.coverImageUrl} alt={scanResult.book.title} />
					{/if}
					<h3>{scanResult.book.title}</h3>
					<p><strong>著者:</strong> {scanResult.book.authors || '不明'}</p>
					<p><strong>出版社:</strong> {scanResult.book.publisher || '不明'}</p>
					<p><strong>出版年:</strong> {scanResult.book.publishYear || '不明'}</p>
					<p><strong>ISBN:</strong> {scanResult.book.isbn}</p>

					<div class="actions">
						<button onclick={registerBook} class="btn primary" disabled={loading}>
							この書籍を登録
						</button>
					</div>
				</div>
			{/if}
		</div>
	{/if}
</div>

<style>
	.scan-page {
		max-width: 600px;
		margin: 0 auto;
	}

	h1 {
		text-align: center;
		margin-bottom: 2rem;
	}

	.scanner-section {
		margin-bottom: 2rem;
	}

	.manual-search {
		text-align: center;
		margin-bottom: 2rem;
	}

	.manual-search h2 {
		font-size: 1rem;
		color: #6b7280;
		margin-bottom: 1rem;
	}

	.search-input {
		display: flex;
		gap: 0.5rem;
		justify-content: center;
	}

	.search-input input {
		padding: 0.75rem;
		border: 1px solid #d1d5db;
		border-radius: 6px;
		font-size: 1rem;
		width: 200px;
	}

	.search-input button {
		padding: 0.75rem 1.5rem;
		background: #3b82f6;
		color: white;
		border: none;
		border-radius: 6px;
		cursor: pointer;
	}

	.search-input button:disabled {
		opacity: 0.5;
	}

	.result {
		background: white;
		padding: 1.5rem;
		border-radius: 8px;
		box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
	}

	.book-info {
		text-align: center;
	}

	.book-info img {
		max-width: 150px;
		margin-bottom: 1rem;
	}

	.book-info h3 {
		margin: 0 0 1rem;
	}

	.book-info p {
		margin: 0.5rem 0;
		color: #4b5563;
	}

	.actions {
		margin-top: 1.5rem;
	}

	.btn {
		padding: 0.75rem 1.5rem;
		border-radius: 6px;
		font-weight: 500;
		cursor: pointer;
		border: none;
	}

	.btn.primary {
		background: #3b82f6;
		color: white;
	}

	.btn:disabled {
		opacity: 0.5;
		cursor: not-allowed;
	}

	.loading {
		text-align: center;
		color: #6b7280;
	}

	.error {
		color: #dc2626;
		text-align: center;
	}
</style>
