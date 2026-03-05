window.chatPage = {
    disableBodyScroll: function () {
        document.body.style.overflow = 'hidden';
        var body = document.querySelector('.atlas-body');
        if (body) body.style.overflow = 'hidden';
    },
    enableBodyScroll: function () {
        document.body.style.overflow = '';
        var body = document.querySelector('.atlas-body');
        if (body) body.style.overflow = '';
    }
};

window.chatScroll = {
    scrollToBottom: function (elementId) {
        var el = document.getElementById(elementId);
        if (el) {
            el.scrollTop = el.scrollHeight;
        }
    },
    initScrollWatch: function (messagesId, btnId) {
        var el = document.getElementById(messagesId);
        var btn = document.getElementById(btnId);
        if (!el || !btn) return;
        if (el._scrollWatchReady) return;
        el._scrollWatchReady = true;

        function update() {
            var atBottom = (el.scrollHeight - el.scrollTop - el.clientHeight) < 40;
            btn.style.display = atBottom ? 'none' : 'flex';
        }

        el.addEventListener('scroll', update);
        update();
    }
};

window.chatInput = {
    autoResize: function (id) {
        var el = document.getElementById(id);
        if (!el) return;
        el.style.height = 'auto';
        var max = 200; // ~8 lines
        el.style.height = Math.min(el.scrollHeight, max) + 'px';
    },
    getValue: function (id) {
        var el = document.getElementById(id);
        return el ? el.value : '';
    },
    clear: function (id) {
        var el = document.getElementById(id);
        if (el) {
            el.value = '';
            el.style.height = 'auto';
            el.focus();
        }
    },
    setDisabled: function (id, disabled) {
        var el = document.getElementById(id);
        if (el) el.disabled = disabled;
    },
    setStreaming: function (inputId, btnId, streaming) {
        var input = document.getElementById(inputId);
        var btn = document.getElementById(btnId);
        if (btn) {
            if (streaming) {
                btn.classList.remove('chat-send-active');
                btn.classList.add('chat-send-streaming');
                btn.disabled = true;
            } else {
                btn.classList.remove('chat-send-streaming');
                btn.disabled = false;
                // Re-check text state
                if (input) {
                    var hasText = input.value.trim().length > 0;
                    btn.disabled = !hasText;
                    if (hasText) btn.classList.add('chat-send-active');
                }
            }
        }
    },
    initResize: function (id) {
        var el = document.getElementById(id);
        if (!el || el._resizeReady) return;
        el._resizeReady = true;
        el.addEventListener('input', function () {
            el.style.height = 'auto';
            el.style.height = Math.min(el.scrollHeight, 200) + 'px';
        });
    }
};
