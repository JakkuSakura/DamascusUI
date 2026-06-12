use std::path::PathBuf;
use std::process::{Child, Command};
use std::sync::Arc;
use tokio::sync::Notify;

pub fn try_launch() -> Option<(Child, Arc<Notify>)> {
    let child = launch_viewer()?;

    tracing::info!("Launched desktop viewer");
    let shutdown = Arc::new(Notify::new());

    Some((child, shutdown))
}

fn launch_viewer() -> Option<Child> {
    // Set DOTNET_ROOT from PATH if dotnet was installed via Homebrew
    let dotnet_root = find_dotnet_root();

    // Try the native binary first
    if let Some(path) = find_viewer_binary() {
        let mut cmd = Command::new(&path);
        if let Some(ref root) = dotnet_root {
            cmd.env("DOTNET_ROOT", root);
        }
        if let Ok(child) = cmd.spawn() {
            return Some(child);
        }
    }

    // Fallback: use dotnet run from the csharp project
    let repo_root = find_repo_root()?;
    let cs_dir = repo_root.join("csharp");
    if cs_dir.join("DamascusUI.csproj").exists() {
        let child = Command::new("dotnet")
            .args(["run", "--project"])
            .arg(cs_dir.join("DamascusUI.csproj"))
            .spawn()
            .ok()?;
        return Some(child);
    }

    None
}

fn find_dotnet_root() -> Option<String> {
    if let Ok(paths) = std::env::var("PATH") {
        for dir in std::env::split_paths(&paths) {
            let dotnet = dir.join("dotnet");
            if dotnet.exists() {
                // Homebrew: /opt/homebrew/bin/dotnet → root is /opt/homebrew
                if let Some(parent) = dir.parent() {
                    return Some(parent.to_string_lossy().to_string());
                }
            }
        }
    }
    None
}

fn find_repo_root() -> Option<PathBuf> {
    let exe = std::env::current_exe().ok()?;
    let mut dir = exe.parent()?.to_path_buf();

    for _ in 0..10 {
        if dir.join("csharp/DamascusUI.csproj").exists() {
            return Some(dir);
        }
        dir = dir.parent()?.to_path_buf();
    }
    None
}

fn find_viewer_binary() -> Option<PathBuf> {
    let exe = std::env::current_exe().ok()?;
    let dir = exe.parent()?;

    let base = if cfg!(target_os = "windows") { "DamascusUI.exe" } else { "DamascusUI" };

    let candidates = [
        dir.join("../../../csharp/bin/Debug/net10.0").join(base),
        dir.join("../../../csharp/bin/Release/net10.0").join(base),
        dir.join("../../../../csharp/bin/Debug/net10.0").join(base),
        dir.join("../../../../csharp/bin/Release/net10.0").join(base),
        dir.join(base),
    ];

    candidates.into_iter().find(|p| p.exists())
}
