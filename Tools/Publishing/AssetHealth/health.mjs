const $ = id => document.getElementById(id);
const assetOrigin = "https://assets.battlecities.com";
const rows = new Map();
const history = [];
let config, current, busy = false, repeatTimer, fullController;
const round = n => Math.round(n);
const size = n => (n / 1000000).toFixed(1) + " MB";
const hex = async bytes => Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256", bytes)), b => b.toString(16).padStart(2, "0")).join("");
const clean = result => { const { data, ...info } = result; return info; };
function ensure(condition, message, result) {
  if (condition) return;
  const error = new Error(message); error.diagnostics = clean(result); throw error;
}
function json(result) {
  try { return JSON.parse(new TextDecoder().decode(result.data)); }
  catch (error) { error.diagnostics = clean(result); throw error; }
}
function validate(value) {
  const a = value?.artifact, s = value?.sample;
  const url = new URL(a?.url);
  if (value.schema !== 1 || url.origin !== assetOrigin || !/^\/android\/art\/[a-f0-9]{64}\.bundle$/.test(url.pathname)
    || url.search || url.hash || url.username || url.password || !/^[a-f0-9]{64}$/.test(a.sha256)
    || !url.pathname.endsWith("/" + a.sha256 + ".bundle") || !Number.isSafeInteger(a.bytes) || a.bytes < 1 || a.bytes > 1073741824
    || !Number.isSafeInteger(s?.bytes) || s.bytes !== Math.min(65536, a.bytes) || !/^[a-f0-9]{64}$/.test(s.sha256)
    || !/^[a-f0-9]{64}$/.test(value.probeSha256)) throw new Error("The health configuration is invalid.");
  return value;
}
async function get(url, { limit = 262144, range, timeout = 12000, signal, onProgress } = {}) {
  const controller = new AbortController();
  let timedOut = false, reader;
  const cancel = () => controller.abort();
  const timer = setTimeout(() => { timedOut = true; controller.abort(); }, timeout);
  if (signal?.aborted) cancel();
  signal?.addEventListener("abort", cancel, { once: true });
  const start = performance.now();
  const stats = { url: new URL(url, location.href).href, http: null, bytes: 0 };
  try {
    const target = new URL(url, location.href);
    target.searchParams.set("healthcheck", Date.now() + "-" + crypto.randomUUID());
    const response = await fetch(target, { cache: "no-store", redirect: "error", signal: controller.signal,
      headers: range ? { Range: range } : {} });
    stats.http = response.status;
    stats.headersMs = round(performance.now() - start);
    stats.headers = Object.fromEntries(["content-type", "content-length", "content-range", "etag", "cf-cache-status", "cf-ray"]
      .map(name => [name, response.headers.get(name)]).filter(([, value]) => value !== null));
    if (!response.ok) throw new Error("HTTP " + response.status + ": the host responded" + (response.status === 404 ? ", but this file is missing" : " with an error") + ".");
    if (range && response.status !== 206) {
      const error = new Error("HTTP " + response.status + ": the file is reachable, but the server ignored the byte range. The sample was cancelled to avoid a full download.");
      error.level = "warn";
      throw error;
    }
    reader = response.body.getReader();
    const chunks = [];
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      stats.bytes += value.byteLength;
      if (stats.bytes > limit) throw new Error("The response exceeded the expected size; download stopped.");
      chunks.push(value);
      onProgress?.(stats.bytes, performance.now() - start);
    }
    const data = new Uint8Array(stats.bytes);
    let offset = 0;
    for (const chunk of chunks) { data.set(chunk, offset); offset += chunk.byteLength; }
    stats.totalMs = round(performance.now() - start);
    return { ...stats, data };
  } catch (error) {
    if (timedOut) error = new Error("No completed response within " + timeout / 1000 + " seconds.");
    else if (signal?.aborted) { error = new Error("Cancelled by you."); error.level = "warn"; }
    else if (error instanceof TypeError) error = new Error("No readable HTTP response. DNS, TLS, connection or browser-policy failure is possible; this browser cannot identify which one.");
    error.diagnostics = { ...stats, totalMs: round(performance.now() - start) };
    throw error;
  } finally {
    clearTimeout(timer);
    signal?.removeEventListener("abort", cancel);
    if (reader) await reader.cancel().catch(() => {});
    controller.abort();
  }
}
function row(id, title) {
  const root = document.createElement("article"); root.className = "check";
  const badge = document.createElement("span"); badge.className = "badge"; badge.textContent = "WAITING";
  const content = document.createElement("div");
  const heading = document.createElement("h2"); heading.textContent = title;
  const detail = document.createElement("p");
  const timing = document.createElement("p"); timing.className = "timing";
  content.append(heading, detail, timing); root.append(badge, content); $("checks").append(root);
  rows.set(id, { badge, detail, timing });
}
function paint(id, state, detail, result = {}) {
  const r = rows.get(id);
  r.badge.dataset.state = state; r.badge.textContent = state.toUpperCase();
  r.detail.textContent = detail;
  r.timing.textContent = result.http !== undefined
    ? (result.http ? "HTTP " + result.http : "No HTTP response") + " · " + (result.headersMs === undefined ? "" : "headers " + result.headersMs + " ms · ") + (result.totalMs ?? 0) + " ms total · " + (result.bytes ?? 0) + " bytes"
    : "";
}
function report() {
  $("report").textContent = JSON.stringify({ pageVersion: 1, runs: history }, null, 2);
  $("copy").disabled = !history.length;
}
async function check(id, action, run) {
  paint(id, "checking", "Requesting a fresh response…");
  try {
    const result = await action();
    const item = { id, state: "pass", ...clean(result) };
    run.checks.push(item); paint(id, "pass", result.summary, result);
  } catch (error) {
    const item = { id, state: error.level || "fail", message: error.message, ...error.diagnostics };
    run.checks.push(item); paint(id, item.state, error.message, item);
  }
}
function schedule() {
  clearTimeout(repeatTimer);
  if ($("repeat").checked && !busy) repeatTimer = setTimeout(runChecks, 15000);
}
function environment() {
  return { browser: navigator.userAgent, onlineHint: navigator.onLine,
    connectionHint: navigator.connection?.effectiveType ?? null, page: location.origin + location.pathname };
}
async function runChecks() {
  if (busy) return;
  busy = true; clearTimeout(repeatTimer); $("run").disabled = $("full").disabled = true;
  current = { startedAt: new Date().toISOString(), environment: environment(), checks: [] };
  history.push(current); if (history.length > 20) history.shift();
  $("summary").textContent = "Checking asset delivery…"; $("description").textContent = "Quick tests use less than 1 MB. No wallet connection is needed.";
  $("summary").parentElement.removeAttribute("data-state");
  $("checks").replaceChildren(); rows.clear();
  row("host", "Asset host"); row("catalog", "Current Android asset"); row("sample", "Game file sample"); row("screens", "Android screen updates");
  try {
    const info = await get("/health/config.json");
    config = validate(json(info)); current.artifact = config.artifact;
    $("release").textContent = "Android " + config.artifact.versionName + " · code " + config.artifact.versionCode + " · " + size(config.artifact.bytes) + " · catalog updated " + new Date(config.publishedAt).toLocaleString();
    $("full").textContent = "Verify full download (" + size(config.artifact.bytes) + ")";
    paint("catalog", "pass", config.artifact.sha256 + ".bundle", info);
    current.checks.push({ id: "catalog", state: "pass", ...clean(info) });
    await Promise.all([
      check("host", async () => {
        const result = await get("/health/ping.json", { limit: 4096 });
        ensure(await hex(result.data) === config.probeSha256, "The small probe checksum did not match.", result);
        return { ...result, summary: "The asset host returned the expected small file." };
      }, current),
      check("sample", async () => {
        const result = await get(config.artifact.url, { range: "bytes=0-" + (config.sample.bytes - 1), limit: config.sample.bytes, timeout: 20000 });
        const expectedRange = "bytes 0-" + (config.sample.bytes - 1) + "/" + config.artifact.bytes;
        ensure(result.headers["content-range"] === expectedRange && result.bytes === config.sample.bytes, "The file size or byte range differs from the APK's expected asset.", result);
        ensure(await hex(result.data) === config.sample.sha256, "The game file sample checksum did not match.", result);
        return { ...result, summary: config.sample.bytes / 1024 + " KiB downloaded; sample checksum and total file size match. Full-file integrity is not checked here." };
      }, current),
      check("screens", async () => {
        const result = await get("/android/screens/v1/latest.json");
        json(result);
        return { ...result, summary: "Screen-update metadata is reachable and contains JSON. This check does not validate its signature." };
      }, current)
    ]);
    const failed = current.checks.filter(r => r.state !== "pass");
    $("summary").parentElement.dataset.state = failed.length ? "fail" : "pass";
    $("summary").textContent = failed.length ? failed.length + " check" + (failed.length === 1 ? "" : "s") + " need attention" : "Quick checks passed";
    $("description").textContent = failed.length
      ? "Copy the report while the issue is happening. Open the small probe directly for the browser's connection error."
      : "This browser can reach the asset host and the expected game file right now. Use the full download test to verify the entire file.";
  } catch (error) {
    current.checks.push({ id: "catalog", state: "fail", message: error.message, ...error.diagnostics });
    paint("catalog", "fail", error.message, error.diagnostics);
    for (const id of ["host", "sample", "screens"]) paint(id, "skipped", "The catalog could not be loaded; retry the checks.");
    $("summary").textContent = "Could not start the checks"; $("description").textContent = error.message;
    $("summary").parentElement.dataset.state = "fail"; config = null;
  } finally {
    current.finishedAt = new Date().toISOString();
    busy = false; $("run").disabled = false; $("full").disabled = !config;
    report(); schedule();
  }
}
async function fullDownload() {
  if (busy || !config) return;
  busy = true; clearTimeout(repeatTimer); $("run").disabled = $("full").disabled = true;
  $("cancel").hidden = $("progress").hidden = false; $("progress").value = 0;
  fullController = new AbortController();
  const run = { startedAt: new Date().toISOString(), kind: "full-download", environment: environment(), artifact: config.artifact, checks: [] };
  history.push(run); if (history.length > 20) history.shift();
  try {
    const result = await get(config.artifact.url, { limit: config.artifact.bytes, timeout: 180000, signal: fullController.signal,
      onProgress(bytes) { $("progress").value = bytes / config.artifact.bytes; $("full-status").textContent = size(bytes) + " / " + size(config.artifact.bytes); } });
    $("full-status").textContent = "Checking the complete file's SHA-256…";
    ensure(result.bytes === config.artifact.bytes, "The complete file size did not match.", result);
    ensure(await hex(result.data) === config.artifact.sha256, "The complete file checksum did not match.", result);
    const message = "Verified " + size(result.bytes) + " in " + (result.totalMs / 1000).toFixed(1) + " seconds. Complete SHA-256 matches the APK manifest.";
    run.checks.push({ id: "full", state: "pass", summary: message, sha256: config.artifact.sha256, ...clean(result) });
    $("full-status").textContent = message;
  } catch (error) {
    run.checks.push({ id: "full", state: error.level || "fail", message: error.message, ...error.diagnostics });
    $("full-status").textContent = error.message;
  } finally {
    run.finishedAt = new Date().toISOString(); fullController = null; busy = false;
    $("cancel").hidden = true; $("run").disabled = $("full").disabled = false;
    report(); schedule();
  }
}
$("run").addEventListener("click", runChecks);
$("full").addEventListener("click", fullDownload);
$("cancel").addEventListener("click", () => fullController?.abort());
$("repeat").addEventListener("change", schedule);
$("copy").addEventListener("click", async () => {
  try { await navigator.clipboard.writeText($("report").textContent); $("copy-status").textContent = "Report copied. It includes recent checks and your browser version."; }
  catch { $("report").parentElement.open = true; $("copy-status").textContent = "Clipboard access was unavailable. Select and copy the report below."; }
});
window.addEventListener("offline", () => { $("description").textContent = "The browser reports that the network is offline. Run the checks again after reconnecting."; });
runChecks();
