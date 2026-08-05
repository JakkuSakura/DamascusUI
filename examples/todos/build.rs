use std::process::Command;

fn main() {
    let ui_dir = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("ui");

    if ui_dir.join("package.json").exists() {
        let status = Command::new("pnpm")
            .args(["install"])
            .current_dir(&ui_dir)
            .status();

        if let Ok(s) = &status
            && !s.success()
        {
            return;
        }

        let status = Command::new("pnpm")
            .args(["run", "build"])
            .current_dir(&ui_dir)
            .status();

        if let Ok(s) = &status
            && s.success()
        {
            println!("cargo:warning=todos frontend built successfully");
        }
    }

    println!("cargo:rerun-if-changed=ui/src");
    println!("cargo:rerun-if-changed=ui/index.html");
}
