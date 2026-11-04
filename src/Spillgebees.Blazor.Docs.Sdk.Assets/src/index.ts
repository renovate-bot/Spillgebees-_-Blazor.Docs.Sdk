import "./styles/docs-sdk.scss";
import hljs from "highlight.js/lib/core";

// Import only the languages we need (tree-shakeable)
import csharp from "highlight.js/lib/languages/csharp";
import xml from "highlight.js/lib/languages/xml";
import json from "highlight.js/lib/languages/json";
import css from "highlight.js/lib/languages/css";
import javascript from "highlight.js/lib/languages/javascript";
import bash from "highlight.js/lib/languages/bash";

// @ts-ignore - no types available for the Razor plugin
import razor from "highlightjs-cshtml-razor";

hljs.registerLanguage("csharp", csharp);
hljs.registerLanguage("cs", csharp);
hljs.registerLanguage("xml", xml);
hljs.registerLanguage("json", json);
hljs.registerLanguage("css", css);
hljs.registerLanguage("javascript", javascript);
hljs.registerLanguage("js", javascript);
hljs.registerLanguage("bash", bash);
hljs.registerLanguage("shell", bash);
hljs.registerLanguage("cshtml-razor", razor);
hljs.registerLanguage("razor", razor);

// ── Theme + colour scheme ────────────────────────────────────────────────── //
//
// Two independent axes:
//   data-docs-theme   the palette/type family ("refined", "terminal", "glory-to-mankind", custom)
//   data-docs-scheme  the resolved colour scheme ("light" | "dark")
//
// The scheme *preference* may additionally be "system", in which case the
// resolved scheme follows the OS and updates live.

const SCHEME_KEY = "spillgebees-docs-scheme";
const THEME_KEY = "spillgebees-docs-theme";

type SchemePreference = "system" | "light" | "dark";

let mediaQuery: MediaQueryList | null = null;

function readStorage(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    // storage can throw in private modes / sandboxed iframes
    return null;
  }
}

function writeStorage(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // preference simply won't persist; the current page still applies it
  }
}

function prefersDark(): boolean {
  return !window.matchMedia?.("(prefers-color-scheme: light)").matches;
}

function resolveScheme(preference: SchemePreference): "light" | "dark" {
  if (preference === "light" || preference === "dark") {
    return preference;
  }
  return prefersDark() ? "dark" : "light";
}

function getSchemePreference(): SchemePreference {
  const stored = readStorage(SCHEME_KEY);
  return stored === "light" || stored === "dark" || stored === "system" ? stored : "system";
}

function applyScheme(preference: SchemePreference): void {
  document.documentElement.dataset.docsScheme = resolveScheme(preference);
}

function setSchemePreference(preference: SchemePreference): void {
  writeStorage(SCHEME_KEY, preference);
  applyScheme(preference);
}

/** Cycles system → light → dark → system, returning the new preference. */
function cycleSchemePreference(): SchemePreference {
  const next: Record<SchemePreference, SchemePreference> = {
    system: "light",
    light: "dark",
    dark: "system",
  };
  const preference = next[getSchemePreference()];
  setSchemePreference(preference);
  return preference;
}

function getResolvedScheme(): "light" | "dark" {
  return resolveScheme(getSchemePreference());
}

/**
 * Sets the theme family. Persisting is skipped when the value came from the
 * site's configured default rather than from a visitor action.
 */
function setTheme(theme: string, persist = true): void {
  document.documentElement.dataset.docsTheme = theme;
  if (persist) {
    writeStorage(THEME_KEY, theme);
  }
}

function getTheme(): string {
  return document.documentElement.dataset.docsTheme ?? "refined";
}

/**
 * Applies the site's theme. A stored visitor choice wins only when it is one of
 * `allowedThemes`; pass `null` to pin the site to `defaultTheme`. The storage
 * key is shared by every docs site on the origin, so a theme picked on one site
 * may not exist on the next.
 */
