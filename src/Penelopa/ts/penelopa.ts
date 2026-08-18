// Penelopa 浏览器辅助脚本。
// 暴露当前设备像素比并监听其变化（窗口跨不同 DPI 的显示器移动会触发 matchMedia 变更），
// 使 .NET 画布渲染器的 CSS→物理映射保持同步。
// 同时挂载画布指针层：指针捕获、画布相对 CSS 坐标、合成鼠标事件抑制、rAF 节流 move 事件，
// 全部以语义回调形式报告给 .NET。
//
// 由 TypeScript 编译（见 tsconfig.json）；生成的 wwwroot/js/penelopa.js
// 随仓库提交，使项目在无 node 环境下也能运行。

interface DotNetRef {
    invokeMethodAsync(methodName: string, ...args: unknown[]): Promise<unknown>;
}

interface CssPoint {
    x: number;
    y: number;
}

interface PointerLayerHandle {
    dispose(): void;
}

interface PenelopaNamespace {
    getDpr(): number;
    watchDpr(dotNetRef: DotNetRef): void;
    attachPointer(canvasEl: HTMLElement, dotNetRef: DotNetRef): PointerLayerHandle;
}

// 脚本级（非模块）接口扩展会合并进全局 Window 接口。
interface Window {
    penelopa: PenelopaNamespace;
}

window.penelopa = window.penelopa || ({} as PenelopaNamespace);

window.penelopa.getDpr = function (): number {
    return window.devicePixelRatio || 1;
};

window.penelopa.watchDpr = function (dotNetRef: DotNetRef): void {
    let mq: MediaQueryList | null = null;

    const onChange = (): void => {
        // 为新比率重新挂载监听；每次分辨率变更触发一次，然后监听下一个比率。
        if (mq) {
            mq.removeEventListener('change', onChange);
        }

        const dpr = window.devicePixelRatio || 1;
        mq = window.matchMedia('(resolution: ' + dpr + 'dppx)');
        mq.addEventListener('change', onChange);

        void dotNetRef.invokeMethodAsync('OnDevicePixelRatioChanged', dpr);
    };

    onChange();
};

// 将指针交互层挂载到画布元素。
// 返回 { dispose } 句柄供清理使用。
window.penelopa.attachPointer = function (canvasEl: HTMLElement, dotNetRef: DotNetRef): PointerLayerHandle {
    const state = {
        activePointerId: null as number | null,
        interacting: false,
        rect: null as DOMRect | null,
        lastPos: null as CssPoint | null,
        rafPending: false,
    };

    function refreshRect(): void {
        state.rect = canvasEl.getBoundingClientRect();
    }

    function toCss(e: PointerEvent | WheelEvent): CssPoint {
        // 画布相对 CSS 坐标；rect 是视口相对的且已包含滚动偏移，
        // 因此在滚动容器内结果正确，无需额外计算。
        const rect = state.rect as DOMRect;
        return {
            x: e.clientX - rect.left,
            y: e.clientY - rect.top,
        };
    }

    function onPointerDown(e: PointerEvent): void {
        // 让覆盖层控件（Fit 按钮）处理自身的点击：
        // 下方的合成鼠标抑制不能吞掉它们的事件。
        const target = e.target as HTMLElement | null;
        if (target && target.closest && target.closest('.penelopa-canvas-fit')) {
            return;
        }
        if (state.interacting) {
            return; // 已在交互中
        }
        if (e.pointerType !== 'mouse' && !e.isPrimary) {
            return; // 次级触摸指针不驱动编辑
        }
        if (e.pointerType === 'mouse' && e.button !== 0) {
            return; // 仅主键；右键交给 contextmenu
        }

        state.interacting = true;
        state.activePointerId = e.pointerId;
        refreshRect();
        try {
            canvasEl.setPointerCapture(e.pointerId);
        } catch {
            /* 捕获是尽力而为；move 事件仍会到达元素 */
        }
        canvasEl.classList.add('penelopa-dragging'); // touch-action: none
        e.preventDefault(); // 抑制拖拽后的合成鼠标事件

        const pos = toCss(e);
        void dotNetRef.invokeMethodAsync(
            'OnPointerDown',
            pos.x, pos.y, e.button, e.ctrlKey, e.shiftKey, e.pointerId);
    }

    function onPointerMove(e: PointerEvent): void {
        if (!state.interacting || e.pointerId !== state.activePointerId) {
            return;
        }

        state.lastPos = toCss(e);
        if (state.rafPending) {
            return;
        }

        state.rafPending = true;
        requestAnimationFrame(() => {
            state.rafPending = false;
            if (state.lastPos) {
                void dotNetRef.invokeMethodAsync('OnPointerMove', state.lastPos.x, state.lastPos.y);
            }
        });
    }

    function endInteraction(): void {
        state.interacting = false;
        state.activePointerId = null;
        state.lastPos = null;
        canvasEl.classList.remove('penelopa-dragging');
    }

    function onPointerUp(e: PointerEvent): void {
        if (!state.interacting || e.pointerId !== state.activePointerId) {
            return;
        }

        const pos = toCss(e);
        endInteraction();
        try {
            canvasEl.releasePointerCapture(e.pointerId);
        } catch {
            /* 已释放 */
        }
        void dotNetRef.invokeMethodAsync('OnPointerUp', pos.x, pos.y);
    }

    function onCancel(): void {
        if (!state.interacting) {
            return;
        }

        endInteraction();
        void dotNetRef.invokeMethodAsync('OnPointerCancel');
    }

    function onLostPointerCapture(e: PointerEvent): void {
        if (e.pointerId === state.activePointerId) {
            onCancel();
        }
    }

    function onWindowBlur(): void {
        if (state.interacting) {
            onCancel();
        }
    }

    function onVisibilityChange(): void {
        if (document.visibilityState === 'hidden' && state.interacting) {
            onCancel();
        }
    }

    function onKeyDown(e: KeyboardEvent): void {
        if (e.key === 'Escape') {
            void dotNetRef.invokeMethodAsync('OnEscape');
            return;
        }
        const mod = e.ctrlKey || e.metaKey;
        const key = e.key.toLowerCase();
        if (mod && !e.shiftKey && key === 'z') {
            e.preventDefault();
            void dotNetRef.invokeMethodAsync('OnUndo');
            return;
        }
        if (mod && (key === 'y' || (e.shiftKey && key === 'z'))) {
            e.preventDefault();
            void dotNetRef.invokeMethodAsync('OnRedo');
        }
    }

    function onWheel(e: WheelEvent): void {
        // 仅 Ctrl+滚轮缩放（浏览器约定）；普通滚轮留给页面/容器滚动。
        if (!e.ctrlKey) {
            return;
        }
        e.preventDefault();
        const pos = toCss(e);
        void dotNetRef.invokeMethodAsync('OnWheel', pos.x, pos.y, e.deltaY);
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
        dispose(): void {
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
