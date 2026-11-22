/* Desktop-only Sales Support navigation regression, no network/DB or browser required.
 * Exercise the actual bundled route adapters in a minimal DOM harness.
 * Repro: Your Customer -> Add Customer / Customer Map -> Your Customer.
 */
"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const root = path.resolve(__dirname, "..", "build", "web", "big-brother-dashboard");
const legacy = fs.readFileSync(path.join(root, "legacy-dashboard-v3.40.html"), "utf8");
assert.match(legacy, /frame\.dataset\.module === moduleName/);
assert.match(legacy, /const sameModule\s*=/);

function testCustomerRoute(file, apiName, desiredRoute, expectedView) {
  const source = fs.readFileSync(path.join(root, file), "utf8");
  const classList = { add() {}, remove() {} };
  const elements = {};
  const submenu = {
    classList,
    appendChild(el) { elements[el.id] = el; el.parentNode = this; }
  };
  const frame = {
    dataset: { module: "sales-support-your-customer" },
    src: "https://app.bigbrother.test/big-brother-sales-support/index.html?view=your-customer"
  };
  const home = { hidden: false };
  const workspace = { hidden: true };
  elements.salesSupportSubmenu = submenu;
  elements.dashboardHome = home;
  elements.moduleWorkspace = workspace;
  elements.moduleFrame = frame;
  elements.salesSupportMenuButton = {
    classList,
    setAttribute() {},
    querySelector() { return null; }
  };
  const historyChanges = [];
  const dashboardWindow = {
    URL,
    BBDashboardAdapter: { getProfile() { return { user: { isAdmin: true } }; } },
    addEventListener() {}
  };
  const document = {
    readyState: "loading",
    getElementById(id) { return elements[id] || null; },
    querySelectorAll() { return []; },
    createElement() {
      return {
        style: {}, classList, id: null, hidden: false,
        addEventListener() {}, setAttribute() {}
      };
    },
    addEventListener() {}
  };
  const location = {
    href: "https://app.bigbrother.test/big-brother-dashboard/index.html?module=sales-support-your-customer"
  };
  vm.runInNewContext(source, {
    window: dashboardWindow, document, location,
    history: {
      replaceState(...args) { historyChanges.push(args); }
    },
    URL, URLSearchParams, setTimeout() {}
  }, { filename: file, timeout: 2000 });
  const api = dashboardWindow[apiName];
  assert.ok(api, `Missing ${apiName}`);
  assert.equal(api.open(true), true);
  assert.equal(home.hidden, true);
  assert.equal(workspace.hidden, false);
  assert.match(frame.src, new RegExp(expectedView));
  assert.equal(frame.dataset.module, desiredRoute,
    `${file} did not update Dashboard's active module marker`);
  assert.equal(historyChanges.length, 1);

  // The actual Dashboard V3.40 sameModule guard previously returned true,
  // preventing the Your Customer iframe from reopening. It must now be false.
  const sameModule = frame.dataset.module === "sales-support-your-customer" &&
    Boolean(frame.src) && frame.src !== "about:blank";
  assert.equal(sameModule, false,
    `Returning to Your Customer would be incorrectly skipped after ${desiredRoute}`);
  console.log(`PASS Your Customer -> ${desiredRoute} -> Your Customer`);
}

testCustomerRoute(
  "supabase-sales-support-add-customer.js", "BBAddCustomer",
  "sales-support-add-customer", "add-customer\\.html"
);
testCustomerRoute(
  "supabase-sales-support-customer-map.js", "BBCustomerMap",
  "sales-support-customer-map", "sales-support-customer-map\\.html"
);
console.log("Sales Support Desktop navigation regression passed without accounting data access.");
