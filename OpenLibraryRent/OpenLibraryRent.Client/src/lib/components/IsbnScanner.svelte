<script lang="ts">
	import { onMount } from 'svelte';

	let onScan: (isbn: string) => void = () => {};
	let onError: (error: string) => void = () => {};

	let videoElement: HTMLVideoElement;
	let isScanning = $state(false);
	let manualIsbn = $state('');

	async function startScanning() {
		if (isScanning) return;

		try {
			const stream = await navigator.mediaDevices.getUserMedia({
				video: { facingMode: 'environment' }
			});
			videoElement.srcObject = stream;
			await videoElement.play();
			isScanning = true;
		} catch (error) {
			console.error('Failed to start scanning:', error);
			onError('カメラへのアクセスに失敗しました');
		}
	}

	function stopScanning() {
		if (videoElement.srcObject) {
			const stream = videoElement.srcObject as MediaStream;
			stream.getTracks().forEach(track => track.stop());
			videoElement.srcObject = null;
		}
		isScanning = false;
	}

	function toggleScanning() {
		if (isScanning) {
			stopScanning();
		} else {
			startScanning();
		}
	}

	function submitManual() {
		const isbn = manualIsbn.trim();
		if (/^\d{10}$/.test(isbn) || /^\d{13}$/.test(isbn)) {
			onScan(isbn);
			manualIsbn = '';
		} else {
			onError('ISBNは10桁または13桁の数字で入力してください');
		}
	}

	onMount(() => {
		return () => stopScanning();
	});
</script>

<div class="scanner-container">
	<video bind:this={videoElement} class="scanner-video" autoplay playsinline></video>

	<div class="scanner-controls">
		<button onclick={toggleScanning} class="scan-button">
			{isScanning ? 'スキャン停止' : 'スキャン開始'}
		</button>
	</div>

	{#if isScanning}
		<div class="scanner-overlay">
			<div class="scanner-frame"></div>
		</div>
	{/if}

	<div class="manual-input">
		<p>または手動でISBNを入力:</p>
		<div class="input-group">
			<input
				type="text"
				bind:value={manualIsbn}
				placeholder="ISBN (10桁または13桁)"
				onkeydown={(e) => e.key === 'Enter' && submitManual()}
			/>
			<button onclick={submitManual} class="submit-button">入力</button>
		</div>
	</div>
</div>

<style>
	.scanner-container {
		position: relative;
		width: 100%;
		max-width: 400px;
		margin: 0 auto;
	}

	.scanner-video {
		width: 100%;
		height: 300px;
		border-radius: 8px;
		background: #000;
		object-fit: cover;
	}

	.scanner-controls {
		margin-top: 1rem;
		text-align: center;
	}

	.scan-button {
		padding: 0.75rem 2rem;
		font-size: 1rem;
		background: #3b82f6;
		color: white;
		border: none;
		border-radius: 8px;
		cursor: pointer;
		transition: background 0.2s;
	}

	.scan-button:hover {
		background: #2563eb;
	}

	.scanner-overlay {
		position: absolute;
		top: 0;
		left: 0;
		right: 0;
		height: 300px;
		display: flex;
		align-items: center;
		justify-content: center;
		pointer-events: none;
	}

	.scanner-frame {
		width: 80%;
		height: 40%;
		border: 2px solid #3b82f6;
		border-radius: 8px;
		box-shadow: 0 0 0 9999px rgba(0, 0, 0, 0.3);
	}

	.manual-input {
		margin-top: 1.5rem;
		padding: 1rem;
		background: #f3f4f6;
		border-radius: 8px;
	}

	.manual-input p {
		margin: 0 0 0.5rem;
		font-size: 0.875rem;
		color: #6b7280;
	}

	.input-group {
		display: flex;
		gap: 0.5rem;
	}

	.input-group input {
		flex: 1;
		padding: 0.5rem;
		border: 1px solid #d1d5db;
		border-radius: 4px;
		font-size: 1rem;
	}

	.submit-button {
		padding: 0.5rem 1rem;
		background: #10b981;
		color: white;
		border: none;
		border-radius: 4px;
		cursor: pointer;
	}
</style>
