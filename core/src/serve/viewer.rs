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
    tracing::info!("extracting viewer to {}", temp_dir.display());
    std::fs::create_dir_all(&temp_dir).ok()?;

    let mut count = 0u32;
    for entry in ViewerBinary::iter() {
        let content = ViewerBinary::get(entry.as_ref())?;
        let dest = temp_dir.join(entry.as_ref());
        if let Some(parent) = dest.parent() {
            std::fs::create_dir_all(parent).ok();
        }
        std::fs::write(&dest, content.data.as_ref()).ok();
        count += 1;
    }
    tracing::info!("extracted {} files", count);

    #[cfg(target_os = "macos")]
    {
        let app_bin = temp_dir.join("DamascusUI.app/Contents/MacOS/DamascusUI");
        if app_bin.exists() {
            use std::os::unix::fs::PermissionsExt;
            std::fs::set_permissions(&app_bin, std::fs::Permissions::from_mode(0o755)).ok()?;
            tracing::info!("launching viewer from app bundle");
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
        tracing::debug!("setting DOTNET_ROOT={}", root);
        cmd.env("DOTNET_ROOT", &root);
    }

    tracing::info!("launching viewer binary");
    cmd.spawn().ok()
}

fn find_dotnet_root() -> Option<String> {
    for candidate in &["/opt/homebrew", "/usr/local"] {
        let libexec = std::path::Path::new(candidate).join("Cellar/dotnet");
        if libexec.exists() {
            tracing::debug!("found dotnet at {}", candidate);
            return Some(candidate.to_string());
        }
    }
    if let Ok(paths) = std::env::var("PATH") {
        for dir in std::env::split_paths(&paths) {
            if dir.join("dotnet").exists() {
                if let Some(parent) = dir.parent() {
                    let root = parent.to_string_lossy().to_string();
                    tracing::debug!("found dotnet in PATH at {}", root);
                    return Some(root);
                }
            }
        }
    }
    None
}