function initializeTheming(defaultTheme: string, allowedThemes: string[] | null): void {
  const storedTheme = readStorage(THEME_KEY);
  const visitorTheme = storedTheme && allowedThemes?.includes(storedTheme) ? storedTheme : null;
  setTheme(visitorTheme ?? defaultTheme, false);
  applyScheme(getSchemePreference());

  if (!mediaQuery && window.matchMedia) {
    mediaQuery = window.matchMedia("(prefers-color-scheme: light)");
    mediaQuery.addEventListener("change", () => {
      if (getSchemePreference() === "system") {
        applyScheme("system");
      }
    });
  }
}

// ── Clipboard ────────────────────────────────────────────────────────────── //

async function copyToClipboard(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    return false;
  }
}

// ── Syntax highlighting ──────────────────────────────────────────────────── //

/**
 * Wraps each source line in its own element so the gutter can measure it.
 *
 * Two things matter here:
 *
 * 1. This walks the highlighted DOM rather than splitting `innerHTML` on
 *    newlines. highlight.js *does* emit spans covering multiple lines (raw
 *    string literals, block comments), so a textual split tears those tags in
 *    half and the browser re-nests the fragments. When a newline is found
 *    mid-element the open ancestor chain is cloned into the new line.
 *
 * 2. The lines stay `display: inline` and are separated by real newline text
 *    nodes, so selecting the code by hand still copies with line breaks. Block
 *    spans would swallow the newlines and paste as one run-on line.
 */
function wrapLines(element: HTMLElement): void {
  const lines: HTMLElement[] = [];
  const chain: HTMLElement[] = [];
  let tip: HTMLElement;

  const startLine = (): void => {
    const line = document.createElement("span");
    line.className = "docs-code-line";
    lines.push(line);

    tip = line;
    for (const ancestor of chain) {
      const clone = ancestor.cloneNode(false) as HTMLElement;
      tip.append(clone);
      tip = clone;
    }
  };

  const visit = (node: Node): void => {
    if (node.nodeType === Node.TEXT_NODE) {
      const parts = (node.nodeValue ?? "").split("\n");
      parts.forEach((part, index) => {
        if (index > 0) {
          startLine();
        }
        if (part.length > 0) {
          tip.append(document.createTextNode(part));
        }
      });
      return;
    }

    if (node.nodeType !== Node.ELEMENT_NODE) {
      return;
    }

    const clone = (node as HTMLElement).cloneNode(false) as HTMLElement;
    tip.append(clone);

    chain.push(node as HTMLElement);
    tip = clone;

    for (const child of Array.from(node.childNodes)) {
      visit(child);
    }

    chain.pop();
    // `tip` is always the deepest clone for `chain`, including across a line
    // break, so stepping to the parent is correct either way.
    tip = tip.parentElement as HTMLElement;
  };

  startLine();
  for (const child of Array.from(element.childNodes)) {
    visit(child);
  }

  // a trailing newline would otherwise number one blank line too many
  const last = lines[lines.length - 1];
  if (lines.length > 1 && last.textContent?.length === 0) {
    lines.pop();
  }

  // interleave real newlines so copy/paste keeps its line breaks
  const children: Node[] = [];
  lines.forEach((line, index) => {
    if (index > 0) {
      children.push(document.createTextNode("\n"));
    }
    children.push(line);
  });

  element.replaceChildren(...children);
}

// ── Line-number gutter ───────────────────────────────────────────────────── //
//
// The gutter is a sibling of the scrolling `pre`, not a pseudo-element inside
// it. Inside, a long line scrolls underneath the numbers; outside, the overlap
// cannot happen at all and the column is a real element themes can style.

interface GutterObserver {
  observer: ResizeObserver;
  code: HTMLElement;
}

const gutterObservers = new WeakMap<HTMLElement, GutterObserver>();

function isWrapEnabled(): boolean {
  return document.documentElement.dataset.docsCodeWrap === "true";
}

