// Maps TemplateDefinition JSON (millimetres, schema v1) <-> Fabric objects. The server validates; this is never the source of truth.
(function () {
    const K = 4; // canvas units per mm
    const PT_MM = 0.3528;
    const TEAL = "#0F766E";
    const ITEMS_ZONE_H = 50;

    const newId = () => "e" + Date.now().toString(36) + Math.random().toString(36).slice(2, 6);

    function labelFor(el) {
        if (el.text) return el.text;
        if (el.binding) return "{{" + el.binding + "}}";
        return el.type;
    }

    function placeholder(el, label) {
        const rect = new fabric.Rect({ width: el.widthMm * K, height: el.heightMm * K, fill: "#F1F5F9", stroke: "#64748B", strokeDashArray: [6, 4], strokeWidth: 1, strokeUniform: true });
        const text = new fabric.Text(label, { fontSize: 12, fill: "#334155", fontFamily: "Manrope, sans-serif", left: 6, top: 6 });
        return new fabric.Group([rect, text], { subTargetCheck: false });
    }

    let tokens = {};
    function setTokens(t) { tokens = t || {}; }
    function resolveColour(v, fallback) {
        if (!v) return fallback;
        if (v === "transparent") return "transparent";
        if (v.startsWith("#")) return v;
        return tokens[v] || fallback;
    }

    function elementToFabric(el) {
        let obj;
        const left = el.xMm * K, top = el.yMm * K;
        switch (el.type) {
            case "text":
            case "field":
            case "signature":
                obj = new fabric.Textbox(labelFor(el), {
                    width: Math.max(el.widthMm, 5) * K,
                    fontSize: (el.sizePt || 10) * PT_MM * K,
                    fontWeight: el.weight || 400,
                    fill: el.color && el.color.startsWith("#") ? el.color : "#0F172A",
                    fontFamily: "Manrope, sans-serif",
                    textAlign: el.align || "left",
                    lineHeight: el.lineHeight || 1.3,
                    splitByGrapheme: false,
                });
                break;
            case "shape":
                {
                    const sw = Math.max(el.widthMm, 0.5) * K, sh = Math.max(el.heightMm, 0.5) * K;
                    const look = {
                        fill: resolveColour(el.fill, "#99D5CF"),
                        stroke: resolveColour(el.stroke, undefined),
                        strokeWidth: (el.strokeWidthMm || 0) * K,
                        strokeUniform: true,
                    };
                    obj = el.shape === "ellipse"
                        ? new fabric.Ellipse({ rx: sw / 2, ry: sh / 2, ...look })
                        : new fabric.Rect({ width: sw, height: sh, rx: (el.radiusMm || 0) * K, ry: (el.radiusMm || 0) * K, ...look });
                }
                break;
            case "line":
                obj = new fabric.Line([el.xMm * K, el.yMm * K, el.x2Mm * K, el.y2Mm * K], {
                    originX: "left", originY: "top",
                    stroke: el.stroke && el.stroke.startsWith("#") ? el.stroke : "#0F172A",
                    strokeWidth: Math.max(el.strokeWidthMm || 0.3, 0.2) * K,
                });
                break;
            case "image":
                obj = placeholder(el, "Image / logo");
                break;
            case "payQr":
                obj = placeholder(el, "Pay QR");
                break;
            default:
                obj = placeholder(el, el.type);
        }
        if (el.type !== "line") {
            obj.set({ originX: "left", originY: "top", left, top, angle: 0 });
            if (el.rotation) {
                const c = new fabric.Point(left + (obj.width * (obj.scaleX || 1)) / 2, top + (obj.height * (obj.scaleY || 1)) / 2);
                obj.set({ angle: el.rotation });
                obj.setPositionByOrigin(c, "center", "center");
            }
            obj.setCoords();
        }
        obj.ib = { kind: "el", el: { ...el, id: el.id || newId() }, w0: obj.width };
        applyFlags(obj);
        return obj;
    }

    function applyFlags(obj) {
        const el = obj.ib.el;
        obj.visible = !el.hidden;
        obj.opacity = el.opacity > 0 ? el.opacity : 1;
        const locked = !!el.locked;
        obj.set({
            lockMovementX: locked, lockMovementY: locked, lockRotation: locked, hasControls: !locked,
            lockScalingX: locked, lockScalingY: locked || el.type === "line",
            borderColor: TEAL, cornerColor: TEAL, cornerStyle: "circle", transparentCorners: false, cornerSize: 10,
        });
        if (el.type === "line") obj.setControlsVisibility({ tl: false, tr: false, bl: false, br: false, mt: false, mb: false, mtr: false });
    }

    function zone(kind, label, x, y, w, h) {
        const rect = new fabric.Rect({ width: w * K, height: h * K, fill: "rgba(20,184,166,.07)", stroke: TEAL, strokeDashArray: [8, 5], strokeWidth: 1, strokeUniform: true });
        const text = new fabric.Text(label, { fontSize: 12, fill: "#0B4F4A", fontFamily: "Manrope, sans-serif", left: 6, top: 6 });
        const g = new fabric.Group([rect, text], { originX: "left", originY: "top", lockScalingY: true, lockRotation: true, hasRotatingPoint: false });
        g.set({ left: x * K, top: y * K });
        g.setCoords();
        g.ib = { kind };
        g.set({ borderColor: TEAL, cornerColor: TEAL, cornerStyle: "circle", transparentCorners: false, cornerSize: 10 });
        return g;
    }

    function flowZones(def) {
        const t = def.flow.itemsTable, tot = def.flow.totals;
        const itemsBottom = t.yMm + ITEMS_ZONE_H;
        const totalsTop = itemsBottom + tot.gapMm;
        const zones = [
            zone("itemsTable", "Items table · overflows onto page 2+", t.xMm, t.yMm, t.widthMm, ITEMS_ZONE_H),
            zone("totals", "Totals", tot.xMm, totalsTop, tot.widthMm, tot.heightMm),
        ];
        if (def.flow.instalmentSchedule) {
            const s = def.flow.instalmentSchedule;
            zones.push(zone("schedule", "Instalment schedule", s.xMm, totalsTop + tot.heightMm + s.gapMm, s.widthMm, 24));
        }
        return zones;
    }

    function readGeometry(obj) {
        const el = obj.ib.el;
        if (el.type === "line") {
            const dx = el.x2Mm - el.xMm, dy = el.y2Mm - el.yMm;
            const l = obj.left / K, t = obj.top / K;
            const x = dx >= 0 ? l : l + Math.abs(dx), y = dy >= 0 ? t : t + Math.abs(dy);
            return { ...el, xMm: round(x), yMm: round(y), x2Mm: round(x + dx), y2Mm: round(y + dy), widthMm: round(Math.hypot(dx, dy)) };
        }
        const w = obj.width * Math.abs(obj.scaleX || 1), h = obj.height * Math.abs(obj.scaleY || 1);
        const c = obj.getCenterPoint();
        const next = {
            ...el,
            xMm: round((c.x - w / 2) / K), yMm: round((c.y - h / 2) / K),
            widthMm: round(w / K), heightMm: round(h / K),
            rotation: Math.round(obj.angle || 0),
        };
        if (el.type === "text") next.text = obj.text;
        return next;
    }

    function round(n) { return Math.round(n * 100) / 100; }

    function readFlow(def, zones, objects) {
        const flow = JSON.parse(JSON.stringify(def.flow));
        const items = zones.find(o => o.ib.kind === "itemsTable");
        const totals = zones.find(o => o.ib.kind === "totals");
        const sched = zones.find(o => o.ib.kind === "schedule");
        if (items) {
            flow.itemsTable.xMm = round(items.left / K);
            flow.itemsTable.yMm = round(items.top / K);
            flow.itemsTable.widthMm = round(items.getScaledWidth() / K);
        }
        const itemsBottom = flow.itemsTable.yMm + ITEMS_ZONE_H;
        if (totals) {
            flow.totals.xMm = round(totals.left / K);
            flow.totals.widthMm = round(totals.getScaledWidth() / K);
            flow.totals.gapMm = Math.max(0, round(totals.top / K - itemsBottom));
        }
        if (sched && flow.instalmentSchedule) {
            const totalsBottom = itemsBottom + flow.totals.gapMm + flow.totals.heightMm;
            flow.instalmentSchedule.xMm = round(sched.left / K);
            flow.instalmentSchedule.widthMm = round(sched.getScaledWidth() / K);
            flow.instalmentSchedule.gapMm = Math.max(0, round(sched.top / K - totalsBottom));
        }
        return flow;
    }

    function newElement(type, page) {
        const base = {
            id: newId(), type, xMm: 20, yMm: 20, widthMm: 60, heightMm: 10, rotation: 0, zIndex: 0,
            sizePt: 10, weight: 400, align: "left", color: "#0F172A",
            x2Mm: 0, y2Mm: 0, fit: "contain", opacity: 1 };
        switch (type) {
            case "text": return { ...base, text: "Text" };
            case "field": return { ...base, binding: "client.name" };
            case "shape": return { ...base, fill: "#99D5CF", heightMm: 20 };
            case "line": return { ...base, x2Mm: 120, y2Mm: 20, strokeWidthMm: 0.3, stroke: "#0F172A", widthMm: 0, heightMm: 0 };
            case "image": return { ...base, binding: "business.logo", widthMm: 30, heightMm: 20 };
            case "payQr": return { ...base, widthMm: 30, heightMm: 30 };
            case "signature": return { ...base, text: "Signature", widthMm: 60, heightMm: 15 };
            case "notes": return { ...base, type: "field", binding: "document.notes", widthMm: 80, heightMm: 20 };
            case "terms": return { ...base, type: "field", binding: "document.terms", widthMm: 80, heightMm: 25, sizePt: 8 };
            case "bank": return { ...base, type: "field", binding: "business.bankDetails", widthMm: 80, heightMm: 25 };
            case "business": return { ...base, type: "field", binding: "business.address", widthMm: 60, heightMm: 20 };
            case "clientDetails": return { ...base, type: "field", binding: "client.address", widthMm: 60, heightMm: 20 };
            case "status": return { ...base, type: "field", binding: "document.status", widthMm: 40, heightMm: 12, sizePt: 18, weight: 700, uppercase: true, align: "center" };
            case "stamp": return { ...base, type: "field", binding: "document.status", xMm: 40, yMm: 120, widthMm: 130, heightMm: 40, sizePt: 72, weight: 800, uppercase: true, align: "center", rotation: -30, opacity: 0.15 };
            case "footer":
            default: return base;
        }
    }

    window.IbTemplateMapper = { setTokens, K, newId, labelFor, elementToFabric, flowZones, readGeometry, readFlow, newElement };
})();
