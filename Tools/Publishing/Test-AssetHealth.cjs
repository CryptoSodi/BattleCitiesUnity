const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const { chromium } = require("playwright");
const root = path.join(__dirname, "AssetHealth");
const output = path.resolve(__dirname, "../../Builds/AssetHealthChecks");
const origin = "https://assets.battlecities.com";
const digest = bytes => crypto.createHash("sha256").update(bytes).digest("hex");
const bytes = Buffer.alloc(131072, 49); bytes[65536] = 89;
const probe = fs.readFileSync(path.join(root, "ping.json"));
const config = {
  schema: 1, publishedAt: new Date().toISOString(),
  artifact: { url: origin + "/android/art/" + digest(bytes) + ".bundle", sha256: digest(bytes), bytes: bytes.length, versionName: "1.0.9", versionCode: 10 },
  sample: { bytes: 65536, sha256: digest(bytes.subarray(0, 65536)) }, probeSha256: digest(probe)
};
let browser;
async function fixture(mode = "healthy", viewport = { width: 1440, height: 1000 }) {
  const context = await browser.newContext({ viewport });
  const page = await context.newPage();
  const errors = [];
  page.on("pageerror", error => errors.push(error.message));
  let broken = false;
  await page.addInitScript(() => {
    Object.defineProperty(navigator, "clipboard", { value: { writeText: async text => { window.copiedReport = text; } } });
  });
  await page.route(origin + "/**", async route => {
    const url = new URL(route.request().url());
    const file = url.pathname;
    if (broken && (file.endsWith(".json") || file.endsWith(".bundle"))) return route.abort("namenotresolved");
    if (file === "/health.html") return route.fulfill({ contentType: "text/html", body: fs.readFileSync(path.join(root, "index.html")) });
    if (file === "/health/health.css") return route.fulfill({ contentType: "text/css", body: fs.readFileSync(path.join(root, "health.css")) });
    if (file === "/health/health.mjs") return route.fulfill({ contentType: "text/javascript", body: fs.readFileSync(path.join(root, "health.mjs")) });
    if (file.endsWith(".ttf")) return route.fulfill({ body: Buffer.alloc(0) });
    if (file === "/health/config.json") return route.fulfill({ json: config });
    if (file === "/health/ping.json") return route.fulfill({ contentType: "application/json", body: probe });
    if (file === "/android/screens/v1/latest.json") return route.fulfill({ json: { schema: 1 } });
    if (file.endsWith(".bundle")) {
      const range = route.request().headers().range;
      if (range) {
        if (mode === "missing") return route.fulfill({ status: 404, body: "Not found" });
        if (mode === "no-range") return route.fulfill({ status: 200, body: bytes });
        const sample = Buffer.from(bytes.subarray(0, 65536));
        if (mode === "corrupt-sample") sample[0] ^= 255;
        return route.fulfill({ status: 206, headers: { "Content-Range": "bytes 0-65535/" + bytes.length }, body: sample });
      }
      if (mode === "cancel") await new Promise(resolve => setTimeout(resolve, 1500));
      const body = Buffer.from(mode === "truncated" ? bytes.subarray(0, bytes.length - 1) : bytes);
      if (mode === "corrupt-full") body[0] ^= 255;
      return route.fulfill({ body }).catch(() => {});
    }
    return route.fulfill({ status: 404 });
  });
  await page.goto(origin + "/health.html");
  await page.waitForFunction(() => !document.getElementById("run").disabled);
  assert.deepEqual(errors, []);
  const latest = () => page.locator("#report").textContent().then(text => JSON.parse(text).runs.at(-1));
  return { page, context, latest, breakNetwork: () => { broken = true; } };
}
async function test(name, action) {
  await action();
  console.log("PASS " + name);
}
(async () => {
  fs.mkdirSync(output, { recursive: true });
  browser = await chromium.launch({ headless: true, ...(process.env.PLAYWRIGHT_CHROMIUM_CHANNEL ? { channel: process.env.PLAYWRIGHT_CHROMIUM_CHANNEL } : {}) });
  if (process.argv.includes("--live")) {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
    const page = await context.newPage();
    const errors = [];
    page.on("pageerror", error => errors.push(error.message));
    await page.goto(origin + "/health.html?verify=" + Date.now());
    await page.waitForFunction(() => !document.getElementById("run").disabled, null, { timeout: 45000 });
    let report = JSON.parse(await page.locator("#report").textContent());
    assert(report.runs.at(-1).checks.every(check => check.state === "pass"), JSON.stringify(report));
    console.log("PASS live browser quick checks");
    await page.locator("#full").click();
    await page.waitForFunction(() => !document.getElementById("run").disabled, null, { timeout: 190000 });
    report = JSON.parse(await page.locator("#report").textContent());
    assert.equal(report.runs.at(-1).checks[0].state, "pass", JSON.stringify(report));
    assert.deepEqual(errors, []);
    await page.screenshot({ path: path.join(output, "live-desktop.png"), fullPage: true });
    fs.writeFileSync(path.join(output, "live-report.json"), JSON.stringify(report, null, 2));
    console.log("PASS live full download: " + report.runs.at(-1).checks[0].summary);
    await context.close(); await browser.close(); return;
  }
  await test("quick checks, copyable report, desktop layout", async () => {
    const f = await fixture();
    assert((await f.latest()).checks.every(check => check.state === "pass"));
    await f.page.locator("#copy").click();
    assert.equal((await f.page.evaluate(() => JSON.parse(window.copiedReport).runs[0].artifact.versionCode)), 10);
    await f.page.screenshot({ path: path.join(output, "desktop.png"), fullPage: true });
    await f.context.close();
  });
  await test("fresh failed retry cannot reuse an earlier successful result", async () => {
    const f = await fixture(); f.breakNetwork();
    await f.page.locator("#run").click();
    await f.page.waitForFunction(() => !document.getElementById("run").disabled);
    const run = await f.latest();
    assert.equal(run.checks[0].state, "fail"); assert.equal(run.checks[0].http, null);
    assert.match(run.checks[0].message, /DNS, TLS, connection or browser-policy/);
    assert.equal(await f.page.locator("#copy").isEnabled(), true);
    await f.context.close();
  });
  for (const [mode, state, message] of [
    ["missing", "fail", /HTTP 404/], ["corrupt-sample", "fail", /checksum did not match/],
    ["no-range", "warn", /ignored the byte range/]
  ]) await test(mode + " sample is reported honestly", async () => {
    const f = await fixture(mode);
    const sample = (await f.latest()).checks.find(check => check.id === "sample");
    assert.equal(sample.state, state); assert.match(sample.message, message);
    await f.context.close();
  });
  for (const [mode, state, message] of [
    ["healthy", "pass", /Complete SHA-256 matches/], ["truncated", "fail", /file size did not match/],
    ["corrupt-full", "fail", /checksum did not match/], ["cancel", "warn", /Cancelled by you/]
  ]) await test(mode + " complete download", async () => {
    const f = await fixture(mode);
    await f.page.locator("#full").click();
    if (mode === "cancel") await f.page.locator("#cancel").click();
    await f.page.waitForFunction(() => !document.getElementById("run").disabled);
    const result = (await f.latest()).checks[0];
    assert.equal(result.state, state); assert.match(result.summary || result.message, message);
    await f.context.close();
  });
  await test("narrow browser layout has no horizontal overflow", async () => {
    const f = await fixture("healthy", { width: 390, height: 844 });
    assert(await f.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
    await f.page.screenshot({ path: path.join(output, "narrow.png"), fullPage: true });
    await f.context.close();
  });
  await browser.close();
})().catch(async error => { console.error(error); await browser?.close(); process.exitCode = 1; });