/**
 * Rebuilds the gutter to match the block's lines and aligns each cell with the
 * line it labels.
 *
 * When soft wrap is on a logical line can occupy several visual rows, so cell
 * heights are measured from the rendered lines rather than assumed.
 */
function syncGutter(block: HTMLElement): void {
  const gutter = block.querySelector<HTMLElement>(".docs-code-gutter");
  const code = block.querySelector<HTMLElement>("pre code");
  if (!gutter || !code) {
    return;
  }

  const lines = Array.from(code.querySelectorAll<HTMLElement>(".docs-code-line"));

  if (gutter.childElementCount !== lines.length) {
    gutter.replaceChildren(
      ...lines.map((_, index) => {
        const cell = document.createElement("span");
        cell.textContent = String(index + 1);
        return cell;
      }),
    );
  }

  const cells = Array.from(gutter.children) as HTMLElement[];

  if (!isWrapEnabled()) {
    // every logical line is exactly one row, so let CSS handle it
    for (const cell of cells) {
      cell.style.height = "";
    }
    return;
  }

  const lineHeight = Number.parseFloat(getComputedStyle(code).lineHeight) || 0;
  lines.forEach((line, index) => {
    const cell = cells[index];
    if (!cell) {
      return;
    }

    // How many rows does this logical line occupy?
    //
    // `getClientRects()` is not it: for an inline element it yields one rect per
    // child fragment, so a single row containing nine highlight spans reports
    // seventeen rects. The union box is the reliable measure, but it spans the
    // *ink*, from the first row's top to the last row's bottom, which is
    // `(rows - 1) * lineHeight + inkHeight`. Since ink is a bit shorter than a
    // full row, that ratio lands just under the row count, and rounding
    // recovers it exactly. An empty line has no box and still needs one row.
    const height = line.getBoundingClientRect().height;
    const rows = Math.max(1, Math.round(height / lineHeight));
    cell.style.height = `${rows * lineHeight}px`;
  });
}

/** Keeps the gutter aligned as the block is resized or the wrap mode changes. */
function observeGutter(block: HTMLElement): void {
  syncGutter(block);

  const code = block.querySelector<HTMLElement>("pre code");
  if (!code || typeof ResizeObserver === "undefined") {
    return;
  }

  // Switching files replaces the keyed <pre>, so the observed element can go
  // stale while the block itself stays.
  const existing = gutterObservers.get(block);
  if (existing) {
    if (existing.code !== code) {
      existing.observer.unobserve(existing.code);
      existing.observer.observe(code);
      existing.code = code;
    }
    return;
  }

  const observer = new ResizeObserver(() => syncGutter(block));
  observer.observe(code);
  gutterObservers.set(block, { observer, code });
}

function syncAllGutters(): void {
  for (const block of document.querySelectorAll<HTMLElement>(".docs-code")) {
    syncGutter(block);
  }
}

// ── File tabs ────────────────────────────────────────────────────────────── //
//
// A block with a few files renders both a tab strip and a file picker. The
// strip is preferred; once it no longer fits the bar, the block switches to the
// picker. This is decided on the bar's own width rather than the viewport,
// because a block inside a split example is narrow on desktop too.

const tabObservers = new WeakSet<HTMLElement>();

/**
 * `remeasure` drops the cached strip width and measures the strip again. It is
 * set when the files change, since the cache describes the previous names.
 */
