use std::process::Command;

fn main() {
    // Build the Avalonia desktop viewer
    let csharp_dir = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../csharp");

    if !csharp_dir.join("DamascusUI.csproj").exists() {
        println!("cargo:warning=csharp/DamascusUI.csproj not found, skipping viewer embed");
        return;
    }

    // Build in release mode for embedding
    let status = Command::new("dotnet")
        .args(["publish", "-c", "Release", "-o", "publish"])
        .current_dir(&csharp_dir)
        .status()
        .expect("failed to run dotnet publish for viewer; install .NET SDK");

    if !status.success() {
        panic!("dotnet publish failed for the viewer");
    }

    // Determine the binary name
    let viewer_bin = if cfg!(target_os = "windows") {
        csharp_dir.join("publish/DamascusUI.exe")
    } else {
        csharp_dir.join("publish/DamascusUI")
    };

    if !viewer_bin.exists() {
        panic!("viewer binary not found after build: {}", viewer_bin.display());
    }

    // Copy to the embed directory
    let embed_dir = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("viewer-binary");
    std::fs::create_dir_all(&embed_dir).unwrap();

    let dest = embed_dir.join(viewer_bin.file_name().unwrap());
    std::fs::copy(&viewer_bin, &dest).unwrap();

    println!("cargo:warning=viewer binary embedded: {}", dest.display());
    println!("cargo:rerun-if-changed=../csharp");
}
