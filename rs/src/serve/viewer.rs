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

    let dest = temp_dir.join(bin_name);
    std::fs::write(&dest, file.data.as_ref()).ok()?;

    // Make executable on Unix
    #[cfg(unix)]
    {
        use std::os::unix::fs::PermissionsExt;
        std::fs::set_permissions(&dest, std::fs::Permissions::from_mode(0o755)).ok()?;
    }

    // Copy all publish artifacts so .NET can find dependencies
    for entry in ViewerBinary::iter() {
        if entry.as_ref() == bin_name { continue; }
        let content = ViewerBinary::get(entry.as_ref())?;
        std::fs::write(temp_dir.join(entry.as_ref()), content.data.as_ref()).ok();
    }

    Command::new(&dest).spawn().ok()
}
