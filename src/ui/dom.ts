export interface ElAttrs {
  class?: string;
  text?: string;
  html?: string;
  title?: string;
  id?: string;
  onClick?: (ev: MouseEvent) => void;
  disabled?: boolean;
  style?: string;
  type?: string;
  value?: string;
  placeholder?: string;
  dataset?: Record<string, string>;
}

export function el<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  attrs: ElAttrs = {},
  children: (Node | string | null | undefined | false)[] = [],
): HTMLElementTagNameMap[K] {
  const node = document.createElement(tag);
  if (attrs.class) node.className = attrs.class;
  if (attrs.id) node.id = attrs.id;
  if (attrs.text !== undefined) node.textContent = attrs.text;
  if (attrs.html !== undefined) node.innerHTML = attrs.html;
  if (attrs.title) node.title = attrs.title;
  if (attrs.style) node.setAttribute('style', attrs.style);
  if (attrs.type) (node as HTMLInputElement).type = attrs.type;
  if (attrs.value !== undefined) (node as HTMLInputElement).value = attrs.value;
  if (attrs.placeholder) (node as HTMLInputElement).placeholder = attrs.placeholder;
  if (attrs.disabled) (node as HTMLButtonElement).disabled = true;
  if (attrs.dataset) for (const [k, v] of Object.entries(attrs.dataset)) node.dataset[k] = v;
  if (attrs.onClick) node.addEventListener('click', attrs.onClick as EventListener);
  for (const c of children) {
    if (c === null || c === undefined || c === false) continue;
    node.append(typeof c === 'string' ? document.createTextNode(c) : c);
  }
  return node;
}

export function $(id: string): HTMLElement {
  const node = document.getElementById(id);
  if (!node) throw new Error(`Missing element #${id}`);
  return node;
}

export function clear(node: HTMLElement): void {
  while (node.firstChild) node.removeChild(node.firstChild);
}

export function fmtTime(frames: number | null): string {
  if (frames === null) return '--.--';
  return `${(frames / 40).toFixed(2)}s`;
}

export function fmtInk(m: number | null): string {
  if (m === null) return '--';
  return `${m.toFixed(1)} m`;
}
