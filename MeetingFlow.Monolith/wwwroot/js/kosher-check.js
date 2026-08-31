(function (global) {
    "use strict";

    const MAX_DISHES = 10;
    const MAX_HISTORY_ENTRIES = 20;
    const HISTORY_KEY = "meetingflow.kosher-check.history.v1";
    const KNOWN_STATUSES = new Set([
        "KOSHER",
        "NOT_KOSHER",
        "CONDITIONAL",
        "INVALID_INPUT"
    ]);

    function canAddDish(currentCount) {
        return currentCount < MAX_DISHES;
    }

    function addHistoryEntry(existingEntries, newEntry) {
        const safeEntries = Array.isArray(existingEntries) ? existingEntries : [];
        return [newEntry, ...safeEntries].slice(0, MAX_HISTORY_ENTRIES);
    }

    function isKnownStatus(status) {
        return KNOWN_STATUSES.has(status);
    }

    function clearCurrentResults(resultsSection, resultsBody) {
        resultsSection.hidden = true;
        resultsBody.replaceChildren();
    }

    function sanitizeHistory(value) {
        if (!Array.isArray(value)) {
            return [];
        }

        return value.filter(entry =>
            entry &&
            typeof entry.id === "string" &&
            typeof entry.createdAt === "string" &&
            !Number.isNaN(Date.parse(entry.createdAt)) &&
            Array.isArray(entry.results) &&
            entry.results.length >= 1 &&
            entry.results.length <= MAX_DISHES &&
            entry.results.every(result =>
                result &&
                typeof result.dish === "string" &&
                isKnownStatus(result.status) &&
                typeof result.explanation === "string"))
            .slice(0, MAX_HISTORY_ENTRIES);
    }

    const publicApi = {
        addHistoryEntry,
        canAddDish,
        clearCurrentResults,
        isKnownStatus,
        sanitizeHistory
    };

    if (typeof module !== "undefined" && module.exports) {
        module.exports = publicApi;
    }

    if (!global.document) {
        return;
    }

    global.document.addEventListener("DOMContentLoaded", () => {
        const form = global.document.getElementById("kosher-check-form");
        if (!form) {
            return;
        }

        const dishFields = global.document.getElementById("dish-fields");
        const addDishButton = global.document.getElementById("add-dish");
        const loading = global.document.getElementById("kosher-loading");
        const errorBox = global.document.getElementById("kosher-error");
        const resultsSection = global.document.getElementById("kosher-results");
        const resultsBody = global.document.getElementById("kosher-results-body");
        const historySection = global.document.getElementById("kosher-history");
        const historyList = global.document.getElementById("kosher-history-list");

        function getRows() {
            return Array.from(dishFields.querySelectorAll(".dish-row"));
        }

        function updateRows() {
            const rows = getRows();
            rows.forEach((row, index) => {
                const label = row.querySelector("label");
                const input = row.querySelector("input");
                const removeButton = row.querySelector(".remove-dish");
                const inputId = `dish-${index + 1}`;

                label.textContent = `Dish ${index + 1}`;
                label.htmlFor = inputId;
                input.id = inputId;
                removeButton.hidden = rows.length === 1;
                removeButton.setAttribute("aria-label", `Remove dish ${index + 1}`);
            });

            addDishButton.disabled = !canAddDish(rows.length);
        }

        function createDishRow() {
            const row = global.document.createElement("div");
            row.className = "dish-row";

            const field = global.document.createElement("div");
            field.className = "field dish-input";

            const label = global.document.createElement("label");
            const input = global.document.createElement("input");
            input.type = "text";
            input.name = "Dishes";
            input.maxLength = 500;
            input.required = true;
            input.autocomplete = "off";
            input.placeholder = "Example: beef burger with plant-based cheese";

            const removeButton = global.document.createElement("button");
            removeButton.type = "button";
            removeButton.className = "remove-dish";
            removeButton.textContent = "Remove";
            removeButton.addEventListener("click", () => {
                row.remove();
                updateRows();
            });

            field.append(label, input);
            row.append(field, removeButton);
            return row;
        }

        function setBusy(isBusy) {
            form.setAttribute("aria-busy", String(isBusy));
            loading.hidden = !isBusy;
            form.querySelectorAll("input, button").forEach(control => {
                control.disabled = isBusy;
            });

            if (!isBusy) {
                updateRows();
            }
        }

        function showError(message) {
            errorBox.textContent = message;
            errorBox.hidden = false;
        }

        function clearError() {
            errorBox.textContent = "";
            errorBox.hidden = true;
        }

        function appendResultRows(target, results) {
            results.forEach(result => {
                if (!isKnownStatus(result.status)) {
                    throw new Error("The server returned an unknown status.");
                }

                const row = global.document.createElement("tr");
                const dishCell = global.document.createElement("td");
                const statusCell = global.document.createElement("td");
                const explanationCell = global.document.createElement("td");
                const statusBadge = global.document.createElement("span");

                dishCell.textContent = result.dish;
                statusBadge.textContent = result.status;
                statusBadge.className = `badge kosher-status kosher-status-${result.status.toLowerCase().replaceAll("_", "-")}`;
                statusCell.append(statusBadge);
                explanationCell.textContent = result.explanation;
                row.append(dishCell, statusCell, explanationCell);
                target.append(row);
            });
        }

        function renderResults(results) {
            resultsBody.replaceChildren();
            appendResultRows(resultsBody, results);
            resultsSection.hidden = false;
            resultsSection.scrollIntoView({ behavior: "smooth", block: "start" });
        }

        function readHistory() {
            try {
                const stored = global.localStorage.getItem(HISTORY_KEY);
                if (!stored) {
                    return [];
                }

                return sanitizeHistory(JSON.parse(stored));
            } catch {
                return [];
            }
        }

        function writeHistory(entries) {
            try {
                global.localStorage.setItem(HISTORY_KEY, JSON.stringify(entries));
            } catch {
                // History is optional. The current assessment remains usable.
            }
        }

        function renderHistory(entries) {
            historyList.replaceChildren();
            historySection.hidden = entries.length === 0;

            entries.forEach(entry => {
                const details = global.document.createElement("details");
                details.className = "history-entry";
                const summary = global.document.createElement("summary");
                const date = new Date(entry.createdAt);
                summary.textContent = `${date.toLocaleString()} — ${entry.results.length} dish${entry.results.length === 1 ? "" : "es"}`;

                const tableWrapper = global.document.createElement("div");
                tableWrapper.className = "table-scroll";
                const table = global.document.createElement("table");
                const head = global.document.createElement("thead");
                const headRow = global.document.createElement("tr");
                ["Dish", "Status", "Explanation"].forEach(text => {
                    const heading = global.document.createElement("th");
                    heading.scope = "col";
                    heading.textContent = text;
                    headRow.append(heading);
                });
                head.append(headRow);

                const body = global.document.createElement("tbody");
                appendResultRows(body, entry.results);
                table.append(head, body);
                tableWrapper.append(table);
                details.append(summary, tableWrapper);
                historyList.append(details);
            });
        }

        addDishButton.addEventListener("click", () => {
            if (!canAddDish(getRows().length)) {
                return;
            }

            const row = createDishRow();
            dishFields.append(row);
            updateRows();
            row.querySelector("input").focus();
        });

        form.addEventListener("submit", async event => {
            event.preventDefault();
            clearError();

            if (!form.reportValidity()) {
                return;
            }

            clearCurrentResults(resultsSection, resultsBody);
            const formData = new FormData(form);
            setBusy(true);

            try {
                const response = await global.fetch(form.action, {
                    method: "POST",
                    body: formData,
                    headers: {
                        Accept: "application/json",
                        "X-Requested-With": "XMLHttpRequest"
                    }
                });
                const payload = await response.json();

                if (!response.ok) {
                    const message = Array.isArray(payload.errors)
                        ? payload.errors.join(" ")
                        : payload.error;
                    throw new Error(message || "Kosher checking is currently unavailable. Please try again later.");
                }

                if (!Array.isArray(payload.results)) {
                    throw new Error("Kosher checking is currently unavailable. Please try again later.");
                }

                renderResults(payload.results);
                const entry = {
                    id: global.crypto?.randomUUID?.() ?? String(Date.now()),
                    createdAt: new Date().toISOString(),
                    results: payload.results
                };
                const history = addHistoryEntry(readHistory(), entry);
                writeHistory(history);
                renderHistory(history);
            } catch (error) {
                showError(error instanceof Error
                    ? error.message
                    : "Kosher checking is currently unavailable. Please try again later.");
            } finally {
                setBusy(false);
            }
        });

        updateRows();
        renderHistory(readHistory());
    });
})(typeof window !== "undefined" ? window : globalThis);
