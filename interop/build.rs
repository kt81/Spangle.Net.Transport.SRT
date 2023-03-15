use cmake::Config;
use std::collections::HashSet;

#[derive(Debug)]
struct IgnoreMacros(HashSet<String>);

impl bindgen::callbacks::ParseCallbacks for IgnoreMacros {
    fn will_parse_macro(&self, name: &str) -> bindgen::callbacks::MacroParsingBehavior {
        if self.0.contains(name) {
            bindgen::callbacks::MacroParsingBehavior::Ignore
        } else {
            bindgen::callbacks::MacroParsingBehavior::Default
        }
    }
}

fn main() {
    let ignored_macros = IgnoreMacros(
        vec![
            // "FP_INFINITE".into(),
            // "FP_NAN".into(),
            // "FP_NORMAL".into(),
            // "FP_SUBNORMAL".into(),
            // "FP_ZERO".into(),
            "IPPORT_RESERVED".into(),
        ]
        .into_iter()
        .collect(),
    );
    let mut vcpkg_root = std::env::current_dir().unwrap();
    vcpkg_root.push("/vcpkg");
    let mut openssl_root = vcpkg_root.clone();
    openssl_root.push("/packages/openssl_x64-linux");
    let mut include_dir = openssl_root.clone();
    include_dir.push("/include");
    let mut crypto_lib = openssl_root.clone();
    crypto_lib.push("/lib/libcrypto.a");

    let dst = Config::new("srt")
        // .very_verbose(true)
        // .define("CMAKE_TOOLCHAIN_FILE", root + "\\vcpkg\\scripts\\buildsystems\\vcpkg.cmake")
        .define("ENABLE_STATIC", "ON")
        .define("ENABLE_STDCXX_SYNC", "ON")
        .define("OPENSSL_ROOT_DIR", openssl_root)
        .define("OPENSSL_INCLUDE_DIR", include_dir)
        .define("OPENSSL_CRYPTO_LIBRARY", crypto_lib)
        .build();

    println!("cargo:rustc-link-search=native={}", dst.display());
    println!("cargo:rustc-link-lib=static=libsrt");
    println!("cargo:rustc-link-lib=dylib=stdc++");

    let mut header = dst.clone();
    header.push("include/srt/srt.h");
    bindgen::Builder::default()
        .header(header.to_string_lossy())
        .parse_callbacks(Box::new(ignored_macros))
        // .allowlist_function("^srt_.*")
        // .allowlist_type("^sockaddr.*")
        // .allowlist_var("^SRT_.*")
        // exclude methods that have Option types in the signature (and maybe will not be used)
        // .blocklist_function(".*_callback$")
        // .blocklist_function(".*handler$")
        .generate()
        .unwrap()
        .write_to_file("srt.rs")
        .unwrap();

    csbindgen::Builder::default()
        .input_bindgen_file("srt.rs")
        .rust_file_header("use super::srt::*;")
        .method_filter(|x| x.starts_with("srt_"))
        .csharp_entry_point_prefix("csbindgen_")
        .csharp_class_name("LibSRT")
        .csharp_class_accessibility("internal")
        .csharp_namespace("Spangle.Interop.Native")
        .csharp_dll_name("libsrt_interop")
        .generate_to_file("srt_ffi.rs", "dotnet/LibSRT.g.cs")
        .unwrap();

    // let mut lib_path = dst.clone();
    // let out_dir = std::env::var("OUT_DIR").unwrap() + "/libsrt.so";
    // lib_path.push("lib/libsrt.so");
    // let res = std::fs::copy(lib_path, out_dir);
    // println!("Copy result: {}", res.unwrap());
}
