window.radialMenu = {
    _active: false,
    _items: [],
    _selectedIndex: -1,
    _el: null,
    _overlay: null,
    _centerX: 0,
    _centerY: 0,
    _dotnetRef: null,

    init: function (dotnetRef, items) {
        window.radialMenu._dotnetRef = dotnetRef;
        window.radialMenu._items = items; // [{icon, label, href, color}]

        var fab = document.getElementById('radialFab');
        if (!fab) return;
        window.radialMenu._el = fab;

        // Long press detection
        var pressTimer = null;
        var startX = 0, startY = 0;
        var moved = false;

        fab.addEventListener('touchstart', function (e) {
            e.preventDefault();
            moved = false;
            var touch = e.touches[0];
            startX = touch.clientX;
            startY = touch.clientY;
            pressTimer = setTimeout(function () {
                if (!moved) window.radialMenu.open(startX, startY);
            }, 200);
        }, { passive: false });

        fab.addEventListener('touchmove', function (e) {
            if (window.radialMenu._active) {
                e.preventDefault();
                var touch = e.touches[0];
                window.radialMenu.track(touch.clientX, touch.clientY);
            } else {
                var touch = e.touches[0];
                var dx = touch.clientX - startX;
                var dy = touch.clientY - startY;
                if (Math.sqrt(dx * dx + dy * dy) > 10) {
                    moved = true;
                    clearTimeout(pressTimer);
                }
            }
        }, { passive: false });

        fab.addEventListener('touchend', function (e) {
            clearTimeout(pressTimer);
            if (window.radialMenu._active) {
                e.preventDefault();
                window.radialMenu.release();
            }
        });

        // Mouse support for desktop testing
        fab.addEventListener('mousedown', function (e) {
            e.preventDefault();
            moved = false;
            startX = e.clientX;
            startY = e.clientY;
            pressTimer = setTimeout(function () {
                window.radialMenu.open(startX, startY);
                document.addEventListener('mousemove', window.radialMenu._onMouseMove);
                document.addEventListener('mouseup', window.radialMenu._onMouseUp);
            }, 200);
        });

        // Simple tap = go home
        fab.addEventListener('click', function () {
            if (!window.radialMenu._active) {
                // Short tap — do nothing or go home
            }
        });
    },

    _onMouseMove: function (e) {
        if (window.radialMenu._active) {
            window.radialMenu.track(e.clientX, e.clientY);
        }
    },

    _onMouseUp: function () {
        document.removeEventListener('mousemove', window.radialMenu._onMouseMove);
        document.removeEventListener('mouseup', window.radialMenu._onMouseUp);
        if (window.radialMenu._active) {
            window.radialMenu.release();
        }
    },

    open: function (cx, cy) {
        window.radialMenu._active = true;
        window.radialMenu._selectedIndex = -1;

        // Get FAB position
        var fab = window.radialMenu._el;
        var rect = fab.getBoundingClientRect();
        window.radialMenu._centerX = rect.left + rect.width / 2;
        window.radialMenu._centerY = rect.top + rect.height / 2;

        // Create overlay
        var overlay = document.createElement('div');
        overlay.id = 'radialOverlay';
        overlay.className = 'radial-overlay radial-overlay-active';
        document.body.appendChild(overlay);
        window.radialMenu._overlay = overlay;

        // Create menu items in arc
        var items = window.radialMenu._items;
        var count = items.length;
        var radius = 120;
        // Arc from left to right, spreading above center FAB
        var startAngle = -170;
        var endAngle = -10;
        var angleStep = count > 1 ? (endAngle - startAngle) / (count - 1) : 0;

        for (var i = 0; i < count; i++) {
            var angle = (startAngle + angleStep * i) * Math.PI / 180;
            var x = window.radialMenu._centerX + radius * Math.cos(angle);
            var y = window.radialMenu._centerY + radius * Math.sin(angle);

            var item = document.createElement('div');
            item.className = 'radial-item';
            item.setAttribute('data-index', i);
            item.style.left = x + 'px';
            item.style.top = y + 'px';
            item.innerHTML = '<span class="radial-item-icon">' + items[i].icon + '</span>' +
                             '<span class="radial-item-label">' + items[i].label + '</span>';
            item.style.setProperty('--item-color', items[i].color || '#3b82f6');
            overlay.appendChild(item);

            // Animate in
            setTimeout((function(el, idx) {
                return function() { el.classList.add('radial-item-visible'); };
            })(item, i), i * 30);
        }

        // Haptic feedback if available
        if (navigator.vibrate) navigator.vibrate(20);

        // Add FAB active state
        fab.classList.add('radial-fab-active');
    },

    track: function (tx, ty) {
        var items = window.radialMenu._overlay ? window.radialMenu._overlay.querySelectorAll('.radial-item') : [];
        var closest = -1;
        var closestDist = 60; // Max distance to select

        for (var i = 0; i < items.length; i++) {
            var rect = items[i].getBoundingClientRect();
            var ix = rect.left + rect.width / 2;
            var iy = rect.top + rect.height / 2;
            var dist = Math.sqrt((tx - ix) * (tx - ix) + (ty - iy) * (ty - iy));
            if (dist < closestDist) {
                closestDist = dist;
                closest = i;
            }
            items[i].classList.remove('radial-item-hover');
        }

        if (closest >= 0) {
            items[closest].classList.add('radial-item-hover');
            if (window.radialMenu._selectedIndex !== closest) {
                window.radialMenu._selectedIndex = closest;
                if (navigator.vibrate) navigator.vibrate(10);
            }
        } else {
            window.radialMenu._selectedIndex = -1;
        }
    },

    release: function () {
        var idx = window.radialMenu._selectedIndex;
        window.radialMenu._active = false;

        // Clean up
        if (window.radialMenu._overlay) {
            window.radialMenu._overlay.remove();
            window.radialMenu._overlay = null;
        }
        if (window.radialMenu._el) {
            window.radialMenu._el.classList.remove('radial-fab-active');
        }

        // Navigate
        if (idx >= 0 && idx < window.radialMenu._items.length) {
            var href = window.radialMenu._items[idx].href;
            if (href && window.radialMenu._dotnetRef) {
                window.radialMenu._dotnetRef.invokeMethodAsync('NavigateFromRadial', href);
            }
        }
    }
};