function fitCodeTabs(block: HTMLElement | null, remeasure = false): void {
  if (!block) {
    return;
  }
  if (remeasure) {
    block.classList.remove("docs-code-compact");
    delete block.dataset.docsTabsWidth;
  }
  const bar = block.querySelector<HTMLElement>(".docs-code-bar");
  const tabs = bar?.querySelector<HTMLElement>(".docs-code-tabs");
  if (!bar || !tabs) {
    block.classList.remove("docs-code-compact");
    return;
  }

  if (!block.classList.contains("docs-code-compact")) {
    if (tabs.scrollWidth > tabs.clientWidth + 1) {
      block.dataset.docsTabsWidth = String(tabs.scrollWidth);
      block.classList.add("docs-code-compact");
    }
    return;
  }

  // Compact: the strip is hidden, so compare the width it needed against the
  // room it would get back from the picker and the free space.
  const needed = Number(block.dataset.docsTabsWidth ?? "0");
  const style = getComputedStyle(bar);
  const gap = Number.parseFloat(style.columnGap) || 0;
  const others = Array.from(bar.children).filter(
    (child) => child !== tabs && !child.classList.contains("docs-code-filepicker"),
  );
  const used = others.reduce((total, child) => total + (child as HTMLElement).offsetWidth + gap, 0);
  const room =
    bar.clientWidth - Number.parseFloat(style.paddingLeft) - Number.parseFloat(style.paddingRight) - used;

  if (needed <= room) {
    block.classList.remove("docs-code-compact");
  }
}

/**
 * Refits on any size change in the bar, not just the block's: the Copy label
 * growing to "Copied" can squeeze the strip into overflow on its own.
 */
function observeTabs(block: HTMLElement): void {
  fitCodeTabs(block);
  const bar = block.querySelector<HTMLElement>(".docs-code-bar");
  if (!bar || tabObservers.has(block) || typeof ResizeObserver === "undefined") {
    return;
  }
  tabObservers.add(block);
  const observer = new ResizeObserver(() => fitCodeTabs(block));
  observer.observe(block);
  for (const child of Array.from(bar.children)) {
    observer.observe(child);
  }
}

// ── Soft wrap ────────────────────────────────────────────────────────────── //

const CODE_WRAP_KEY = "spillgebees-docs-code-wrap";

/**
 * Applies the wrap preference site-wide.
 *
 * Kept on the document element rather than per block so every code block agrees,
 * and so each block's toggle can show its state from CSS alone, with no component
 * state to drift out of sync.
 */
function applyCodeWrap(enabled: boolean): void {
  document.documentElement.dataset.docsCodeWrap = String(enabled);
  for (const button of document.querySelectorAll(".docs-code-wrap")) {
    button.setAttribute("aria-pressed", String(enabled));
  }
  syncAllGutters();
}

function toggleCodeWrap(): boolean {
  const enabled = !isWrapEnabled();
  writeStorage(CODE_WRAP_KEY, String(enabled));
  applyCodeWrap(enabled);
  return enabled;
}

/**
 * Highlights `element`, optionally re-seeding it from `source` first.
 *
 * Re-seeding matters: `wrapLines` moves each line into its own span, so
 * highlighting the same element a second time would start from the wrapped DOM
 * rather than the source. Callers that own the text (CodeBlock) pass it in
 * to make this idempotent.
 */
function highlightElement(element: HTMLElement | null, source?: string): void {
  if (!element) {
    return;
  }
  if (typeof source === "string") {
    element.textContent = source;
  }
  delete element.dataset.highlighted;
  hljs.highlightElement(element);
  wrapLines(element);

  const block = element.closest<HTMLElement>(".docs-code");
  if (block) {
    block.querySelector(".docs-code-wrap")?.setAttribute("aria-pressed", String(isWrapEnabled()));
    observeGutter(block);
    observeTabs(block);
  }
}

function highlightAll(): void {
  for (const element of document.querySelectorAll<HTMLElement>("pre code[class*='language-']")) {
    if (!element.dataset.highlighted) {
      highlightElement(element);
    }
  }
}

// ── On-this-page scroll spy ──────────────────────────────────────────────── //

export interface Section {
  title: string;
  id: string;
  level: number;
}

const HEADING_SELECTOR = ".docs-content-inner h2[id], .docs-content-inner h3[id]";

