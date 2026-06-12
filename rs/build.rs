use std::fs;
use std::process::Command;

fn main() {
    // Bundle frontend (ts/dist → embedded by static_files.rs)
    let ts_dist = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../ts/dist");
    if ts_dist.exists() {
        println!("cargo:warning=bundling frontend from ts/dist");
        println!("cargo:rerun-if-changed=../ts/dist");
    }

    // Bundle viewer (copy entire csharp/publish → viewer-binary/)
    let csharp_publish = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../csharp/publish");
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

        println!("cargo:warning=viewer bundled from csharp/publish");
        println!("cargo:rerun-if-changed=../csharp/publish");
    }
}
