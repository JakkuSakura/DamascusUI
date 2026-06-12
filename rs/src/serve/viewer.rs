use std::path::PathBuf;
use std::process::{Child, Command};
use std::sync::Arc;
use tokio::sync::Notify;

pub fn try_launch() -> Option<(Child, Arc<Notify>)> {
    let child = find_viewer_binary().and_then(|path| Command::new(&path).spawn().ok())?;

    tracing::info!("Launched desktop viewer");
    let shutdown = Arc::new(Notify::new());

    Some((child, shutdown))
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
    ];

    candidates.into_iter().find(|p| p.exists())
}