let scrollSpy: IntersectionObserver | null = null;
let scrollSpyListener: (() => void) | null = null;
/**
 * Set when the visitor clicks an on-this-page entry. Scroll-derived updates are
 * ignored until it expires, so the clicked entry stays selected even when the
 * page cannot scroll far enough to bring it to the top: the case where the last
 * few sections are all on screen at once.
 */
let pinnedSection: { id: string; until: number } | null = null;

function markActiveSection(id: string): void {
  for (const link of document.querySelectorAll<HTMLElement>("[data-docs-toc-link]")) {
    link.classList.toggle("docs-on", link.dataset.docsTocLink === id);
  }
}

function getSections(): Section[] {
  const headings = document.querySelectorAll<HTMLElement>(HEADING_SELECTOR);
  return Array.from(headings).map((element) => ({
    title: element.dataset.docsTocTitle ?? element.textContent?.trim() ?? "",
    id: element.id,
    level: Number(element.tagName.slice(1)),
  }));
}

/**
 * Marks the on-this-page entry for the heading nearest the top of the viewport.
 * Falls back to the last heading scrolled past so the rail is never blank.
 */
function observeSections(): void {
  scrollSpy?.disconnect();
  if (scrollSpyListener) {
    window.removeEventListener("scroll", scrollSpyListener);
    scrollSpyListener = null;
  }

  const headings = Array.from(document.querySelectorAll<HTMLElement>(HEADING_SELECTOR));
  if (headings.length === 0) {
    return;
  }

  const visible = new Set<string>();

  const setActive = (id: string | undefined): void => {
    if (pinnedSection && Date.now() < pinnedSection.until) {
      return;
    }
    pinnedSection = null;
    if (id) {
      markActiveSection(id);
    }
  };

  /**
   * Picks the active heading.
   *
   * The plain "first heading in view" rule breaks at the end of a page: once
   * the document is scrolled to the bottom, the remaining headings are all on
   * screen at once and the topmost one stays selected forever, so the last few
   * entries can never activate, including by clicking them. When we are within
   * a screen's reach of the bottom, distribute the trailing headings across the
   * remaining scroll instead, so the final entry is reachable.
   */
  const resolveActive = (): string | undefined => {
    const { scrollHeight } = document.documentElement;
    const scrollable = scrollHeight > window.innerHeight + 2;
    const atEnd = scrollable && window.scrollY + window.innerHeight >= scrollHeight - 2;

    if (atEnd) {
      return headings[headings.length - 1]?.id;
    }

    const firstVisible = headings.find((heading) => visible.has(heading.id));
    if (firstVisible) {
      return firstVisible.id;
    }

    let lastPassed = headings[0];
    for (const heading of headings) {
      if (heading.getBoundingClientRect().top < 120) {
        lastPassed = heading;
      }
    }
    return lastPassed?.id;
  };

  const update = (): void => setActive(resolveActive());

  scrollSpy = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        if (entry.isIntersecting) {
          visible.add(entry.target.id);
        } else {
          visible.delete(entry.target.id);
        }
      }
      update();
    },
    { rootMargin: "-72px 0px -65% 0px", threshold: [0, 1] },
  );

  for (const heading of headings) {
    scrollSpy.observe(heading);
  }

  // The observer only fires when intersection changes; scrolling the last few
  // pixels to the bottom often changes nothing, so listen for scroll too. The
  // handler is kept at module scope so the next call can detach this exact one.
  scrollSpyListener = update;
  window.addEventListener("scroll", update, { passive: true });
  update();
}

function scrollToElement(id: string): void {
  const target = document.getElementById(id);
  if (!target) {
    return;
  }

  // Activate immediately and hold it briefly: the smooth scroll may not be able
  // to bring this heading to the top (near the end of the document), and without
  // the pin the spy would snap the selection back.
  pinnedSection = { id, until: Date.now() + 900 };
  markActiveSection(id);

  // replaced rather than pushed, so Back leaves the page instead of stepping
  // through sections; Blazor keeps its own navigation state in history.state
  history.replaceState(history.state, "", `${location.pathname}${location.search}#${id}`);

  target.scrollIntoView({ behavior: prefersReducedMotion() ? "auto" : "smooth", block: "start" });
}

