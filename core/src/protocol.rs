//! Protocol types mirroring `wit/`. Shared across Rust backend, TS frontend, C# viewer.
//!
//! Every struct uses `#[serde(tag = "type")]` on enums so JSON messages on the
//! WebSocket carry a `"type"` discriminator field.

use serde::{Deserialize, Serialize};

// ── types ────────────────────────────────────────────────────────────────────

pub type SurfaceId = u32;

#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub struct PointI32 {
    pub x: i32,
    pub y: i32,
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub struct PointF32 {
    pub x: f32,
    pub y: f32,
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub struct SizeI32 {
    pub width: i32,
    pub height: i32,
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub struct SizeF32 {
    pub width: f32,
    pub height: f32,
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub struct RectI32 {
    pub origin: PointI32,
    pub size: SizeI32,
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub struct Color {
    pub r: u8,
    pub g: u8,
    pub b: u8,
    pub a: u8,
}

// ── window ────────────────────────────────────────────────────────────────────

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct OpenRequest {
    pub title: String,
    pub width: u32,
    pub height: u32,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub min_size: Option<SizeI32>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub max_size: Option<SizeI32>,
    pub resizable: bool,
    pub maximized: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub parent_id: Option<SurfaceId>,
    pub transparent: bool,
    pub decorations: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct OpenResponse {
    pub surface_id: SurfaceId,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CloseRequest {
    pub surface_id: SurfaceId,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SetProperties {
    pub surface_id: SurfaceId,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub title: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub size: Option<SizeI32>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub resizable: Option<bool>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub minimized: Option<bool>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub maximized: Option<bool>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub fullscreen: Option<bool>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub transparent: Option<bool>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub decorations: Option<bool>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct WindowResized {
    pub surface_id: SurfaceId,
    pub width: u32,
    pub height: u32,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct WindowClosed {
    pub surface_id: SurfaceId,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct FocusChanged {
    pub surface_id: SurfaceId,
    pub focused: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct FullscreenChanged {
    pub surface_id: SurfaceId,
    pub fullscreen: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ScaleChanged {
    pub surface_id: SurfaceId,
    pub scale: f32,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type")]
pub enum WindowEvent {
    #[serde(rename = "window.resized")]
    Resized(WindowResized),
    #[serde(rename = "window.closed")]
    Closed(WindowClosed),
    #[serde(rename = "window.focus-changed")]
    FocusChanged(FocusChanged),
    #[serde(rename = "window.fullscreen-changed")]
    FullscreenChanged(FullscreenChanged),
    #[serde(rename = "window.scale-changed")]
    ScaleChanged(ScaleChanged),
}

// ── render ────────────────────────────────────────────────────────────────────

#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub enum ImageFormat {
    #[serde(rename = "rgba8")]
    Rgba8,
    #[serde(rename = "jpeg")]
    Jpeg,
    #[serde(rename = "png")]
    Png,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DirtyRect {
    pub rect: RectI32,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type", rename = "render.frame")]
pub struct Frame {
    pub surface_id: SurfaceId,
    pub sequence: u64,
    pub size: SizeI32,
    pub format: ImageFormat,
    pub data: Vec<u8>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub dirty: Vec<DirtyRect>,
}

// ── input ─────────────────────────────────────────────────────────────────────

#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub enum MouseButton {
    #[serde(rename = "left")]
    Left,
    #[serde(rename = "right")]
    Right,
    #[serde(rename = "middle")]
    Middle,
    #[serde(rename = "back")]
    Back,
    #[serde(rename = "forward")]
    Forward,
}

#[derive(Debug, Clone, Copy, Default, Serialize, Deserialize)]
pub struct Modifiers {
    pub shift: bool,
    pub control: bool,
    pub alt: bool,
    pub meta: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PointerMoved {
    pub surface_id: SurfaceId,
    pub position: PointF32,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PointerPressed {
    pub surface_id: SurfaceId,
    pub button: MouseButton,
    pub position: PointF32,
    pub modifiers: Modifiers,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PointerReleased {
    pub surface_id: SurfaceId,
    pub button: MouseButton,
    pub position: PointF32,
    pub modifiers: Modifiers,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PointerWheel {
    pub surface_id: SurfaceId,
    pub delta_x: f32,
    pub delta_y: f32,
    pub modifiers: Modifiers,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PointerEnter {
    pub surface_id: SurfaceId,
    pub entered: bool,
    pub position: PointF32,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct KeyEvent {
    pub surface_id: SurfaceId,
    pub key: String,
    pub pressed: bool,
    pub modifiers: Modifiers,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CharEvent {
    pub surface_id: SurfaceId,
    #[serde(rename = "char")]
    pub character: u32,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TouchPoint {
    pub id: u64,
    pub position: PointF32,
    pub pressure: f32,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TouchStarted {
    pub surface_id: SurfaceId,
    pub points: Vec<TouchPoint>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TouchMoved {
    pub surface_id: SurfaceId,
    pub points: Vec<TouchPoint>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TouchEnded {
    pub surface_id: SurfaceId,
    pub points: Vec<TouchPoint>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ClipboardOffer {
    pub mime_type: String,
    pub data: Vec<u8>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ClipboardRequest {
    pub mime_types: Vec<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DragEnter {
    pub surface_id: SurfaceId,
    pub position: PointF32,
    pub paths: Vec<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DragMoved {
    pub surface_id: SurfaceId,
    pub position: PointF32,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DragLeave {
    pub surface_id: SurfaceId,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DropFiles {
    pub surface_id: SurfaceId,
    pub position: PointF32,
    pub paths: Vec<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SetCursor {
    pub surface_id: SurfaceId,
    pub cursor: u8,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type")]
pub enum InputEvent {
    #[serde(rename = "input.pointer-moved")]
    PointerMoved(PointerMoved),
    #[serde(rename = "input.pointer-pressed")]
    PointerPressed(PointerPressed),
    #[serde(rename = "input.pointer-released")]
    PointerReleased(PointerReleased),
    #[serde(rename = "input.pointer-wheel")]
    PointerWheel(PointerWheel),
    #[serde(rename = "input.pointer-enter")]
    PointerEnter(PointerEnter),
    #[serde(rename = "input.key")]
    Key(KeyEvent),
    #[serde(rename = "input.char")]
    Char(CharEvent),
    #[serde(rename = "input.touch-started")]
    TouchStarted(TouchStarted),
    #[serde(rename = "input.touch-moved")]
    TouchMoved(TouchMoved),
    #[serde(rename = "input.touch-ended")]
    TouchEnded(TouchEnded),
    #[serde(rename = "input.clipboard-offer")]
    ClipboardOffer(ClipboardOffer),
    #[serde(rename = "input.clipboard-request")]
    ClipboardRequest(ClipboardRequest),
    #[serde(rename = "input.drag-enter")]
    DragEnter(DragEnter),
    #[serde(rename = "input.drag-moved")]
    DragMoved(DragMoved),
    #[serde(rename = "input.drag-leave")]
    DragLeave(DragLeave),
    #[serde(rename = "input.drop-files")]
    DropFiles(DropFiles),
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct InputBatch {
    pub surface_id: SurfaceId,
    pub events: Vec<InputEvent>,
}

// ── menu ──────────────────────────────────────────────────────────────────────

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "kind")]
pub enum MenuItem {
    #[serde(rename = "action")]
    Action { id: String, label: String },
    #[serde(rename = "check")]
    Check { id: String, label: String, checked: bool },
    #[serde(rename = "radio")]
    Radio { id: String, label: String, group: String, selected: bool },
    #[serde(rename = "separator")]
    Separator,
    #[serde(rename = "submenu")]
    Submenu { label: String, items: Vec<MenuItem> },
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SetMenuBar {
    pub surface_id: SurfaceId,
    pub items: Vec<MenuItem>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ShowContextMenu {
    pub surface_id: SurfaceId,
    pub items: Vec<MenuItem>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MenuActivated {
    pub surface_id: SurfaceId,
    pub path: String,
}

// ── dialog ────────────────────────────────────────────────────────────────────

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct FileFilter {
    pub name: String,
    pub extensions: Vec<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct OpenFileDialog {
    pub surface_id: SurfaceId,
    pub title: String,
    pub filters: Vec<FileFilter>,
    pub multi_select: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SaveFileDialog {
    pub surface_id: SurfaceId,
    pub title: String,
    pub filters: Vec<FileFilter>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub default_name: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct FileResult {
    pub paths: Vec<String>,
    pub cancelled: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MessageBox {
    pub surface_id: SurfaceId,
    pub title: String,
    pub message: String,
    pub kind: String,
    pub buttons: Vec<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MessageBoxResult {
    pub button: u32,
}

// ── tray ──────────────────────────────────────────────────────────────────────

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "kind")]
pub enum TrayMenuItem {
    #[serde(rename = "action")]
    Action { id: String, label: String },
    #[serde(rename = "separator")]
    Separator,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SetTrayIcon {
    pub icon: Vec<u8>,
    pub tooltip: String,
    pub menu: Vec<TrayMenuItem>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TrayActivated {
    pub left_click: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TrayMenuSelected {
    pub id: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ShowNotification {
    pub title: String,
    pub body: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub icon: Option<Vec<u8>>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct NotificationClicked {
    pub id: u32,
}

// ── viewer commands (backend → desktop viewer over WebSocket) ──────────────────

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct OpenWindowCmd {
    pub title: String,
    pub width: u32,
    pub height: u32,
    pub url: String,
    pub resizable: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub parent_id: Option<SurfaceId>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CloseWindowCmd {
    pub surface_id: SurfaceId,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SetWindowPropsCmd {
    pub surface_id: SurfaceId,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub title: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub minimized: Option<bool>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub maximized: Option<bool>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub fullscreen: Option<bool>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "kind")]
pub enum ViewerMenuItem {
    #[serde(rename = "action")]
    Action { id: String, label: String },
    #[serde(rename = "separator")]
    Separator,
    #[serde(rename = "submenu")]
    Submenu { label: String, items: Vec<ViewerMenuItem> },
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SetMenuBarCmd {
    pub items: Vec<ViewerMenuItem>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SetDockIconCmd {
    pub icon: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SetDockBadgeCmd {
    pub text: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SetDockMenuCmd {
    pub items: Vec<ViewerMenuItem>,
}

/// Commands sent from the backend to the desktop viewer.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type")]
pub enum ViewerCommand {
    #[serde(rename = "viewer.open-window")]
    OpenWindow(OpenWindowCmd),
    #[serde(rename = "viewer.close-window")]
    CloseWindow(CloseWindowCmd),
    #[serde(rename = "viewer.set-window-props")]
    SetWindowProps(SetWindowPropsCmd),
    #[serde(rename = "viewer.set-menu-bar")]
    SetMenuBar(SetMenuBarCmd),
    #[serde(rename = "viewer.set-dock-icon")]
    SetDockIcon(SetDockIconCmd),
    #[serde(rename = "viewer.set-dock-badge")]
    SetDockBadge(SetDockBadgeCmd),
    #[serde(rename = "viewer.set-dock-menu")]
    SetDockMenu(SetDockMenuCmd),
}

/// Events sent from the desktop viewer to the backend.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type")]
pub enum ViewerEvent {
    #[serde(rename = "viewer.window-closed")]
    WindowClosed { surface_id: SurfaceId },
    #[serde(rename = "viewer.menu-activated")]
    MenuActivated { surface_id: SurfaceId, path: String },
    #[serde(rename = "viewer.dock-activated")]
    DockActivated { path: String },
}
