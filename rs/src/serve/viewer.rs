use std::path::PathBuf;
use std::process::{Child, Command};
use std::sync::Arc;
use tokio::sync::Notify;

pub fn try_launch() -> Option<(Child, Arc<Notify>)> {
    let viewer_path = find_or_release_viewer()?;
    let child = Command::new(&viewer_path).spawn().ok()?;

    tracing::info!("Launched desktop viewer");
    let shutdown = Arc::new(Notify::new());

    Some((child, shutdown))
}

fn find_or_release_viewer() -> Option<PathBuf> {
    // First, try to find an existing binary
    if let Some(path) = find_viewer_binary() {
        return Some(path);
    }

    // Try to build and release to a temp location
    release_viewer()
}

fn find_viewer_binary() -> Option<PathBuf> {
    let exe = std::env::current_exe().ok()?;
    let dir = exe.parent()?;

    let base = if cfg!(target_os = "windows") { "DamascusUI.exe" } else { "DamascusUI" };

    let candidates = [
        dir.join("../../../csharp/bin/Debug/net10.0").join(base),
        dir.join("../../../csharp/bin/Release/net10.0").join(base),
        dir.join("../csharp/bin/Debug/net10.0").join(base),
        dir.join("../csharp/bin/Release/net10.0").join(base),
        dir.join(base),
    ];

    candidates.into_iter().find(|p| p.exists())
}

fn release_viewer() -> Option<PathBuf> {
    let temp = std::env::temp_dir().join("damascus-viewer");
    std::fs::create_dir_all(&temp).ok()?;

    let base = if cfg!(target_os = "windows") { "DamascusUI.exe" } else { "DamascusUI" };
    let dest = temp.join(base);

    // Try to find source binary and copy it
    if let Some(src) = find_viewer_binary_source() {
        std::fs::copy(&src, &dest).ok()?;
        return Some(dest);
    }

    None
}

fn find_viewer_binary_source() -> Option<PathBuf> {
    let exe = std::env::current_exe().ok()?;
    let dir = exe.parent()?;

    let base = if cfg!(target_os = "windows") { "DamascusUI.exe" } else { "DamascusUI" };
    let candidates = [
        dir.join("../../../csharp/bin/Debug/net10.0").join(base),
        dir.join("../../../csharp/bin/Release/net10.0").join(base),
        dir.join("../csharp/bin/Debug/net10.0").join(base),
        dir.join("../csharp/bin/Release/net10.0").join(base),
    ];

    candidates.into_iter().find(|p| p.exists())
}