function prefersReducedMotion(): boolean {
  return window.matchMedia?.("(prefers-reduced-motion: reduce)").matches ?? false;
}

// ── Scroll lock + focus trap ─────────────────────────────────────────────── //

/**
 * The drawer and the search palette can both be open at once, so the lock is
 * held per owner and released only when nobody holds it.
 */
const scrollLockOwners = new Set<string>();

function setScrollLock(owner: string, locked: boolean): void {
  if (locked) {
    scrollLockOwners.add(owner);
  } else {
    scrollLockOwners.delete(owner);
  }
  document.body.classList.toggle("docs-scroll-locked", scrollLockOwners.size > 0);
}

const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

function focusableIn(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll<HTMLElement>(FOCUSABLE)).filter(
    (element) => element.offsetParent !== null,
  );
}

const traps = new Map<string, (restoreFocus: boolean) => void>();

/**
 * Confines Tab focus to `container` until `releaseFocusTrap(owner)`, then hands
 * focus back to whatever had it before the trap was set. `outside` adds
 * controls that live elsewhere in the page but belong to the cycle, such as
 * the top bar button that closes the drawer.
 */
function trapFocus(owner: string, container: HTMLElement | null, outside: HTMLElement[] = []): void {
  releaseFocusTrap(owner, false);
  if (!container) {
    return;
  }

  const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;

  const onKeyDown = (event: KeyboardEvent): void => {
    if (event.key !== "Tab") {
      return;
    }
    const focusable = [
      ...outside.filter((element) => element.isConnected && element.offsetParent !== null),
      ...focusableIn(container),
    ];
    const index = focusable.indexOf(document.activeElement as HTMLElement);
    if (index < 0) {
      return;
    }

    // Moved by hand rather than left to the browser: an `outside` control is
    // not next to the container in page order, so native Tab would leave the
    // cycle on the way between them.
    event.preventDefault();
    const step = event.shiftKey ? -1 : 1;
    focusable[(index + step + focusable.length) % focusable.length].focus();
  };

  // on the document, so a Tab from an `outside` control is caught too
  document.addEventListener("keydown", onKeyDown);
  traps.set(owner, (restoreFocus) => {
    document.removeEventListener("keydown", onKeyDown);
    if (restoreFocus && opener?.isConnected && opener.offsetParent !== null) {
      opener.focus();
    }
  });
}

function releaseFocusTrap(owner: string, restoreFocus = true): void {
  const release = traps.get(owner);
  traps.delete(owner);
  release?.(restoreFocus);
}

function focusElement(element: HTMLElement | null): void {
  element?.focus();
}

// ── Mobile navigation drawer ─────────────────────────────────────────────── //

interface DrawerHost {
  invokeMethodAsync(method: "CloseDrawerFromScript"): Promise<void>;
}

// keep in step with the drawer breakpoint in _base.scss and _chrome.scss
const DRAWER_QUERY = "(max-width: 820px)";

let drawerCleanup: (() => void) | null = null;

/**
 * Opens/closes the mobile navigation drawer. The class lives on <body> because
 * the drawer, its backdrop and the page scroll lock are siblings in different
 * parts of the tree.
 *
 * While open, focus moves into the drawer and stays there, Escape closes it,
 * and widening the window past the drawer breakpoint closes it too, since the
 * sidebar is then part of the page and nothing should stay locked.
 */
