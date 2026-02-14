window.radialMenu = {
    _active: false,
    _mode: null,        // 'hold' or 'tap'
    _items: [],
    _corners: [],
    _selectedIndex: -1,
    _selectedCorner: -1,
    _el: null,
    _overlay: null,
    _centerX: 0,
    _centerY: 0,
    _dotnetRef: null,
    _radius: 120,
    _audioCtx: null,

    _getAudioCtx: function () {
        if (!this._audioCtx) {
            try { this._audioCtx = new (window.AudioContext || window.webkitAudioContext)(); } catch (e) {}
        }
        return this._audioCtx;
    },

    _haptic: function (ms) {
        if (navigator.vibrate) navigator.vibrate(ms || 15);
    },

    _getAudioCtx: function () {
        if (!this._audioCtx) {
            try { this._audioCtx = new (window.AudioContext || window.webkitAudioContext)(); } catch (e) {}
        }
        return this._audioCtx;
    },

    // Hi-hat / tisk noise burst
    _playNoise: function (duration, vol, highpass) {
        var ctx = this._getAudioCtx();
        if (!ctx) return;
        try {
            var bufSize = Math.floor(ctx.sampleRate * (duration || 0.03));
            var buf = ctx.createBuffer(1, bufSize, ctx.sampleRate);
            var data = buf.getChannelData(0);
            for (var i = 0; i < bufSize; i++) {
                data[i] = (Math.random() * 2 - 1);
            }
            var src = ctx.createBufferSource();
            src.buffer = buf;

            var hp = ctx.createBiquadFilter();
            hp.type = 'highpass';
            hp.frequency.value = highpass || 8000;

            var gain = ctx.createGain();
            gain.gain.setValueAtTime(vol || 0.06, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + (duration || 0.03));

            src.connect(hp);
            hp.connect(gain);
            gain.connect(ctx.destination);
            src.start(ctx.currentTime);
        } catch (e) {}
    },

    // Tisk + tonal click
    _playClick: function (freq, duration, vol) {
        var ctx = this._getAudioCtx();
        if (!ctx) return;
        try {
            // Noise transient
            this._playNoise(0.02, 0.05, 9000);
            // Short tonal click
            var osc = ctx.createOscillator();
            var gain = ctx.createGain();
            osc.connect(gain);
            gain.connect(ctx.destination);
            osc.type = 'triangle';
            osc.frequency.value = freq || 1200;
            gain.gain.setValueAtTime(vol || 0.06, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + (duration || 0.04));
            osc.start(ctx.currentTime);
            osc.stop(ctx.currentTime + (duration || 0.04));
        } catch (e) {}
    },

    // Sound presets
    _soundOpen: function () { this._playNoise(0.04, 0.05, 7000); },
    _soundHover: function () { this._playNoise(0.025, 0.035, 9000 + Math.random() * 2000); },
    _soundSelect: function () { this._playClick(1400, 0.05, 0.07); },
    _soundClose: function () { this._playNoise(0.03, 0.03, 6000); },

    init: function (dotnetRef, items, corners) {
        var rm = window.radialMenu;
        rm._dotnetRef = dotnetRef;
        rm._items = items;
        rm._corners = corners || [];

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
            // If menu is open (tap mode), close it
            if (rm._active && rm._mode === 'tap') {
                rm.close();
                holdFired = true; // prevent touchend from reopening
                return;
            }
            moved = false;
            holdFired = false;
            var touch = e.touches[0];
            startX = touch.clientX;
            startY = touch.clientY;
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
            if (rm._active && rm._mode === 'hold') {
                e.preventDefault();
                var touch = e.touches[0];
                rm.track(touch.clientX, touch.clientY);
            } else if (!rm._active) {
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
            // If menu is open (tap mode), close it
            if (rm._active && rm._mode === 'tap') {
                rm.close();
                holdFired = true;
                return;
            }
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
            if (rm._active) { rm.close(); return; }
            e.preventDefault();
            rm._mode = 'tap';
            rm.open();
            rm._attachTapListeners();
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
        if (rm._active && rm._mode === 'hold') {
            rm.selectAndClose();
        }
    },

    // ── Tap-mode listeners (click on items / overlay to dismiss) ──
    _tapOverlayHandler: null,
    _tapItemHandler: null,

    _attachTapListeners: function () {
        var rm = window.radialMenu;

        // Delay slightly so the opening tap doesn't immediately trigger
        setTimeout(function () {
            if (!rm._active || rm._mode !== 'tap') return;

            // Click on overlay background = close
            rm._tapOverlayHandler = function (e) {
                if (e.target === rm._overlay) {
                    rm.close();
                }
            };
            rm._overlay.addEventListener('click', rm._tapOverlayHandler);

            // Click + hover on radial items
            var items = rm._overlay.querySelectorAll('.radial-item');
            items.forEach(function (el) {
                // Tap to navigate
                el.addEventListener('click', function () {
                    var idx = parseInt(el.getAttribute('data-index'));
                    if (!isNaN(idx) && rm._items[idx]) {
                        rm._navigateAndClose(rm._items[idx].href);
                    }
                });
                // Touch/mouse hover — same highlight as hold mode
                el.addEventListener('touchstart', function (e) {
                    e.stopPropagation();
                    items.forEach(function (i) { i.classList.remove('radial-item-hover'); });
                    el.classList.add('radial-item-hover');
                }, { passive: true });
                el.addEventListener('touchend', function () {
                    el.classList.remove('radial-item-hover');
                }, { passive: true });
                el.addEventListener('mouseenter', function () {
                    el.classList.add('radial-item-hover');
                });
                el.addEventListener('mouseleave', function () {
                    el.classList.remove('radial-item-hover');
                });
            });

            // Click + hover on corner buttons
            var corners = rm._overlay.querySelectorAll('.radial-corner-btn');
            corners.forEach(function (el) {
                el.addEventListener('click', function () {
                    var c = parseInt(el.getAttribute('data-corner'));
                    if (!isNaN(c) && rm._corners[c]) {
                        rm._navigateAndClose(rm._corners[c].href);
                    }
                });
                el.addEventListener('touchstart', function (e) {
                    e.stopPropagation();
                    corners.forEach(function (c) { c.classList.remove('radial-corner-hover'); });
                    el.classList.add('radial-corner-hover');
                }, { passive: true });
                el.addEventListener('touchend', function () {
                    el.classList.remove('radial-corner-hover');
                }, { passive: true });
                el.addEventListener('mouseenter', function () {
                    el.classList.add('radial-corner-hover');
                });
                el.addEventListener('mouseleave', function () {
                    el.classList.remove('radial-corner-hover');
                });
            });

        }, 50);
    },

    open: function () {
        var rm = window.radialMenu;
        rm._active = true;
        rm._selectedIndex = -1;
        rm._selectedCorner = -1;

        var fab = rm._el;
        var rect = fab.getBoundingClientRect();
        rm._centerX = rect.left + rect.width / 2;
        rm._centerY = rect.top + rect.height / 2;

        var overlay = document.createElement('div');
        overlay.id = 'radialOverlay';
        overlay.className = 'radial-overlay radial-overlay-active';
        document.body.appendChild(overlay);
        rm._overlay = overlay;

        // Render radial items (arc above FAB)
        var items = rm._items;
        var count = items.length;
        var startAngle = -170;
        var endAngle = -10;
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

        // Render corner buttons (top-left, top-right)
        var corners = rm._corners;
        for (var c = 0; c < corners.length; c++) {
            var btn = document.createElement('div');
            btn.className = 'radial-corner-btn';
            btn.setAttribute('data-corner', c);
            if (corners[c].position === 'top-left') {
                btn.style.left = '20px';
                btn.style.top = 'calc(20px + env(safe-area-inset-top, 0px))';
            } else {
                btn.style.right = '20px';
                btn.style.top = 'calc(20px + env(safe-area-inset-top, 0px))';
            }
            btn.innerHTML = '<span class="radial-corner-icon">' + corners[c].icon + '</span>' +
                            '<span class="radial-corner-label">' + corners[c].label + '</span>';
            btn.style.setProperty('--item-color', corners[c].color || '#8b949e');
            overlay.appendChild(btn);

            setTimeout((function(el) {
                return function() { el.classList.add('radial-corner-visible'); };
            })(btn), 100 + c * 50);
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
            var d = Math.sqrt((tx - ix) * (tx - ix) + (ty - iy) * (ty - iy));
            if (d < closestDist) {
                closestDist = d;
                closest = i;
            }
            items[i].classList.remove('radial-item-hover');
        }

        var corners = rm._overlay ? rm._overlay.querySelectorAll('.radial-corner-btn') : [];
        var closestCorner = -1;
        var closestCornerDist = 55;

        for (var c = 0; c < corners.length; c++) {
            var crect = corners[c].getBoundingClientRect();
            var cx = crect.left + crect.width / 2;
            var cy = crect.top + crect.height / 2;
            var cd = Math.sqrt((tx - cx) * (tx - cx) + (ty - cy) * (ty - cy));
            if (cd < closestCornerDist) {
                closestCornerDist = cd;
                closestCorner = c;
            }
            corners[c].classList.remove('radial-corner-hover');
        }

        if (closestCorner >= 0 && closestCornerDist < closestDist) {
            closest = -1;
        } else {
            closestCorner = -1;
        }

        if (closest >= 0) {
            items[closest].classList.add('radial-item-hover');
            if (rm._selectedIndex !== closest || rm._selectedCorner !== -1) {
                rm._selectedIndex = closest;
                rm._selectedCorner = -1;
                rm._haptic(12);
                rm._soundHover();
            }
        } else if (closestCorner >= 0) {
            corners[closestCorner].classList.add('radial-corner-hover');
            if (rm._selectedCorner !== closestCorner || rm._selectedIndex !== -1) {
                rm._selectedCorner = closestCorner;
                rm._selectedIndex = -1;
                rm._haptic(12);
                rm._soundHover();
            }
        } else {
            rm._selectedIndex = -1;
            rm._selectedCorner = -1;
        }
    },

    selectAndClose: function () {
        var rm = window.radialMenu;
        var href = null;
        if (rm._selectedIndex >= 0 && rm._items[rm._selectedIndex]) {
            href = rm._items[rm._selectedIndex].href;
        } else if (rm._selectedCorner >= 0 && rm._corners[rm._selectedCorner]) {
            href = rm._corners[rm._selectedCorner].href;
        }
        if (href) {
            rm._haptic(20);
            rm._soundSelect();
        }
        rm.close(true);
        if (href && rm._dotnetRef) {
            rm._dotnetRef.invokeMethodAsync('NavigateFromRadial', href);
        }
    },

    _navigateAndClose: function (href) {
        var rm = window.radialMenu;
        rm._haptic(20);
        rm._soundSelect();
        rm.close(true);
        if (href && rm._dotnetRef) {
            rm._dotnetRef.invokeMethodAsync('NavigateFromRadial', href);
        }
    },

    close: function (silent) {
        var rm = window.radialMenu;
        if (!silent) {
            rm._haptic(10);
            rm._soundClose();
        }
        rm._active = false;
        rm._mode = null;
        rm._selectedIndex = -1;
        rm._selectedCorner = -1;

        if (rm._overlay) {
            rm._overlay.remove();
            rm._overlay = null;
        }
        if (rm._el) {
            rm._el.classList.remove('radial-fab-active');
        }
    }
};
