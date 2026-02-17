import { browser } from '$app/environment';

export interface ApiError {
	message: string;
	errors?: Record<string, string[]>;
}

export class ApiClient {
	private tenant: string;

	constructor(tenant: string) {
		this.tenant = tenant;
	}

	private getBaseUrl(): string {
		return `/${this.tenant}/api`;
	}

	async get<T>(path: string): Promise<T> {
		const response = await fetch(`${this.getBaseUrl()}${path}`, {
			credentials: 'include'
		});

		if (!response.ok) {
			const error = await response.json().catch(() => ({ message: 'Request failed' }));
			throw error as ApiError;
		}

		return response.json();
	}

	async post<T>(path: string, body?: unknown): Promise<T> {
		const response = await fetch(`${this.getBaseUrl()}${path}`, {
			method: 'POST',
			headers: body ? { 'Content-Type': 'application/json' } : undefined,
			body: body ? JSON.stringify(body) : undefined,
			credentials: 'include'
		});

		if (!response.ok) {
			const error = await response.json().catch(() => ({ message: 'Request failed' }));
			throw error as ApiError;
		}

		return response.json();
	}

	async put<T>(path: string, body?: unknown): Promise<T> {
		const response = await fetch(`${this.getBaseUrl()}${path}`, {
			method: 'PUT',
			headers: body ? { 'Content-Type': 'application/json' } : undefined,
			body: body ? JSON.stringify(body) : undefined,
			credentials: 'include'
		});

		if (!response.ok) {
			const error = await response.json().catch(() => ({ message: 'Request failed' }));
			throw error as ApiError;
		}

		return response.json();
	}

	async delete<T>(path: string): Promise<T> {
		const response = await fetch(`${this.getBaseUrl()}${path}`, {
			method: 'DELETE',
			credentials: 'include'
		});

		if (!response.ok) {
			const error = await response.json().catch(() => ({ message: 'Request failed' }));
			throw error as ApiError;
		}

		return response.json();
	}
}

export function createApiClient(tenant: string): ApiClient {
	return new ApiClient(tenant);
}
