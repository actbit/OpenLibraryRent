import { writable } from 'svelte/store';
import { browser } from '$app/environment';

export interface User {
	userId: string;
	email?: string;
	name?: string;
	tenant?: string;
	roles: string[];
	isAuthenticated: boolean;
}

export interface AuthState {
	user: User | null;
	isLoading: boolean;
}

function createAuthStore() {
	const { subscribe, set, update } = writable<AuthState>({
		user: null,
		isLoading: true
	});

	return {
		subscribe,
		async checkAuth(tenant: string) {
			if (!browser) return;

			try {
				const response = await fetch(`/${tenant}/auth/me`, {
					credentials: 'include'
				});

				if (response.ok) {
					const user = await response.json();
					set({ user, isLoading: false });
				} else {
					set({ user: null, isLoading: false });
				}
			} catch (error) {
				console.error('Auth check failed:', error);
				set({ user: null, isLoading: false });
			}
		},
		async logout(tenant: string) {
			if (!browser) return;

			try {
				await fetch(`/${tenant}/auth/logout`, {
					method: 'POST',
					credentials: 'include'
				});
				set({ user: null, isLoading: false });
			} catch (error) {
				console.error('Logout failed:', error);
			}
		},
		setLoading(loading: boolean) {
			update(state => ({ ...state, isLoading: loading }));
		},
		reset() {
			set({ user: null, isLoading: false });
		}
	};
}

export const auth = createAuthStore();
