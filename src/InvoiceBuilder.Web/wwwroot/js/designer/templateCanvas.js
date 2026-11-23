// Fabric.js A4 designer canvas: zoom, snapping + guides, 100-step undo/redo, keyboard shortcuts. Talks to Blazor via a DotNetObjectReference.
(function () {
    const M = window.IbTemplateMapper;
    const K = M.K;
    const SNAP_MM = 1.5;
    const GUIDE = "#DB2777";
    const MAX_HISTORY = 100;

    let canvas, dotnet, def, layer, hostEl, resizeObs, zoomMode = "fit", zoomValue = 1;
    let undo = [], redo = [], restoring = false, dirty = false, guides = [], keyHandler;

    function pageW() { return def.page.widthMm; }
    function pageH() { return def.page.heightMm; }

    function init(canvasId, hostId, ref, json) {
        dispose();
        dotnet = ref;
        hostEl = document.getElementById(hostId);
        def = normalise(JSON.parse(json));
        layer = "first";
        canvas = new fabric.Canvas(canvasId, { selection: true, preserveObjectStacking: true, centeredScaling: true, backgroundColor: "#FFFFFF" });
        canvas.on("object:moving", onMoving);
        canvas.on("object:modified", e => { rescaleStamp(e.target); if (resizeLine(e.target)) return; keepInside(e.target); snapshot(); });
        canvas.on("object:scaling", onScaling);
        canvas.on("selection:created", notify);
        canvas.on("selection:updated", notify);
        canvas.on("selection:cleared", notify);
        canvas.on("text:changed", notify);
        canvas.on("mouse:up", () => { guides = []; canvas.requestRenderAll(); });
        canvas.on("after:render", drawGuides);
        keyHandler = onKey;
        document.addEventListener("keydown", keyHandler);
        load(layer);
        setZoom("fit");
        if (resizeObs) resizeObs.disconnect();
        if (hostEl && window.ResizeObserver) {
            resizeObs = new ResizeObserver(() => { if (canvas && zoomMode === "fit" && hostEl.clientWidth > 0) setZoom("fit"); });
            resizeObs.observe(hostEl);
        }
        requestAnimationFrame(() => { if (canvas && zoomMode === "fit") setZoom("fit"); });
        undo = [JSON.stringify(def)];
        redo = [];
        dirty = false;
        notify();
    }

    function normalise(d) {
        d.page = d.page || { size: "A4", widthMm: 210, heightMm: 297 };
        d.firstPage = d.firstPage || { elements: [] };
        d.continuationPage = d.continuationPage || { elements: [], itemsTop: 40 };
        d.firstPage.elements = d.firstPage.elements || [];
        d.continuationPage.elements = d.continuationPage.elements || [];
        d.flow = d.flow || {};
        d.flow.itemsTable = d.flow.itemsTable || { xMm: 18, yMm: 90, widthMm: 174 };
        d.flow.totals = Object.assign({ mode: "followTable", gapMm: 12, xMm: 108, widthMm: 84, heightMm: 20 }, d.flow.totals);
        return d;
    }

    function dispose() {
        if (keyHandler) document.removeEventListener("keydown", keyHandler);
        keyHandler = null;
        if (resizeObs) { resizeObs.disconnect(); resizeObs = null; }
        if (canvas) { canvas.dispose(); canvas = null; }
    }

    function load(which) {
        restoring = true;
        layer = which;
        canvas.clear();
        canvas.backgroundColor = "#FFFFFF";
        const elements = (which === "first" ? def.firstPage.elements : def.continuationPage.elements).slice().sort((a, b) => (a.zIndex || 0) - (b.zIndex || 0));
        elements.forEach(e => canvas.add(M.elementToFabric(e)));
        if (which === "first") M.flowZones(def).forEach(z => canvas.add(z));
        canvas.discardActiveObject();
        canvas.requestRenderAll();
        restoring = false;
    }

    function commit() {
        if (!canvas) return;
        const active = canvas.getActiveObject();
        if (active && active.type === "activeSelection") {
            // Objects in a multi-selection have group-relative coordinates; release them so left/top are absolute.
            const members = active.getObjects().slice();
            const wasRestoring = restoring;
            restoring = true;
            canvas.discardActiveObject();
            try { commit(); } finally { restoring = wasRestoring; }
            const sel = new fabric.ActiveSelection(members, { canvas });
            restoring = true;
            canvas.setActiveObject(sel);
            restoring = wasRestoring;
            canvas.requestRenderAll();
            return;
        }
        const objs = canvas.getObjects();
        const els = objs
            .filter(o => o.ib && o.ib.kind === "el")
            .map(o => {
                const next = M.readGeometry(o);
                o.ib.el = next;
                return next;
            });
        if (layer === "first") {
            def.firstPage.elements = els;
            def.flow = M.readFlow(def, objs.filter(o => o.ib && o.ib.kind !== "el"), objs);
        } else {
            def.continuationPage.elements = els;
        }
    }

    function snapshot() {
        if (restoring) return;
        commit();
        const s = JSON.stringify(def);
        if (undo.length && undo[undo.length - 1] === s) return;
        undo.push(s);
        if (undo.length > MAX_HISTORY + 1) undo.shift();
        redo = [];
        dirty = true;
    }

    function restore(s) {
        def = JSON.parse(s);
        load(layer);
        dirty = true;
        notify();
    }

    function doUndo() { if (undo.length > 1) { redo.push(undo.pop()); restore(undo[undo.length - 1]); } }
    function doRedo() { if (redo.length) { const s = redo.pop(); undo.push(s); restore(s); } }

    function onMoving(e) {
        const o = e.target;
        guides = [];
        const x = o.left / K, y = o.top / K, w = o.getScaledWidth() / K, h = o.getScaledHeight() / K;
        const xs = [0, pageW() / 2, pageW()], ys = [0, pageH() / 2, pageH()];
        canvas.getObjects().forEach(p => {
            if (p === o || !p.visible) return;
            const px = p.left / K, py = p.top / K, pw = p.getScaledWidth() / K, ph = p.getScaledHeight() / K;
            xs.push(px, px + pw / 2, px + pw);
            ys.push(py, py + ph / 2, py + ph);
        });
        let nx = Math.round(x), ny = Math.round(y);
        const bestX = best([x, x + w / 2, x + w], xs), bestY = best([y, y + h / 2, y + h], ys);
        if (bestX) { nx = x + bestX.delta; guides.push({ v: bestX.target }); }
        if (bestY) { ny = y + bestY.delta; guides.push({ h: bestY.target }); }
        o.set({ left: nx * K, top: ny * K });
        o.setCoords();
        notify();
    }

    function best(mine, targets) {
        let r = null;
        mine.forEach(m => targets.forEach(t => {
            const d = t - m;
            if (Math.abs(d) <= SNAP_MM && (!r || Math.abs(d) < Math.abs(r.delta))) r = { delta: d, target: t };
        }));
        return r;
    }

    function onScaling() { notify(); }

    function resizeLine(o) {
        if (!o || !o.ib || o.ib.kind !== "el" || o.ib.el.type !== "line" || Math.abs(o.scaleX - 1) < 1e-6) return false;
        const el = o.ib.el;
        const dx = el.x2Mm - el.xMm, dy = el.y2Mm - el.yMm;
        const len = Math.hypot(dx, dy);
        const s = len > 0 ? Math.max(o.scaleX, 1 / len) : 1;
        let ndx = dx * s, ndy = dy * s;
        const fit = Math.min(Math.abs(ndx) > pageW() ? pageW() / Math.abs(ndx) : 1, Math.abs(ndy) > pageH() ? pageH() / Math.abs(ndy) : 1);
        ndx *= fit;
        ndy *= fit;
        const c = o.getCenterPoint();
        let x = c.x / K - ndx / 2, y = c.y / K - ndy / 2;
        x += Math.max(0, -Math.min(x, x + ndx)) - Math.max(0, Math.max(x, x + ndx) - pageW());
        y += Math.max(0, -Math.min(y, y + ndy)) - Math.max(0, Math.max(y, y + ndy) - pageH());
        patch(el.id, { xMm: round(x), yMm: round(y), x2Mm: round(x + ndx), y2Mm: round(y + ndy), widthMm: round(Math.hypot(ndx, ndy)) });
        return true;
    }

    function isStamp(o) {
        const el = o && o.ib && o.ib.kind === "el" ? o.ib.el : null;
        return !!el && (el.sizePt || 0) >= 24;
    }

    function rescaleStamp(o) {
        if (!isStamp(o) || !o.ib.w0) return;
        const w = o.width * Math.abs(o.scaleX || 1);
        const factor = w / o.ib.w0;
        if (Math.abs(factor - 1) < 0.001) { o.scaleX = 1; o.scaleY = 1; return; }
        const c = o.getCenterPoint();
        const size = Math.max(24, Math.round((o.ib.el.sizePt || 24) * factor));
        o.ib.el = { ...o.ib.el, sizePt: size };
        o.set({ scaleX: 1, scaleY: 1, width: w, fontSize: size * 0.3528 * K });
        o.ib.w0 = w;
        o.setPositionByOrigin(c, "center", "center");
        o.setCoords();
    }

    function keepInside(o) {
        if (o && o.ib && o.ib.kind === "el" && o.ib.el.type === "line") {
            const lw = Math.abs(o.ib.el.x2Mm - o.ib.el.xMm) * K, lh = Math.abs(o.ib.el.y2Mm - o.ib.el.yMm) * K;
            o.set({
                left: Math.min(Math.max(o.left, 0), Math.max(0, pageW() * K - lw)),
                top: Math.min(Math.max(o.top, 0), Math.max(0, pageH() * K - lh)),
            });
            o.setCoords();
            canvas.requestRenderAll();
            return;
        }
        if (!o || !o.ib || o.ib.kind !== "el" || Math.round(o.angle || 0) !== 0) return;
        const maxW = pageW() * K, maxH = pageH() * K;
        let w = o.width * Math.abs(o.scaleX || 1), h = o.height * Math.abs(o.scaleY || 1);
        if (w > maxW) { o.scaleX = maxW / o.width; w = maxW; }
        if (h > maxH) { o.scaleY = maxH / o.height; h = maxH; }
        const c = o.getCenterPoint();
        const left = Math.min(Math.max(c.x - w / 2, 0), maxW - w), top = Math.min(Math.max(c.y - h / 2, 0), maxH - h);
        o.setPositionByOrigin(new fabric.Point(left + w / 2, top + h / 2), "center", "center");
        o.setCoords();
        canvas.requestRenderAll();
    }

    function drawGuides() {
        if (!guides.length) return;
        const ctx = canvas.getContext();
        const z = canvas.getZoom();
        ctx.save();
        ctx.strokeStyle = GUIDE;
        ctx.fillStyle = GUIDE;
        ctx.lineWidth = 1;
        ctx.font = "11px Manrope, sans-serif";
        guides.forEach(g => {
            ctx.beginPath();
            if (g.v !== undefined) { ctx.moveTo(g.v * K * z, 0); ctx.lineTo(g.v * K * z, pageH() * K * z); ctx.fillText(g.v.toFixed(1) + " mm", g.v * K * z + 4, 12); }
            if (g.h !== undefined) { ctx.moveTo(0, g.h * K * z); ctx.lineTo(pageW() * K * z, g.h * K * z); ctx.fillText(g.h.toFixed(1) + " mm", 4, g.h * K * z - 4); }
            ctx.stroke();
        });
        ctx.restore();
    }

    function setZoom(mode) {
        zoomMode = mode;
        let z;
        if (mode === "fit") {
            const avail = Math.max(200, (hostEl && hostEl.clientWidth ? hostEl.clientWidth : 800) - 64);
            z = Math.min(1.5, Math.max(0.3, avail / (pageW() * K)));
        } else {
            // 100% = true size at 96 dpi: 1 mm = 3.7795 px; canvas unit = 1/K mm
            z = (Number(mode) / 100) * (96 / 25.4) / K;
        }
        zoomValue = z;
        canvas.setDimensions({ width: pageW() * K * z, height: pageH() * K * z });
        canvas.setZoom(z);
        canvas.requestRenderAll();
        notify();
    }

    function selected() {
        const o = canvas && canvas.getActiveObject();
        if (!o || !o.ib) return null;
        if (o.ib.kind !== "el") return { kind: o.ib.kind, id: o.ib.kind, xMm: round(o.left / K), yMm: round(o.top / K), widthMm: round(o.getScaledWidth() / K), heightMm: 0 };
        const el = M.readGeometry(o);
        return { kind: "el", ...el };
    }

    function round(n) { return Math.round(n * 100) / 100; }

    function state() {
        const objs = canvas ? canvas.getObjects().filter(o => o.ib && o.ib.kind === "el") : [];
        const layers = objs.map(o => ({ id: o.ib.el.id, type: o.ib.el.type, label: M.labelFor(o.ib.el), locked: !!o.ib.el.locked, hidden: !!o.ib.el.hidden })).reverse();
        const sel = selected();
        return { layers, selected: sel, layer, dirty, canUndo: undo.length > 1, canRedo: redo.length > 0, zoom: Math.round(zoomValue * K / (96 / 25.4) * 100) };
    }

    function notify() {
        if (!dotnet || restoring) return;
        dotnet.invokeMethodAsync("OnDesignerState", JSON.stringify(state()));
    }

    function byId(id) { return canvas.getObjects().find(o => o.ib && o.ib.kind === "el" && o.ib.el.id === id); }

    function setLayer(which) {
        commit();
        load(which);
        notify();
    }

    function add(type) {
        if (type === "schedule") {
            if (!def.flow.instalmentSchedule) def.flow.instalmentSchedule = { gapMm: 8, xMm: 18, widthMm: 174 };
            commit();
            snapshot();
            load(layer);
            notify();
            return;
        }
        const el = M.newElement(type, layer);
        const o = M.elementToFabric(el);
        canvas.add(o);
        canvas.setActiveObject(o);
        snapshot();
        notify();
    }

    function remove() {
        const o = canvas.getActiveObject();
        if (!o) return;
        if (o.ib.kind === "schedule") { delete def.flow.instalmentSchedule; canvas.remove(o); snapshot(); notify(); return; }
        if (o.ib.kind !== "el" || o.ib.el.locked) return;
        canvas.remove(o);
        canvas.discardActiveObject();
        snapshot();
        notify();
    }

    function duplicate() {
        const o = canvas.getActiveObject();
        if (!o || !o.ib || o.ib.kind !== "el") return;
        const el = { ...M.readGeometry(o), id: M.newId(), xMm: o.ib.el.xMm + 5, yMm: o.ib.el.yMm + 5 };
        if (el.type === "line") { el.x2Mm += 5; el.y2Mm += 5; }
        const c = M.elementToFabric(el);
        canvas.add(c);
        canvas.setActiveObject(c);
        snapshot();
        notify();
    }

    function select(id) {
        const o = byId(id);
        if (o) { canvas.setActiveObject(o); canvas.requestRenderAll(); notify(); }
    }

    function patch(id, changes) {
        const o = byId(id);
        if (!o) return;
        const current = M.readGeometry(o);
        const el = { ...current, ...changes };
        if (current.type === "line") {
            const dx = current.x2Mm - current.xMm, dy = current.y2Mm - current.yMm;
            const len = Math.hypot(dx, dy);
            const ux = len > 0 ? dx / len : 1, uy = len > 0 ? dy / len : 0;
            const newLen = changes.widthMm !== undefined ? Math.max(1, changes.widthMm) : len;
            el.x2Mm = round(el.xMm + ux * newLen);
            el.y2Mm = round(el.yMm + uy * newLen);
            el.widthMm = 0;
            el.heightMm = 0;
        }
        const idx = canvas.getObjects().indexOf(o);
        canvas.remove(o);
        const n = M.elementToFabric(el);
        canvas.insertAt(idx, n);
        canvas.setActiveObject(n);
        snapshot();
        notify();
    }

    function reorder(id, dir) {
        const o = byId(id);
        if (!o) return;
        const objs = canvas.getObjects();
        const step = dir > 0 ? 1 : -1;
        let j = objs.indexOf(o) + step;
        while (j >= 0 && j < objs.length && !(objs[j].ib && objs[j].ib.kind === "el")) j += step;
        if (j < 0 || j >= objs.length) return;
        canvas.moveObjectTo(o, j);
        canvas.requestRenderAll();
        snapshot();
        notify();
    }

    function toFront() {
        const o = canvas.getActiveObject();
        if (!o || !o.ib || o.ib.kind !== "el") return;
        canvas.bringObjectToFront(o);
        canvas.requestRenderAll();
        snapshot();
        notify();
    }

    function toBack() {
        const o = canvas.getActiveObject();
        if (!o || !o.ib || o.ib.kind !== "el") return;
        canvas.sendObjectToBack(o);
        canvas.requestRenderAll();
        snapshot();
        notify();
    }

    function moveLayer(id, targetId) {
        const o = byId(id), t = byId(targetId);
        if (!o || !t || o === t) return;
        canvas.moveObjectTo(o, canvas.getObjects().indexOf(t));
        canvas.requestRenderAll();
        snapshot();
        notify();
    }

    function nudge(dx, dy) {
        const o = canvas.getActiveObject();
        if (!o || !o.ib || (o.ib.kind === "el" && o.ib.el.locked)) return;
        o.set({ left: o.left + dx * K, top: o.top + dy * K });
        o.setCoords();
        canvas.requestRenderAll();
        snapshot();
        notify();
    }

    function onKey(e) {
        const t = e.target;
        if (t && (t.tagName === "INPUT" || t.tagName === "TEXTAREA" || t.tagName === "SELECT" || t.isContentEditable)) return;
        if (!canvas || canvas.getActiveObject()?.isEditing) return;
        const ctrl = e.ctrlKey || e.metaKey;
        const step = e.shiftKey ? 5 : 1;
        if (ctrl && e.key.toLowerCase() === "z") { e.preventDefault(); e.shiftKey ? doRedo() : doUndo(); }
        else if (ctrl && e.key.toLowerCase() === "y") { e.preventDefault(); doRedo(); }
        else if (ctrl && e.key.toLowerCase() === "d") { e.preventDefault(); duplicate(); }
        else if (e.key === "Delete") { e.preventDefault(); remove(); }
        else if (e.key === "ArrowLeft") { e.preventDefault(); nudge(-step, 0); }
        else if (e.key === "ArrowRight") { e.preventDefault(); nudge(step, 0); }
        else if (e.key === "ArrowUp") { e.preventDefault(); nudge(0, -step); }
        else if (e.key === "ArrowDown") { e.preventDefault(); nudge(0, step); }
    }

    function setGeometry(changes) {
        const o = canvas.getActiveObject();
        if (!o || !o.ib) return;
        if (o.ib.kind === "el") { patch(o.ib.el.id, changes); return; }
        if (changes.xMm !== undefined) o.set({ left: changes.xMm * K });
        if (changes.yMm !== undefined) o.set({ top: changes.yMm * K });
        if (changes.widthMm !== undefined) o.set({ scaleX: (changes.widthMm * K) / o.width });
        o.setCoords();
        canvas.requestRenderAll();
        snapshot();
        notify();
    }

    function centreHorizontally() {
        const o = canvas && canvas.getActiveObject();
        if (!o || !o.ib) return;
        if (o.ib.kind !== "el") {
            setGeometry({ xMm: round((pageW() - o.getScaledWidth() / K) / 2) });
            return;
        }
        const el = M.readGeometry(o);
        if (el.locked) return;
        if (el.type === "line") {
            const dx = el.x2Mm - el.xMm;
            const left = (pageW() - Math.abs(dx)) / 2;
            patch(el.id, { xMm: round(dx >= 0 ? left : left + Math.abs(dx)) });
            return;
        }
        patch(el.id, { xMm: round((pageW() - el.widthMm) / 2) });
    }

    window.IbDesigner = {
        init, dispose,
        getJson: () => { commit(); return JSON.stringify(def); },
        markSaved: () => { dirty = false; notify(); },
        reorder, moveLayer, toFront, toBack, setGeometry, centreHorizontally,
        setZoom, setLayer, add, remove, duplicate, select, patch,
        undo: doUndo,
        redo: doRedo
    };
})();
