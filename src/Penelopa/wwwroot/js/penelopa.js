// Penelopa browser helpers.
// Exposes the current device pixel ratio and watches it for changes (moving
// the window across displays with different DPI fires a matchMedia change),
// so the .NET canvas renderer can keep its CSS-to-physical mapping in sync.
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
