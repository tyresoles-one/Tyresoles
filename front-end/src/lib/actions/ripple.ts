/**
 * Creates a ripple effect on click.
 * usage: <button use:ripple>Click me</button>
 */
export function ripple(node: HTMLElement, params: { color?: string } = {}) {
    // Ensure the node has relative positioning so the ripple is contained
    const existingPos = getComputedStyle(node).position;
    if (existingPos === 'static') {
        node.style.position = 'relative';
    }
    // overflow-hidden is crucial for internal ripple
    node.style.overflow = 'hidden';

    function handleClick(e: MouseEvent) {
        if (node.hasAttribute('disabled') || node.classList.contains('disabled')) return;

        const rect = node.getBoundingClientRect();
        
        // Create circle
        const circle = document.createElement('span');
        
        // Calculate size
        const diameter = Math.max(rect.width, rect.height);
        const radius = diameter / 2;
        
        // Calculate position relative to the element
        const x = e.clientX - rect.left - radius;
        const y = e.clientY - rect.top - radius;
        
        // Apply styles
        circle.style.width = `${diameter}px`;
        circle.style.height = `${diameter}px`;
        circle.style.left = `${x}px`;
        circle.style.top = `${y}px`;
        circle.style.position = 'absolute';
        circle.style.borderRadius = '50%';
        circle.style.transform = 'scale(0)';
        circle.style.opacity = '0.5';
        circle.style.backgroundColor = params.color || 'currentColor'; // Use text color by default ensures contrast
        circle.style.pointerEvents = 'none';
        
        // Animation
        // We use Web Animations API for better performance and explicit control
        const animation = circle.animate([
            { transform: 'scale(0)', opacity: 0.5 },
            { transform: 'scale(2.5)', opacity: 0 }
        ], {
            duration: 600,
            easing: 'ease-out'
        });

        // Add to DOM
        // The button needs to be overflow hidden for this to work as internal ripple
        // We append to the button itself
        node.appendChild(circle);

        animation.onfinish = () => {
            circle.remove();
        };
    }

    node.addEventListener('click', handleClick);

    return {
        destroy() {
            node.removeEventListener('click', handleClick);
        }
    };
}
