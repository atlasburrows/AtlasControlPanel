window.radialMenu = {
    _active: false,
    _shelfOpen: false,
    _mode: null,        // 'hold' or 'tap'
    _items: [],
    _premiumModules: [],
    _utilItems: [],
    _selectedIndex: -1,
    _el: null,
    _overlay: null,
    _shelf: null,
    _centerX: 0,
    _centerY: 0,
    _dotnetRef: null,
    _radius: 110,
    _audioCtx: null,
    _swipeStartY: 0,
    _swipeTracking: false,

    _getAudioCtx: function () {
        if (!this._audioCtx) {
            try { this._audioCtx = new (window.AudioContext || window.webkitAudioContext)(); } catch (e) {}
        }
        return this._audioCtx;
    },

    _haptic: function (ms) {
        if (navigator.vibrate) navigator.vibrate(ms || 15);
    },

    _playNoise: function (duration, vol, highpass) {
        var ctx = this._getAudioCtx();
        if (!ctx) return;
        try {
            var bufSize = Math.floor(ctx.sampleRate * (duration || 0.03));
            var buf = ctx.createBuffer(1, bufSize, ctx.sampleRate);
            var data = buf.getChannelData(0);
            for (var i = 0; i < bufSize; i++) data[i] = Math.random() * 2 - 1;
            var src = ctx.createBufferSource();
            src.buffer = buf;
            var hp = ctx.createBiquadFilter();
            hp.type = 'highpass';
            hp.frequency.value = highpass || 8000;
            var gain = ctx.createGain();
            gain.gain.setValueAtTime(vol || 0.06, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + (duration || 0.03));
            src.connect(hp); hp.connect(gain); gain.connect(ctx.destination);
            src.start(ctx.currentTime);
        } catch (e) {}
    },

    _playClick: function (freq, duration, vol) {
        var ctx = this._getAudioCtx();
        if (!ctx) return;
        try {
            this._playNoise(0.02, 0.05, 9000);
            var osc = ctx.createOscillator();
            var gain = ctx.createGain();
            osc.connect(gain); gain.connect(ctx.destination);
            osc.type = 'triangle';
            osc.frequency.value = freq || 1200;
            gain.gain.setValueAtTime(vol || 0.06, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + (duration || 0.04));
            osc.start(ctx.currentTime);
            osc.stop(ctx.currentTime + (duration || 0.04));
        } catch (e) {}
    },

    _soundOpen: function () { this._playNoise(0.04, 0.05, 7000); },
    _soundHover: function () { this._playNoise(0.025, 0.035, 9000 + Math.random() * 2000); },
    _soundSelect: function () { this._playClick(1400, 0.05, 0.07); },
    _soundClose: function () { this._playNoise(0.03, 0.03, 6000); },
    _soundShelf: function () { this._playClick(900, 0.06, 0.05); },

    init: function (dotnetRef, items, premiumModules, utilItems) {
        var rm = window.radialMenu;
        rm._dotnetRef = dotnetRef;
        rm._items = items;
        rm._premiumModules = premiumModules || [];
        rm._utilItems = utilItems || [];

        var fab = document.getElementById('radialFab');
        if (!fab) return;
        rm._el = fab;

        var pressTimer = null;
        var holdFired = false;
        var startX = 0, startY = 0;
        var moved = false;

        // ── Touch ──
        fab.addEventListener('touchstart', function (e) {
            e.preventDefault();
            if (rm._shelfOpen) { rm.closeShelf(); holdFired = true; return; }
            if (rm._active && rm._mode === 'tap') { rm.close(); holdFired = true; return; }
            moved = false;
            holdFired = false;
            rm._swipeTracking = true;
            var touch = e.touches[0];
            startX = touch.clientX;
            startY = touch.clientY;
            rm._swipeStartY = startY;
            rm._haptic(8);
            pressTimer = setTimeout(function () {
                if (!moved) {
                    holdFired = true;
                    rm._mode = 'hold';
                    rm.open();
                }
            }, 250);
        }, { passive: false });

        fab.addEventListener('touchmove', function (e) {
            var touch = e.touches[0];
            var dy = rm._swipeStartY - touch.clientY;
            var dx = Math.abs(touch.clientX - startX);

            // Detect upward swipe before hold timer fires
            if (rm._swipeTracking && !rm._active && !holdFired && dy > 60 && dx < 40) {
                clearTimeout(pressTimer);
                rm._swipeTracking = false;
                holdFired = true;
                rm.openShelf();
                return;
            }

            if (rm._active && rm._mode === 'hold') {
                e.preventDefault();
                rm.track(touch.clientX, touch.clientY);
            } else if (!rm._active) {
                if (Math.sqrt((touch.clientX - startX) ** 2 + (touch.clientY - startY) ** 2) > 10) {
                    moved = true;
                    clearTimeout(pressTimer);
                }
            }
        }, { passive: false });

        fab.addEventListener('touchend', function (e) {
            clearTimeout(pressTimer);
            rm._swipeTracking = false;
            if (rm._active && rm._mode === 'hold') {
                e.preventDefault();
                rm.selectAndClose();
            } else if (!holdFired && !moved && !rm._active) {
                e.preventDefault();
                rm._mode = 'tap';
                rm.open();
                rm._attachTapListeners();
            }
        });

        // ── Mouse ──
        fab.addEventListener('mousedown', function (e) {
            e.preventDefault();
            if (rm._shelfOpen) { rm.closeShelf(); holdFired = true; return; }
            if (rm._active && rm._mode === 'tap') { rm.close(); holdFired = true; return; }
            moved = false;
            holdFired = false;
            startX = e.clientX;
            startY = e.clientY;
            pressTimer = setTimeout(function () {
                holdFired = true;
                rm._mode = 'hold';
                rm.open();
                document.addEventListener('mousemove', rm._onMouseMove);
                document.addEventListener('mouseup', rm._onMouseUp);
            }, 250);
        });

        fab.addEventListener('click', function (e) {
            if (holdFired) return;
            if (rm._shelfOpen) { rm.closeShelf(); return; }
            if (rm._active) { rm.close(); return; }
            e.preventDefault();
            rm._mode = 'tap';
            rm.open();
            rm._attachTapListeners();
        });

        // Double-click / double-tap = shelf (mouse)
        var lastClickTime = 0;
        fab.addEventListener('dblclick', function (e) {
            e.preventDefault();
            if (rm._active) rm.close(true);
            rm.openShelf();
        });
    },

    _onMouseMove: function (e) {
        if (window.radialMenu._active && window.radialMenu._mode === 'hold') {
            window.radialMenu.track(e.clientX, e.clientY);
        }
    },

    _onMouseUp: function () {
        var rm = window.radialMenu;
        document.removeEventListener('mousemove', rm._onMouseMove);
        document.removeEventListener('mouseup', rm._onMouseUp);
        if (rm._active && rm._mode === 'hold') rm.selectAndClose();
    },

    _attachTapListeners: function () {
        var rm = window.radialMenu;
        setTimeout(function () {
            if (!rm._active || rm._mode !== 'tap') return;

            rm._overlay.addEventListener('click', function (e) {
                if (e.target === rm._overlay) rm.close();
            });

            var items = rm._overlay.querySelectorAll('.radial-item');
            items.forEach(function (el) {
                el.addEventListener('click', function () {
                    var idx = parseInt(el.getAttribute('data-index'));
                    if (!isNaN(idx) && rm._items[idx]) rm._navigateAndClose(rm._items[idx].href);
                });
                el.addEventListener('mouseenter', function () { el.classList.add('radial-item-hover'); });
                el.addEventListener('mouseleave', function () { el.classList.remove('radial-item-hover'); });
                el.addEventListener('touchstart', function (e) {
                    e.stopPropagation();
                    items.forEach(function (i) { i.classList.remove('radial-item-hover'); });
                    el.classList.add('radial-item-hover');
                }, { passive: true });
                el.addEventListener('touchend', function () { el.classList.remove('radial-item-hover'); }, { passive: true });
            });

        }, 50);
    },

    // ═══════════════════════════
    //  RADIAL RING (core free)
    // ═══════════════════════════
    open: function () {
        var rm = window.radialMenu;
        rm._active = true;
        rm._selectedIndex = -1;

        var fab = rm._el;
        var rect = fab.getBoundingClientRect();
        rm._centerX = rect.left + rect.width / 2;
        rm._centerY = rect.top + rect.height / 2;

        var overlay = document.createElement('div');
        overlay.id = 'radialOverlay';
        overlay.className = 'radial-overlay radial-overlay-active';
        document.body.appendChild(overlay);
        rm._overlay = overlay;

        var items = rm._items;
        var count = items.length;
        var startAngle = -160;
        var endAngle = -20;
        var angleStep = count > 1 ? (endAngle - startAngle) / (count - 1) : 0;

        for (var i = 0; i < count; i++) {
            var angle = (startAngle + angleStep * i) * Math.PI / 180;
            var x = rm._centerX + rm._radius * Math.cos(angle);
            var y = rm._centerY + rm._radius * Math.sin(angle);

            var item = document.createElement('div');
            item.className = 'radial-item';
            item.setAttribute('data-index', i);
            item.style.left = x + 'px';
            item.style.top = y + 'px';
            item.innerHTML = '<span class="radial-item-icon">' + items[i].icon + '</span>' +
                             '<span class="radial-item-label">' + items[i].label + '</span>';
            item.style.setProperty('--item-color', items[i].color || '#3b82f6');
            overlay.appendChild(item);

            setTimeout((function(el) {
                return function() { el.classList.add('radial-item-visible'); };
            })(item), i * 25);
        }

        rm._haptic(25);
        rm._soundOpen();
        fab.classList.add('radial-fab-active');
    },

    track: function (tx, ty) {
        var rm = window.radialMenu;
        var items = rm._overlay ? rm._overlay.querySelectorAll('.radial-item') : [];
        var closest = -1;
        var closestDist = 55;

        for (var i = 0; i < items.length; i++) {
            var rect = items[i].getBoundingClientRect();
            var ix = rect.left + rect.width / 2;
            var iy = rect.top + rect.height / 2;
            var d = Math.sqrt((tx - ix) ** 2 + (ty - iy) ** 2);
            if (d < closestDist) { closestDist = d; closest = i; }
            items[i].classList.remove('radial-item-hover');
        }

        if (closest >= 0) {
            items[closest].classList.add('radial-item-hover');
            if (rm._selectedIndex !== closest) {
                rm._selectedIndex = closest;
                rm._haptic(12);
                rm._soundHover();
            }
        } else {
            rm._selectedIndex = -1;
        }
    },

    selectAndClose: function () {
        var rm = window.radialMenu;
        var href = null;
        if (rm._selectedIndex >= 0 && rm._items[rm._selectedIndex])
            href = rm._items[rm._selectedIndex].href;
        if (href) { rm._haptic(20); rm._soundSelect(); }
        if (href === '__shelf__') {
            rm.close(true);
            rm.openShelf();
            return;
        }
        rm.close(true);
        if (href && rm._dotnetRef)
            rm._dotnetRef.invokeMethodAsync('NavigateFromRadial', href);
    },

    _navigateAndClose: function (href) {
        var rm = window.radialMenu;
        rm._haptic(20);
        rm._soundSelect();
        if (href === '__shelf__') {
            rm.close(true);
            rm.openShelf();
            return;
        }
        rm.close(true);
        if (rm._shelfOpen) rm.closeShelf(true);
        if (href && rm._dotnetRef)
            rm._dotnetRef.invokeMethodAsync('NavigateFromRadial', href);
    },

    close: function (silent) {
        var rm = window.radialMenu;
        if (!silent) { rm._haptic(10); rm._soundClose(); }
        rm._active = false;
        rm._mode = null;
        rm._selectedIndex = -1;
        if (rm._overlay) { rm._overlay.remove(); rm._overlay = null; }
        if (rm._el) rm._el.classList.remove('radial-fab-active');
    },

    // ═══════════════════════════
    //  PREMIUM SHELF (swipe up)
    // ═══════════════════════════
    openShelf: function () {
        var rm = window.radialMenu;
        if (rm._shelfOpen) return;
        rm._shelfOpen = true;

        rm._haptic(30);
        rm._soundShelf();

        // Overlay
        var overlay = document.createElement('div');
        overlay.className = 'shelf-overlay';
        overlay.addEventListener('click', function () { rm.closeShelf(); });
        document.body.appendChild(overlay);
        requestAnimationFrame(function () { overlay.classList.add('shelf-overlay-active'); });

        // Shelf container
        var shelf = document.createElement('div');
        shelf.className = 'premium-shelf';

        // Handle bar
        shelf.innerHTML = '<div class="shelf-handle"><div class="shelf-handle-bar"></div></div>';

        // Header
        var header = document.createElement('div');
        header.className = 'shelf-header';
        header.innerHTML = '<div class="shelf-title">' +
            '<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="#f59e0b" stroke-width="2"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/></svg>' +
            '<span>Pro Modules</span></div>' +
            '<span class="shelf-badge">PRO</span>';
        shelf.appendChild(header);

        // Module grid
        var grid = document.createElement('div');
        grid.className = 'shelf-grid';

        rm._premiumModules.forEach(function (mod) {
            var card = document.createElement('div');
            card.className = 'shelf-module-card' + (mod.owned ? '' : ' shelf-module-locked');
            card.style.setProperty('--module-color', mod.color);

            var lockBadge = mod.owned ? '' :
                '<div class="module-lock-badge"><svg viewBox="0 0 24 24" width="10" height="10" fill="none" stroke="currentColor" stroke-width="2.5"><rect x="3" y="11" width="18" height="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg></div>';

            card.innerHTML = '<div class="module-icon-wrap">' + mod.icon + lockBadge + '</div>' +
                             '<span class="module-label">' + mod.label + '</span>';

            if (mod.owned) {
                card.addEventListener('click', function () { rm._navigateAndClose(mod.href); });
            } else {
                card.addEventListener('click', function () {
                    rm._haptic(15);
                    // TODO: Show upgrade prompt
                    rm._navigateAndClose('/settings');
                });
            }

            // Touch feedback
            card.addEventListener('touchstart', function () { card.classList.add('shelf-module-active'); }, { passive: true });
            card.addEventListener('touchend', function () { card.classList.remove('shelf-module-active'); }, { passive: true });
            card.addEventListener('mouseenter', function () { card.classList.add('shelf-module-active'); });
            card.addEventListener('mouseleave', function () { card.classList.remove('shelf-module-active'); });

            grid.appendChild(card);
        });

        shelf.appendChild(grid);

        // Utility footer (Settings, Sign Out)
        if (rm._utilItems.length > 0) {
            var footer = document.createElement('div');
            footer.className = 'shelf-footer';

            rm._utilItems.forEach(function (item) {
                var btn = document.createElement('div');
                btn.className = 'shelf-util-btn';
                btn.style.setProperty('--util-color', item.color);
                btn.innerHTML = '<span class="shelf-util-icon">' + item.icon + '</span>' +
                                '<span class="shelf-util-label">' + item.label + '</span>';
                btn.addEventListener('click', function () { rm._navigateAndClose(item.href); });
                btn.addEventListener('touchstart', function () { btn.classList.add('shelf-util-active'); }, { passive: true });
                btn.addEventListener('touchend', function () { btn.classList.remove('shelf-util-active'); }, { passive: true });
                footer.appendChild(btn);
            });

            shelf.appendChild(footer);
        }

        document.body.appendChild(shelf);
        rm._shelf = shelf;
        rm._shelfOverlay = overlay;

        // Swipe-down to close
        var shelfStartY = 0;
        var shelfDragging = false;
        var shelfTranslateY = 0;

        shelf.addEventListener('touchstart', function (e) {
            shelfStartY = e.touches[0].clientY;
            shelfDragging = true;
            shelfTranslateY = 0;
            shelf.style.transition = 'none';
        }, { passive: true });

        shelf.addEventListener('touchmove', function (e) {
            if (!shelfDragging) return;
            var dy = e.touches[0].clientY - shelfStartY;
            if (dy > 0) {
                shelfTranslateY = dy;
                shelf.style.transform = 'translateY(' + dy + 'px)';
                // Fade overlay as shelf drags down
                var opacity = Math.max(0, 1 - dy / 300);
                if (rm._shelfOverlay) rm._shelfOverlay.style.opacity = opacity;
            }
        }, { passive: true });

        shelf.addEventListener('touchend', function () {
            shelfDragging = false;
            shelf.style.transition = '';
            if (shelfTranslateY > 80) {
                // Threshold reached, close
                rm.closeShelf();
            } else {
                // Snap back
                shelf.style.transform = '';
                if (rm._shelfOverlay) rm._shelfOverlay.style.opacity = '';
            }
        }, { passive: true });

        // Animate in
        requestAnimationFrame(function () {
            requestAnimationFrame(function () {
                shelf.classList.add('premium-shelf-active');
            });
        });

        // Hide FAB
        rm._el.classList.add('radial-fab-shelf');
    },

    closeShelf: function (silent) {
        var rm = window.radialMenu;
        if (!rm._shelfOpen) return;
        rm._shelfOpen = false;

        if (!silent) { rm._haptic(10); rm._soundClose(); }

        if (rm._shelf) {
            rm._shelf.classList.remove('premium-shelf-active');
            rm._shelf.classList.add('premium-shelf-closing');
        }
        if (rm._shelfOverlay) {
            rm._shelfOverlay.classList.remove('shelf-overlay-active');
        }

        setTimeout(function () {
            if (rm._shelf) { rm._shelf.remove(); rm._shelf = null; }
            if (rm._shelfOverlay) { rm._shelfOverlay.remove(); rm._shelfOverlay = null; }
        }, 300);

        rm._el.classList.remove('radial-fab-shelf');
    }
};