function setNavOpen(open: boolean, host: DrawerHost | null): void {
  document.body.classList.toggle("docs-nav-open", open);
  setScrollLock("nav", open);

  drawerCleanup?.();
  drawerCleanup = null;

  const drawer = document.getElementById("docs-sidebar");
  if (!open || !drawer) {
    releaseFocusTrap("nav");
    return;
  }

  // the top bar's hamburger is the drawer's close button, so it is part of the cycle
  const toggle = document.querySelector<HTMLElement>(".docs-hamburger");
  trapFocus("nav", drawer, toggle ? [toggle] : []);
  focusableIn(drawer)[0]?.focus();

  const close = (): void => {
    void host?.invokeMethodAsync("CloseDrawerFromScript");
  };
  // An Escape meant for the search palette must not also close the drawer.
  // The palette may already be gone from the DOM by the time this runs, but
  // the composed path was fixed when the event was dispatched.
  const onKeyDown = (event: KeyboardEvent): void => {
    const fromSearch = event
      .composedPath()
      .some((node) => node instanceof Element && node.classList.contains("docs-modal"));
    if (event.key === "Escape" && !fromSearch) {
      close();
    }
  };
  const media = window.matchMedia(DRAWER_QUERY);
  const onMediaChange = (): void => {
    if (!media.matches) {
      close();
    }
  };

  document.addEventListener("keydown", onKeyDown);
  media.addEventListener("change", onMediaChange);
  drawerCleanup = () => {
    document.removeEventListener("keydown", onKeyDown);
    media.removeEventListener("change", onMediaChange);
  };
}

// ── Themed select ────────────────────────────────────────────────────────── //

function selectOptions(root: HTMLElement): HTMLElement[] {
  return Array.from(root.querySelectorAll<HTMLElement>(".docs-select-menu [role='option']"));
}

/**
 * Keyboard support for DocSelect, bound once per instance. Opening and closing
 * stay in .NET; this only moves focus, and dismisses by clicking the trigger so
 * there is a single code path for state.
 */
function bindSelect(root: HTMLElement | null): void {
  if (!root || root.dataset.docsSelectBound) {
    return;
  }
  root.dataset.docsSelectBound = "true";

  root.addEventListener("keydown", (event) => {
    const trigger = root.querySelector<HTMLElement>(".docs-select-trigger");
    const isOpen = root.classList.contains("docs-open");
    const options = selectOptions(root);
    const index = options.indexOf(document.activeElement as HTMLElement);

    const move = (target: number): void => {
      event.preventDefault();
      options[(target + options.length) % options.length]?.focus();
    };

    switch (event.key) {
      case "ArrowDown":
      case "ArrowUp":
        if (!isOpen) {
          event.preventDefault();
          trigger?.click();
        } else {
          move(index + (event.key === "ArrowDown" ? 1 : -1));
        }
        break;
      case "Home":
        if (isOpen) {
          move(0);
        }
        break;
      case "End":
        if (isOpen) {
          move(options.length - 1);
        }
        break;
      case "Escape":
        if (isOpen) {
          event.preventDefault();
          event.stopPropagation();
          trigger?.click();
          trigger?.focus();
        }
        break;
      case "Tab":
        if (isOpen) {
          trigger?.click();
        }
        break;
    }
  });
}

/**
 * Called after the menu renders: flips it above the trigger when there is not
 * enough room below, then focuses the selected option.
 */
function openSelect(root: HTMLElement | null): void {
  const menu = root?.querySelector<HTMLElement>(".docs-select-menu");
  if (!root || !menu) {
    return;
  }

  root.classList.remove("docs-select-up");
  const rect = menu.getBoundingClientRect();
  const spaceAbove = root.getBoundingClientRect().top;
  if (rect.bottom > window.innerHeight && spaceAbove > window.innerHeight - rect.top) {
    root.classList.add("docs-select-up");
  }

  const options = selectOptions(root);
  (options.find((option) => option.getAttribute("aria-selected") === "true") ?? options[0])?.focus();
}

// ── Search palette ───────────────────────────────────────────────────────── //

function scrollOptionIntoView(id: string): void {
  document.getElementById(id)?.scrollIntoView({ block: "nearest" });
}

