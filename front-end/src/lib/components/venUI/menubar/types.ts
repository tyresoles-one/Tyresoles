export type MenuItem = {
    id: string;
    label: string;
    icon?: string;
    shortcut?: string;
    disabled?: boolean;
    onClick?: () => void;
    href?: string;
    /** Optional JSON or string from AccessControl.Values (login menu item options). */
    options?: string;
    children?: MenuItem[];
    separator?: boolean;
    checked?: boolean; // For checkbox/radio items
    type?: 'item' | 'checkbox' | 'radio' | 'separator' | 'label'; // label not standard in menubar but useful
};

export type MenubarProps = {
    menus: MenuItem[];
    moreLabel?: string;
    variant?: 'default' | 'premium';
    class?: string;
};
