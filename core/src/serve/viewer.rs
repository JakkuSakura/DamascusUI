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
    let bin_name = if cfg!(target_os = "windows") { "DamascusUI.exe" } else { "DamascusUI" };

    let file = ViewerBinary::get(bin_name)?;
    let temp_dir = std::env::temp_dir().join("damascus-viewer");
    std::fs::create_dir_all(&temp_dir).ok()?;

    // Extract all embedded files so .NET finds its dependencies
    for entry in ViewerBinary::iter() {
        let content = ViewerBinary::get(entry.as_ref())?;
        std::fs::write(temp_dir.join(entry.as_ref()), content.data.as_ref()).ok();
    }

    let dest = temp_dir.join(bin_name);

    // Make executable on Unix
    #[cfg(unix)]
    {
        use std::os::unix::fs::PermissionsExt;
        std::fs::set_permissions(&dest, std::fs::Permissions::from_mode(0o755)).ok()?;
    }

    let mut cmd = Command::new(&dest);

    // Homebrew installs .NET to /opt/homebrew, not /usr/local/share
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
