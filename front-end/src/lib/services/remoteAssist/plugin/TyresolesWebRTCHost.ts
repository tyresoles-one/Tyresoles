import { registerPlugin } from '@capacitor/core';
import type { PluginListenerHandle } from '@capacitor/core';

/**
 * Native Plugin interface for bridging WebRTC screen broadcasting
 * to iOS ReplayKit and Android MediaProjection.
 */
export interface TyresolesWebRTCHostPlugin {
	/**
	 * Initializes the native WebRTC peer connection independently of the WebView.
	 */
	initializeSession(options: {
		token: string;
		sessionId: string;
		iceServers: { urls: string; username?: string; credential?: string }[];
	}): Promise<{ success: boolean }>;

	/**
	 * Requests native permissions and summons the OS-level screen broadcasting service.
	 * Android: Fires MediaProjection intent popup.
	 * iOS: Requests ReplayKit / Broadcast Upload Extension hook.
	 */
	startBroadcast(): Promise<{ success: boolean; error?: string }>;

	/**
	 * Gracefully tears down the native peer connection and stops capturing.
	 */
	stopBroadcast(): Promise<void>;

	/**
	 * Listeners for native OS events mapping back to the JS UI.
	 */
	addListener(
		eventName: 'onConnectionStateChange',
		listenerFunc: (state: { state: string }) => void
	): Promise<PluginListenerHandle>;

	addListener(
		eventName: 'onError',
		listenerFunc: (error: { message: string }) => void
	): Promise<PluginListenerHandle>;

	addListener(
		eventName: 'onBroadcastStopped',
		listenerFunc: () => void
	): Promise<PluginListenerHandle>;
}

// Exports the global singleton bound to native layer
export const TyresolesWebRTCHost = registerPlugin<TyresolesWebRTCHostPlugin>('TyresolesWebRTCHost');
