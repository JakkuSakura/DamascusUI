use std::process::Command;

fn main() {
    let frontend_dir = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../frontend");

    if frontend_dir.join("package.json").exists() {
        let status = Command::new("pnpm")
            .args(["install"])
            .current_dir(&frontend_dir)
            .status();

        if let Ok(s) = &status {
            if !s.success() {
                // try without install (already set up)
            }
        }

        let status = Command::new("pnpm")
            .args(["run", "build"])
            .current_dir(&frontend_dir)
            .status();

        if let Ok(s) = &status {
            if s.success() {
                println!("cargo:warning=todos frontend built successfully");
            }
        }
    }

    println!("cargo:rerun-if-changed=../frontend/src");
    println!("cargo:rerun-if-changed=../frontend/index.html");
    println!("cargo:rerun-if-changed=../frontend/vite.config.ts");
}
