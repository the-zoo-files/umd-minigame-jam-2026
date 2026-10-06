"use strict";
const assert = require("node:assert/strict");
const { dimensions, configure } = require("../../Assets/Editor/browser-resolution.js");

assert.deepEqual(dimensions(1920, 1080), { width: 1920, height: 1080 });
assert.deepEqual(dimensions(3840, 2160), { width: 1920, height: 1080 });
assert.deepEqual(dimensions(1080, 1920), { width: 1080, height: 1920 });
assert.deepEqual(dimensions(960, 540), { width: 960, height: 540 });
for (const [width, height] of [[8000, 2000], [2000, 8000], [4000, 4000], [0.1, 0.1]]) {
    const size = dimensions(width, height);
    assert.ok(size.width >= 1 && size.height >= 1);
    assert.ok(size.width * size.height <= 1920 * 1080);
    assert.ok(Math.max(size.width, size.height) <= 1920);
}
for (const pair of [[0, 100], [100, 0], [-1, 10], [NaN, 10], [Infinity, 10]]) {
    assert.equal(dimensions(...pair), null);
}

const listeners = {};
global.window = { addEventListener(name, callback) { listeners[name] = callback; } };
global.document = { addEventListener(name, callback) { listeners[name] = callback; } };
global.ResizeObserver = class {
    constructor(callback) { listeners.observer = callback; }
    observe(canvas) { assert.equal(canvas, target); }
};
let bounds = { width: 3840, height: 2160 };
let writes = 0;
const buffer = { width: 0, height: 0 };
const target = {
    getBoundingClientRect() { return bounds; },
    get width() { return buffer.width; },
    set width(value) { buffer.width = value; writes++; },
    get height() { return buffer.height; },
    set height(value) { buffer.height = value; writes++; }
};
const config = { dataUrl: "game.data", frameworkUrl: "game.framework.js" };
assert.equal(configure(target, config), config);
assert.equal(config.matchWebGLToCanvasSize, false);
assert.equal(config.dataUrl, "game.data");
assert.deepEqual(buffer, { width: 1920, height: 1080 });
listeners.observer();
listeners.resize();
assert.equal(writes, 2, "Unchanged dimensions must not reallocate the render buffer");
bounds = { width: 0, height: 0 };
listeners.observer();
assert.equal(writes, 2, "Hidden canvas keeps its previous buffer");
bounds = { width: 800, height: 600 };
listeners.fullscreenchange();
assert.deepEqual(buffer, { width: 800, height: 600 });
delete global.ResizeObserver;
configure(target, {}); // Older browsers use the resize/fullscreen events without an observer.
console.log("Browser resolution tests: PASS");
