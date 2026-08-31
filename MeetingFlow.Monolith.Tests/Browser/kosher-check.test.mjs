import assert from "node:assert/strict";
import { createRequire } from "node:module";
import test from "node:test";

const require = createRequire(import.meta.url);
const {
    addHistoryEntry,
    canAddDish,
    clearCurrentResults,
    isKnownStatus,
    sanitizeHistory
} = require("../../MeetingFlow.Monolith/wwwroot/js/kosher-check.js");

test("canAddDish allows up to ten dish fields", () => {
    assert.equal(canAddDish(1), true);
    assert.equal(canAddDish(9), true);
    assert.equal(canAddDish(10), false);
});

test("addHistoryEntry keeps newest entries first and caps history at twenty", () => {
    const existing = Array.from({ length: 20 }, (_, index) => ({ id: `old-${index}` }));
    const next = addHistoryEntry(existing, { id: "new" });

    assert.equal(next.length, 20);
    assert.equal(next[0].id, "new");
    assert.equal(next.some(entry => entry.id === "old-19"), false);
});

test("isKnownStatus accepts only the four server statuses", () => {
    assert.equal(isKnownStatus("KOSHER"), true);
    assert.equal(isKnownStatus("NOT_KOSHER"), true);
    assert.equal(isKnownStatus("CONDITIONAL"), true);
    assert.equal(isKnownStatus("INVALID_INPUT"), true);
    assert.equal(isKnownStatus("MAYBE"), false);
});

test("sanitizeHistory drops malformed browser data", () => {
    const valid = {
        id: "valid",
        createdAt: "2026-07-29T10:00:00.000Z",
        results: [
            {
                dish: "Falafel",
                status: "KOSHER",
                explanation: "The description is sufficient."
            }
        ]
    };
    const malformed = {
        id: "broken",
        createdAt: "not-a-date",
        results: [{ dish: "Shrimp", status: "MAYBE", explanation: null }]
    };

    assert.deepEqual(sanitizeHistory([malformed, valid]), [valid]);
    assert.deepEqual(sanitizeHistory({ not: "an array" }), []);
});

test("clearCurrentResults hides stale output before a new check", () => {
    const section = { hidden: false };
    let cleared = false;
    const body = {
        replaceChildren() {
            cleared = true;
        }
    };

    clearCurrentResults(section, body);

    assert.equal(section.hidden, true);
    assert.equal(cleared, true);
});
