export type ServiceState =
	| 'Running'
	| 'Stopped'
	| 'StartPending'
	| 'StopPending'
	| 'PausePending'
	| 'Paused'
	| 'ContinuePending'
	| 'Unknown';

export interface ServiceStatus {
	/** Windows short service name */
	name: string;
	/** Human-readable display name from the SCM */
	displayName: string;
	/** Current state string */
	state: ServiceState;
	/** Convenience boolean */
	isRunning: boolean;
	/** Whether the UI should show a "Start" button */
	canStart: boolean;
	/** Whether the UI should show a "Stop" button */
	canStop: boolean;
}

export interface ServiceDescriptor {
	name: string;
	canStart?: boolean;
	canStop?: boolean;
}
