export type DropdownTrigger = {
	label: string;
	icon?: string; // For the trailing icon (e.g. ChevronDown) or main icon if iconOnly is true
	showChevron?: boolean; // Defaults to true
	variant?: 'default' | 'destructive' | 'outline' | 'secondary' | 'ghost' | 'link';
	size?: 'default' | 'sm' | 'lg' | 'icon';
	class?: string;
	disabled?: boolean;
	iconOnly?: boolean; // New property
};

export type DropdownItem =
	| {
			type: 'item';
			label: string;
			icon?: string;
			shortcut?: string;
			disabled?: boolean;
			onClick?: () => void;
			class?: string;
			variant?: 'default' | 'destructive'; // For styling like red text
	  }
	| { type: 'separator' }
	| { type: 'label'; label: string }
	| {
			type: 'checkbox';
			label: string;
			checked: boolean;
			onCheckedChange: (checked: boolean) => void;
			disabled?: boolean;
	  }
	| {
			type: 'radio-group';
			value: string;
			onValueChange: (value: string) => void;
			options: { label: string; value: string; disabled?: boolean }[];
	  }
	| { type: 'sub'; label: string; icon?: string; children: DropdownItem[] }
	| { type: 'group'; children: DropdownItem[] }
	| { type: 'custom'; render: any };

export type DropdownProps = {
	trigger?: DropdownTrigger;
	items: DropdownItem[];
	align?: 'start' | 'center' | 'end';
	class?: string;
    sideOffset?: number;
};
