// Auto-hide header + FAB on scroll down, show on scroll up
// Reposition FAB above chat input bar only on chat page
(function () {
    var lastY = 0;
    var hidden = false;
    var threshold = 8;
    var attached = new WeakSet();
    var chatScrollAttached = false;

    function update(scrollY) {
        var delta = scrollY - lastY;
        if (delta > threshold && !hidden && scrollY > 60) {
            hidden = true;
            toggle(true);
        } else if (delta < -threshold && hidden) {
            hidden = false;
            toggle(false);
        }
        lastY = scrollY;
    }

    function toggle(hide) {
        var layout = document.querySelector('.atlas-layout');
        var header = document.querySelector('.atlas-header');
        var fab = document.getElementById('radialFab');
        if (layout) layout.classList.toggle('bars-hidden', hide);
        if (header) header.classList.toggle('scroll-hidden', hide);
        if (fab) fab.classList.toggle('scroll-hidden', hide);
    }

    function adjustFab() {
        var fab = document.getElementById('radialFab');
        if (!fab) return;
        var inputRow = document.querySelector('.chat-input-row');
        if (inputRow) {
            var rect = inputRow.getBoundingClientRect();
            var fromBottom = window.innerHeight - rect.top;
            fab.style.bottom = (fromBottom + 6) + 'px';
        } else {
            // Not on chat page — clear any inline override so CSS default applies
            if (fab.style.bottom) fab.style.bottom = '';
        }
    }

    function updateChat(scrollY) {
        var delta = scrollY - lastY;
        var layout = document.querySelector('.atlas-layout');
        var header = document.querySelector('.atlas-header');
        var fab = document.getElementById('radialFab');

        if (delta < -threshold) {
            // Scrolling up — show FAB, hide header
            if (header && scrollY > 60) header.classList.add('scroll-hidden');
            if (layout && scrollY > 60) layout.classList.add('bars-hidden');
            if (fab) fab.classList.remove('scroll-hidden');
            hidden = true;
        } else if (delta > threshold) {
            // Scrolling down — hide FAB, show header
            if (header) header.classList.remove('scroll-hidden');
            if (layout) layout.classList.remove('bars-hidden');
            if (fab) fab.classList.add('scroll-hidden');
            hidden = false;
        }
        lastY = scrollY;
    }

    function attach() {
        var body = document.querySelector('.atlas-body');
        if (body && !attached.has(body)) {
            attached.add(body);
            body.addEventListener('scroll', function () { update(body.scrollTop); }, { passive: true });
        }

        // Attach chat scroll handler when chat element exists
        var chat = document.getElementById('chatMessages');
        if (chat && !attached.has(chat)) {
            attached.add(chat);
            chat.addEventListener('scroll', function () {
                updateChat(chat.scrollTop);
                adjustFab();
            }, { passive: true });
        }

        adjustFab();
    }

    document.addEventListener('DOMContentLoaded', attach);
    setTimeout(attach, 200);
    setTimeout(attach, 500);
    setTimeout(attach, 1000);
    setInterval(attach, 1000);
})();
