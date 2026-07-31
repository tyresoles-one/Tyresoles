import { createPersistedStore } from "./persisted";

export type LoginHistoryEntry = { 
    userId: string; 
    lastLogin: number; 
    count: number; 
};

export const loginHistoryStore = createPersistedStore<LoginHistoryEntry[]>('login-history', []);

export function recordLoginSuccess(userId: string) {
    if (!userId) return;
    
    loginHistoryStore.update(history => {
        const existingIndex = history.findIndex(h => h.userId.toLowerCase() === userId.toLowerCase());
        const now = Date.now();
        
        if (existingIndex >= 0) {
            // Update existing
            const updated = [...history];
            updated[existingIndex] = {
                ...updated[existingIndex],
                lastLogin: now,
                count: updated[existingIndex].count + 1
            };
            return updated;
        } else {
            // Add new
            return [...history, { userId, lastLogin: now, count: 1 }];
        }
    });
}
