use std::process::{Child, Command};
use std::sync::Arc;
use rust_embed::RustEmbed;
use tokio::sync::Notify;

#[derive(RustEmbed)]
#[folder = "viewer-binary"]
struct ViewerBinary;

pub fn try_launch() -> Option<(Child, Arc<Notify>)> {
    let child = launch_viewer()?;
    tracing::info!("Launched desktop viewer");
    let shutdown = Arc::new(Notify::new());
    Some((child, shutdown))
}

fn launch_viewer() -> Option<Child> {
    let temp_dir = std::env::temp_dir().join("damascus-viewer");
    std::fs::create_dir_all(&temp_dir).ok()?;

    // Extract all embedded files
    for entry in ViewerBinary::iter() {
        let content = ViewerBinary::get(entry.as_ref())?;
        let dest = temp_dir.join(entry.as_ref());
        if let Some(parent) = dest.parent() {
            std::fs::create_dir_all(parent).ok();
        }
        std::fs::write(&dest, content.data.as_ref()).ok();
    }

    #[cfg(target_os = "macos")]
    {
        // Launch binary directly from inside the .app bundle
        // (avoids Gatekeeper while preserving proper NSApplication context)
        let app_bin = temp_dir.join("DamascusUI.app/Contents/MacOS/DamascusUI");
        if app_bin.exists() {
            use std::os::unix::fs::PermissionsExt;
            std::fs::set_permissions(&app_bin, std::fs::Permissions::from_mode(0o755)).ok()?;
            return Command::new(&app_bin).current_dir(&temp_dir).spawn().ok();
        }
    }

    let bin_name = if cfg!(target_os = "windows") { "DamascusUI.exe" } else { "DamascusUI" };
    let dest = temp_dir.join(bin_name);

    #[cfg(unix)]
    {
        use std::os::unix::fs::PermissionsExt;
        std::fs::set_permissions(&dest, std::fs::Permissions::from_mode(0o755)).ok()?;
    }

    let mut cmd = Command::new(&dest);
    cmd.current_dir(&temp_dir);

    if let Some(root) = find_dotnet_root() {
        cmd.env("DOTNET_ROOT", &root);
    }

    cmd.spawn().ok()
}

fn find_dotnet_root() -> Option<String> {
    // Check known Homebrew paths first
    for candidate in &["/opt/homebrew", "/usr/local"] {
        let libexec = std::path::Path::new(candidate).join("Cellar/dotnet");
        if libexec.exists() {
            return Some(candidate.to_string());
        }
    }
    // Fall back to PATH
    if let Ok(paths) = std::env::var("PATH") {
        for dir in std::env::split_paths(&paths) {
            if dir.join("dotnet").exists() {
                if let Some(parent) = dir.parent() {
                    return Some(parent.to_string_lossy().to_string());
                }
            }
        }
    }
    None
}
