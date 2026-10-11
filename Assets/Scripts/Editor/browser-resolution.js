(function (root) {
    "use strict";

    const maximumPixels = 1920 * 1080;
    const maximumEdge = 1920;

    function dimensions(width, height) {
        if (!Number.isFinite(width) || !Number.isFinite(height) || width <= 0 || height <= 0) {
            return null;
        }
        const scale = Math.min(1, maximumEdge / Math.max(width, height),
            Math.sqrt(maximumPixels / (width * height)));
        return { width: Math.max(1, Math.floor(width * scale)),
            height: Math.max(1, Math.floor(height * scale)) };
    }

    function configure(canvas, config) {
        // CSS controls layout; the backing buffer has a bounded, DPR-independent pixel budget.
        config.matchWebGLToCanvasSize = false;
        function resize() {
            const bounds = canvas.getBoundingClientRect();
            const size = dimensions(bounds.width, bounds.height);
            if (size === null) return; // Hidden/zero-size canvases retain their last valid buffer.
            if (canvas.width !== size.width) canvas.width = size.width;
            if (canvas.height !== size.height) canvas.height = size.height;
        }
        resize();
        if (typeof ResizeObserver !== "undefined") {
            const observer = new ResizeObserver(resize);
            observer.observe(canvas);
        }
        window.addEventListener("resize", resize);
        document.addEventListener("fullscreenchange", resize);
        return config;
    }

    root.UmdBrowserResolution = { dimensions, configure };
    if (typeof module !== "undefined") module.exports = root.UmdBrowserResolution;
}(typeof globalThis !== "undefined" ? globalThis : this));
