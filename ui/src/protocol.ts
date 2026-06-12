// Protocol types mirroring wit/ — shared between SolidJS frontend and Rust backend.
// Communication is via WebSocket with JSON messages carrying a "type" discriminator.

// ── types ────────────────────────────────────────────────────────────────────

export type SurfaceId = number;

export interface PointI32 {
  x: number;
  y: number;
}

export interface PointF32 {
  x: number;
  y: number;
}

export interface SizeI32 {
  width: number;
  height: number;
}

export interface SizeF32 {
  width: number;
  height: number;
}

export interface RectI32 {
  origin: PointI32;
  size: SizeI32;
}

export interface Color {
  r: number;
  g: number;
  b: number;
  a: number;
}

// ── window ────────────────────────────────────────────────────────────────────

export interface OpenRequest {
  type: "window.open";
  title: string;
  width: number;
  height: number;
  minSize?: SizeI32;
  maxSize?: SizeI32;
  resizable: boolean;
  maximized: boolean;
  parentId?: SurfaceId;
}

export interface OpenResponse {
  type: "window.open-response";
  surfaceId: SurfaceId;
}

export interface CloseRequest {
  type: "window.close";
  surfaceId: SurfaceId;
}

export interface SetProperties {
  type: "window.set-properties";
  surfaceId: SurfaceId;
  title?: string;
  size?: SizeI32;
  resizable?: boolean;
  minimized?: boolean;
  maximized?: boolean;
  fullscreen?: boolean;
}

export type WindowEvent =
  | { type: "window.resized"; surfaceId: SurfaceId; width: number; height: number }
  | { type: "window.closed"; surfaceId: SurfaceId }
  | { type: "window.focus-changed"; surfaceId: SurfaceId; focused: boolean }
  | { type: "window.fullscreen-changed"; surfaceId: SurfaceId; fullscreen: boolean }
  | { type: "window.scale-changed"; surfaceId: SurfaceId; scale: number };

// ── render ────────────────────────────────────────────────────────────────────

export type ImageFormat = "rgba8" | "jpeg" | "png";

export interface DirtyRect {
  rect: RectI32;
}

export interface Frame {
  type: "render.frame";
  surfaceId: SurfaceId;
  sequence: number;
  size: SizeI32;
  format: ImageFormat;
  data: number[];
  dirty: DirtyRect[];
}

// ── input ─────────────────────────────────────────────────────────────────────

export type MouseButton = "left" | "right" | "middle" | "back" | "forward";

export interface Modifiers {
  shift: boolean;
  control: boolean;
  alt: boolean;
  meta: boolean;
}

export interface PointerMoved {
  type: "input.pointer-moved";
  surfaceId: SurfaceId;
  position: PointF32;
}

export interface PointerPressed {
  type: "input.pointer-pressed";
  surfaceId: SurfaceId;
  button: MouseButton;
  position: PointF32;
  modifiers: Modifiers;
}

export interface PointerReleased {
  type: "input.pointer-released";
  surfaceId: SurfaceId;
  button: MouseButton;
  position: PointF32;
  modifiers: Modifiers;
}

export interface PointerWheel {
  type: "input.pointer-wheel";
  surfaceId: SurfaceId;
  deltaX: number;
  deltaY: number;
  modifiers: Modifiers;
}

export interface PointerEnter {
  type: "input.pointer-enter";
  surfaceId: SurfaceId;
  entered: boolean;
  position: PointF32;
}

export interface KeyEvent {
  type: "input.key";
  surfaceId: SurfaceId;
  key: string;
  pressed: boolean;
  modifiers: Modifiers;
}

export interface CharEvent {
  type: "input.char";
  surfaceId: SurfaceId;
  char: number;
}

export interface TouchPoint {
  id: number;
  position: PointF32;
  pressure: number;
}

export interface TouchStarted {
  type: "input.touch-started";
  surfaceId: SurfaceId;
  points: TouchPoint[];
}

export interface TouchMoved {
  type: "input.touch-moved";
  surfaceId: SurfaceId;
  points: TouchPoint[];
}

export interface TouchEnded {
  type: "input.touch-ended";
  surfaceId: SurfaceId;
  points: TouchPoint[];
}

export interface ClipboardOffer {
  type: "input.clipboard-offer";
  mimeType: string;
  data: number[];
}