/** "⌘K" for Apple platforms, "Ctrl K" otherwise, from the visitor's browser. */
function getShortcutLabel(): string {
  const platform =
    (navigator as Navigator & { userAgentData?: { platform?: string } }).userAgentData?.platform ??
    navigator.platform;
  return /mac|iphone|ipad|ipod/i.test(platform) ? "⌘K" : "Ctrl K";
}

// ── Global shortcuts ─────────────────────────────────────────────────────── //

interface ShortcutHost {
  invokeMethodAsync(method: string): Promise<void>;
}

let shortcutHandler: ((event: KeyboardEvent) => void) | null = null;

/**
 * Registers the ⌘K / Ctrl+K / "/" shortcut, and cancels the browser's default
 * for the keys the open palette handles. The DotNet object reference is owned
 * by DocSearch, which unregisters on dispose.
 */
function registerShortcuts(host: ShortcutHost): void {
  unregisterShortcuts();

  shortcutHandler = (event: KeyboardEvent) => {
    const target = event.target as HTMLElement | null;
    const typing =
      !!target && (/^(input|textarea|select)$/i.test(target.tagName) || target.isContentEditable);

    // lower-cased for Caps Lock; Shift is left alone, since Ctrl+Shift+K is
    // Firefox's web console
    const isPaletteKey =
      event.key.toLowerCase() === "k" && (event.metaKey || event.ctrlKey) && !event.shiftKey;
    const isSlash = event.key === "/" && !typing;

    // Keys the palette handles must be cancelled while the event is still being
    // dispatched. Blazor's @onkeydown:preventDefault applies the value from the
    // last render, which on Blazor Server is one key behind.
    if (
      target?.closest(".docs-modal") &&
      !event.isComposing &&
      (event.key === "ArrowDown" || event.key === "ArrowUp" || event.key === "Enter")
    ) {
      event.preventDefault();
      return;
    }

    if (isPaletteKey || isSlash) {
      event.preventDefault();
      void host.invokeMethodAsync("OpenFromShortcut");
    }
  };

  document.addEventListener("keydown", shortcutHandler);
}

function unregisterShortcuts(): void {
  if (shortcutHandler) {
    document.removeEventListener("keydown", shortcutHandler);
    shortcutHandler = null;
  }
}

// ── Blazor initializer lifecycle ─────────────────────────────────────────── //

const api = {
  initializeTheming,
  setTheme,
  getTheme,
  setSchemePreference,
  getSchemePreference,
  getResolvedScheme,
  cycleSchemePreference,
  copyToClipboard,
  highlightElement,
  highlightAll,
  fitCodeTabs,
  toggleCodeWrap,
  syncAllGutters,
  getSections,
  observeSections,
  scrollToElement,
  setScrollLock,
  setNavOpen,
  trapFocus,
  releaseFocusTrap,
  focusElement,
  bindSelect,
  openSelect,
  scrollOptionIntoView,
  getShortcutLabel,
  registerShortcuts,
  unregisterShortcuts,
};

declare global {
  interface Window {
    Spillgebees?: { DocsSdk?: typeof api };
    hasDocsSdkInitialized?: boolean;
  }
}

function initialize(): void {
  if (window.hasDocsSdkInitialized) {
    return;
  }
  window.hasDocsSdkInitialized = true;

  window.Spillgebees = window.Spillgebees || {};
  window.Spillgebees.DocsSdk = api;

  // Only the scheme is applied this early. The stored theme family waits for
  // DocSite, which knows which themes this site allows; until then the
  // stylesheet's default applies.
  applyScheme(getSchemePreference());
  document.documentElement.dataset.docsCodeWrap = String(readStorage(CODE_WRAP_KEY) === "true");
}

export function beforeWebStart(): void {
  initialize();
}

export function afterWebStarted(): void {
  initialize();
}

export function beforeWebAssemblyStart(): void {
  initialize();
}

export function afterWebAssemblyStarted(): void {
  initialize();
}

export function beforeServerStart(): void {
  initialize();
}

export function afterServerStarted(): void {
  initialize();
}
