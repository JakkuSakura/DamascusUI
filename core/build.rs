use std::fs;
use std::process::Command;

fn main() {
    // Bundle frontend (ui/dist → embedded by static_files.rs)
    let ts_dist = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../ui/dist");
    if ts_dist.exists() {
        println!("cargo:warning=bundling frontend from ui/dist");
        println!("cargo:rerun-if-changed=../ui/dist");
    }

    // Bundle viewer (copy entire viewer/publish → viewer-binary/)
    let csharp_publish = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../viewer/publish");
    let embed_dir = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("viewer-binary");

    if csharp_publish.exists() {
        fs::create_dir_all(&embed_dir).unwrap();

        #[cfg(unix)]
        {
            Command::new("cp")
                .args(["-r", &format!("{}/.", csharp_publish.display()), &format!("{}", embed_dir.display())])
                .status()
                .unwrap();
        }
        #[cfg(windows)]
        {
            Command::new("xcopy")
                .args([&format!("{}\\*", csharp_publish.display()), &format!("{}", embed_dir.display()), "/E", "/Y"])
                .status()
                .unwrap();
        }

        println!("cargo:warning=viewer bundled from viewer/publish");
        println!("cargo:rerun-if-changed=../viewer/publish");
    }
}