export interface ClipboardRequest {
  type: "input.clipboard-request";
  mimeTypes: string[];
}

export interface DragEnter {
  type: "input.drag-enter";
  surfaceId: SurfaceId;
  position: PointF32;
  paths: string[];
}

export interface DragMoved {
  type: "input.drag-moved";
  surfaceId: SurfaceId;
  position: PointF32;
}

export interface DragLeave {
  type: "input.drag-leave";
  surfaceId: SurfaceId;
}

export interface DropFiles {
  type: "input.drop-files";
  surfaceId: SurfaceId;
  position: PointF32;
  paths: string[];
}

export interface SetCursor {
  type: "input.set-cursor";
  surfaceId: SurfaceId;
  cursor: number;
}

export type InputEvent =
  | PointerMoved
  | PointerPressed
  | PointerReleased
  | PointerWheel
  | PointerEnter
  | KeyEvent
  | CharEvent
  | TouchStarted
  | TouchMoved
  | TouchEnded
  | ClipboardOffer
  | ClipboardRequest
  | DragEnter
  | DragMoved
  | DragLeave
  | DropFiles;

export interface InputBatch {
  surfaceId: SurfaceId;
  events: InputEvent[];
}

// ── menu ──────────────────────────────────────────────────────────────────────

export type MenuItem =
  | { kind: "action"; id: string; label: string }
  | { kind: "check"; id: string; label: string; checked: boolean }
  | { kind: "radio"; id: string; label: string; group: string; selected: boolean }
  | { kind: "separator" }
  | { kind: "submenu"; label: string; items: MenuItem[] };

export interface SetMenuBar {
  type: "menu.set-bar";
  surfaceId: SurfaceId;
  items: MenuItem[];
}

export interface ShowContextMenu {
  type: "menu.show-context";
  surfaceId: SurfaceId;
  items: MenuItem[];
}

export interface MenuActivated {
  type: "menu.activated";
  surfaceId: SurfaceId;
  path: string;
}

// ── dialog ────────────────────────────────────────────────────────────────────

export interface FileFilter {
  name: string;
  extensions: string[];
}

export interface OpenFileDialog {
  type: "dialog.open-file";
  surfaceId: SurfaceId;
  title: string;
  filters: FileFilter[];
  multiSelect: boolean;
}

export interface SaveFileDialog {
  type: "dialog.save-file";
  surfaceId: SurfaceId;
  title: string;
  filters: FileFilter[];
  defaultName?: string;
}

export interface FileResult {
  type: "dialog.file-result";
  paths: string[];
  cancelled: boolean;
}

export interface MessageBox {
  type: "dialog.message-box";
  surfaceId: SurfaceId;
  title: string;
  message: string;
  kind: string;
  buttons: string[];
}

export interface MessageBoxResult {
  type: "dialog.message-result";
  button: number;
}

// ── tray ──────────────────────────────────────────────────────────────────────

export type TrayMenuItem =
  | { kind: "action"; id: string; label: string }
  | { kind: "separator" };

export interface SetTrayIcon {
  type: "tray.set-icon";
  icon: number[];
  tooltip: string;
  menu: TrayMenuItem[];
}

export interface TrayActivated {
  type: "tray.activated";
  leftClick: boolean;
}

export interface TrayMenuSelected {
  type: "tray.menu-selected";
  id: string;
}

export interface ShowNotification {
  type: "tray.notification";
  title: string;
  body: string;
  icon?: number[];
}

export interface NotificationClicked {
  type: "tray.notification-clicked";
  id: number;
}

// ── message union ─────────────────────────────────────────────────────────────

/** Every protocol message that can flow over the WebSocket. */
export type ProtocolMessage =
  // Window — backend→frontend requests & frontend→backend events
  | OpenRequest
  | OpenResponse
  | CloseRequest
  | SetProperties
  | WindowEvent
  // Render — backend→frontend
  | Frame
  // Input — frontend→backend
  | InputEvent
  | InputBatch
  | SetCursor
  // Menu
  | SetMenuBar
  | ShowContextMenu
  | MenuActivated
  // Dialog
  | OpenFileDialog
  | SaveFileDialog
  | FileResult
  | MessageBox
  | MessageBoxResult
  // Tray
  | SetTrayIcon
  | TrayActivated
  | TrayMenuSelected
  | ShowNotification
  | NotificationClicked;
