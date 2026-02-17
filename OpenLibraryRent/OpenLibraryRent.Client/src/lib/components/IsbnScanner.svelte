<script lang="ts">
	import { onMount, onDestroy } from 'svelte';
	import { ReaderResult, BrowserMultiFormatReader } from '@aspect-ratio/zxing-wasm';

	export let onScan: (isbn: string) => void;
	export let onError: (error: string) => void;

	let videoElement: HTMLVideoElement;
	let isScanning = false;
	let reader: BrowserMultiFormatReader | null = null;

	onMount(async () => {
		try {
			reader = new BrowserMultiFormatReader();
		} catch (error) {
			console.error('Failed to initialize barcode reader:', error);
			onError('バーコードリーダーの初期化に失敗しました');
		}
	});

	onDestroy(() => {
		stopScanning();
	});

	async function startScanning() {
		if (!reader || isScanning) return;

		try {
			isScanning = true;
			const devices = await navigator.mediaDevices.enumerateDevices();
			const videoDevices = devices.filter(d => d.kind === 'videoinput');

			if (videoDevices.length === 0) {
				onError('カメラが見つかりません');
				isScanning = false;
				return;
			}

			// 通常は背面カメラを使用
			const backCamera = videoDevices.find(d =>
				d.label.toLowerCase().includes('back') ||
				d.label.toLowerCase().includes('rear')
			) || videoDevices[0];

			await reader.decodeFromVideoDevice(
				backCamera.deviceId,
				videoElement,
				(result: ReaderResult) => {
					if (result) {
						const text = result.getText();
						// ISBNは10桁または13桁の数字
						if (/^\d{10}$/.test(text) || /^\d{13}$/.test(text)) {
							onScan(text);
							stopScanning();
						}
					}
				}
			);
		} catch (error) {
			console.error('Failed to start scanning:', error);
			onError('カメラへのアクセスに失敗しました');
			isScanning = false;
		}
	}

	function stopScanning() {
		if (reader) {
			reader.reset();
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
</script>

<div class="scanner-container">
	<video bind:this={videoElement} class="scanner-video"></video>

	<div class="scanner-controls">
		<button on:click={toggleScanning} class="scan-button">
			{isScanning ? 'スキャン停止' : 'スキャン開始'}
		</button>
	</div>

	{#if isScanning}
		<div class="scanner-overlay">
			<div class="scanner-frame"></div>
		</div>
	{/if}
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
		border-radius: 8px;
		background: #000;
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
		bottom: 60px;
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
</style>
