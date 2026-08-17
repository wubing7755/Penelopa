// Penelopa browser helpers.
// Exposes the current device pixel ratio and watches it for changes (moving
// the window across displays with different DPI fires a matchMedia change),
// so the .NET canvas renderer can keep its CSS-to-physical mapping in sync.
// Also attaches the canvas pointer layer: pointer capture, canvas-relative
// CSS coordinates, synthesized-mouse suppression, and rAF-throttled move
// events, all reported to .NET as semantic callbacks.
window.penelopa = window.penelopa || {};

window.penelopa.getDpr = function () {
    return window.devicePixelRatio || 1;
};

window.penelopa.watchDpr = function (dotNetRef) {
    var mq = null;

    var onChange = function () {
        // Re-arm the query for the new ratio; each resolution change fires
        // this once, then we listen for the next ratio.
        if (mq) {
            mq.removeEventListener('change', onChange);
        }

        var dpr = window.devicePixelRatio || 1;
        mq = window.matchMedia('(resolution: ' + dpr + 'dppx)');
        mq.addEventListener('change', onChange);

        dotNetRef.invokeMethodAsync('OnDevicePixelRatioChanged', dpr);
    };

    onChange();
};

// Attaches the pointer interaction layer to the canvas element.
// Returns a { dispose } handle for cleanup.
window.penelopa.attachPointer = function (canvasEl, dotNetRef) {
    var state = {
        activePointerId: null,
        interacting: false,
        rect: null,
        lastPos: null,
        rafPending: false,
    };

    function refreshRect() {
        state.rect = canvasEl.getBoundingClientRect();
    }

    function toCss(e) {
        // Canvas-relative CSS coordinates; the rect is viewport-relative and
        // includes scroll offset, so the result is correct inside a scroll
        // container without extra math.
        return {
            x: e.clientX - state.rect.left,
            y: e.clientY - state.rect.top,
        };
    }

    function onPointerDown(e) {
        // Let overlay controls (Fit button) handle their own clicks: the
        // synthesized-mouse suppression below must not eat their events.
        if (e.target && e.target.closest && e.target.closest('.penelopa-canvas-fit')) {
            return;
        }
        if (state.interacting) {
            return; // already in an interaction
        }
        if (e.pointerType !== 'mouse' && !e.isPrimary) {
            return; // secondary touch pointers do not drive editing
        }
        if (e.pointerType === 'mouse' && e.button !== 0) {
            return; // primary button only; right-click goes to contextmenu
        }

        state.interacting = true;
        state.activePointerId = e.pointerId;
        refreshRect();
        try {
            canvasEl.setPointerCapture(e.pointerId);
        } catch (_) {
            /* capture is best-effort; moves still arrive on the element */
        }
        canvasEl.classList.add('penelopa-dragging'); // touch-action: none
        e.preventDefault(); // suppress synthesized mouse events after drags

        var pos = toCss(e);
        dotNetRef.invokeMethodAsync(
            'OnPointerDown',
            pos.x, pos.y, e.button, e.ctrlKey, e.shiftKey, e.pointerId);
    }

    function onPointerMove(e) {
        if (!state.interacting || e.pointerId !== state.activePointerId) {
            return;
        }

        state.lastPos = toCss(e);
        if (state.rafPending) {
            return;
        }

        state.rafPending = true;
        requestAnimationFrame(function () {
            state.rafPending = false;
            if (state.lastPos) {
                dotNetRef.invokeMethodAsync('OnPointerMove', state.lastPos.x, state.lastPos.y);
            }
        });
    }

    function endInteraction() {
        state.interacting = false;
        state.activePointerId = null;
        state.lastPos = null;
        canvasEl.classList.remove('penelopa-dragging');
    }

    function onPointerUp(e) {
        if (!state.interacting || e.pointerId !== state.activePointerId) {
            return;
        }

        var pos = toCss(e);
        endInteraction();
        try {
            canvasEl.releasePointerCapture(e.pointerId);
        } catch (_) {
            /* already released */
        }
        dotNetRef.invokeMethodAsync('OnPointerUp', pos.x, pos.y);
    }

    function onCancel() {
        if (!state.interacting) {
            return;
        }

        endInteraction();
        dotNetRef.invokeMethodAsync('OnPointerCancel');
    }

    function onLostPointerCapture(e) {
        if (e.pointerId === state.activePointerId) {
            onCancel();
        }
    }

    function onWindowBlur() {
        if (state.interacting) {
            onCancel();
        }
    }

    function onVisibilityChange() {
        if (document.visibilityState === 'hidden' && state.interacting) {
            onCancel();
        }
    }

    function onKeyDown(e) {
        if (e.key === 'Escape') {
            dotNetRef.invokeMethodAsync('OnEscape');
            return;
        }
        var mod = e.ctrlKey || e.metaKey;
        var key = e.key.toLowerCase();
        if (mod && !e.shiftKey && key === 'z') {
            e.preventDefault();
            dotNetRef.invokeMethodAsync('OnUndo');
            return;
        }
        if (mod && (key === 'y' || (e.shiftKey && key === 'z'))) {
            e.preventDefault();
            dotNetRef.invokeMethodAsync('OnRedo');
        }
    }

    function onWheel(e) {
        // Only Ctrl+wheel zooms (the browser convention); plain wheel is left
        // to the page/container so it can scroll.
        if (!e.ctrlKey) {
            return;
        }
        e.preventDefault();
        var pos = toCss(e);
        dotNetRef.invokeMethodAsync('OnWheel', pos.x, pos.y, e.deltaY);
    }

    canvasEl.addEventListener('pointerdown', onPointerDown);
    canvasEl.addEventListener('pointermove', onPointerMove);
    canvasEl.addEventListener('pointerup', onPointerUp);
    canvasEl.addEventListener('pointercancel', onCancel);
    canvasEl.addEventListener('lostpointercapture', onLostPointerCapture);
    canvasEl.addEventListener('wheel', onWheel, { passive: false });
    window.addEventListener('blur', onWindowBlur);
    document.addEventListener('visibilitychange', onVisibilityChange);
    window.addEventListener('keydown', onKeyDown);

    return {
        dispose: function () {
            canvasEl.removeEventListener('pointerdown', onPointerDown);
            canvasEl.removeEventListener('pointermove', onPointerMove);
            canvasEl.removeEventListener('pointerup', onPointerUp);
            canvasEl.removeEventListener('pointercancel', onCancel);
            canvasEl.removeEventListener('lostpointercapture', onLostPointerCapture);
            canvasEl.removeEventListener('wheel', onWheel);
            window.removeEventListener('blur', onWindowBlur);
            document.removeEventListener('visibilitychange', onVisibilityChange);
            window.removeEventListener('keydown', onKeyDown);
            canvasEl.classList.remove('penelopa-dragging');
        },
    };
};
