use std::collections::HashSet;
use std::env;

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
// macro_rules! str {
//     ($pathBuf:tt) => {
//         $pathBuf.clone().to_str().unwrap()
//     };
// }
// macro_rules! safe_path {
//     ($pathBuf:tt) => {
//         $pathBuf.clone().to_str().unwrap().replace("\\", "/")
//     };
//     ($pathBuf:tt, $joinPath:tt) => {
//         $pathBuf.clone().join($joinPath).to_str().unwrap().replace("\\", "/")
//     };
// }
fn main() {
    let target_os = env::var("CARGO_CFG_TARGET_OS").unwrap();
    let target_arch = env::var("CARGO_CFG_TARGET_ARCH").unwrap();
    let arch = match target_arch.as_str() {
        "x86_64" => "x64",
        "aarch64" => "arm64",
        _ => panic!("unsupported: {}", target_arch),
    };

    let triplet: String;
    let stlib_ext: &str;
    let cmake_cxx_flags: &str;

    if target_os == "windows" {
        triplet = format!("{}-windows-static-md", arch);
        stlib_ext = ".lib";
        cmake_cxx_flags = "/EHsc /utf-8 -DWIN32_LEAN_AND_MEAN";
    } else if target_os == "linux" {
        triplet = format!("{}-linux", arch);
        stlib_ext = ".a";
        cmake_cxx_flags = "";
    } else if target_os == "macos//-is-not-tested!!!!!!" {
        triplet = format!("{}-osx", arch);
        stlib_ext = ".a";
        cmake_cxx_flags = "";
    } else {
        panic!("unsupported target os: {}", target_os)
    }

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

    // Build BoringSSL
    let ssl_dst = cmake::Config::new("deps/boringssl")
        .define("CMAKE_C_FLAGS", cmake_cxx_flags)
        .define("CMAKE_CXX_FLAGS", cmake_cxx_flags)
        .define("BUILD_SHARED_LIBS", "FALSE")
        .generator("Ninja")
        .build();

    // let ssl_include = ssl_dst.clone().join("include");
    let ssl_crypto_lib = ssl_dst.clone().join(format!("lib/crypto{}", stlib_ext));

    let dst = cmake::Config::new("deps/srt")
        // .very_verbose(true)
        .define("CMAKE_C_FLAGS", cmake_cxx_flags)
        .define("CMAKE_CXX_FLAGS", cmake_cxx_flags)
        .define("ENABLE_STATIC", "ON")
        .define("ENABLE_STDCXX_SYNC", "ON")
        .define("OPENSSL_USE_STATIC_LIBS", "ON")
        .define("OPENSSL_ROOT_DIR", ssl_dst)
        // .define("OPENSSL_INCLUDE_DIR", ssl_include)
        .define("OPENSSL_CRYPTO_LIBRARY", ssl_crypto_lib)
        .generator("Ninja")
        .build();

    let mut lib_path = dst.clone();
    lib_path.push("lib");
    println!("cargo:rustc-link-search=native={}", lib_path.display());
    println!(
        "cargo:rustc-link-search=native=vcpkg/installed/{}/lib",
        triplet
    );

    println!("cargo:rustc-link-lib=static=crypto");
    if target_os == "windows" {
        // println!("cargo:rustc-link-lib=dylib=user32");
        // println!("cargo:rustc-link-lib=dylib=crypt32");
        println!("cargo:rustc-link-lib=static=srt_static");
    } else {
        println!("cargo:rustc-link-lib=dylib=stdc++");
        println!("cargo:rustc-link-lib=static=srt");
    }

    let header = dst.clone().join("include").join("srt").join("srt.h");
    bindgen::Builder::default()
        .header(header.to_string_lossy())
        .parse_callbacks(Box::new(ignored_macros))
        .allowlist_function("^srt_.*")
        // .allowlist_type("^sockaddr.*")
        .allowlist_var("^(?:AF_|SRT_).*")
        .allowlist_type("^SRT_.*")
        .generate()
        .unwrap()
        .write_to_file("src/srt.rs")
        .unwrap();

    csbindgen::Builder::default()
        .input_bindgen_file("src/srt.rs")
        .rust_file_header("use super::srt::*;")
        .method_filter(|x| x.starts_with("srt_"))
        .csharp_entry_point_prefix("csbindgen_")
        .csharp_class_name("LibSRT")
        .csharp_class_accessibility("internal")
        .csharp_namespace("Spangle.Interop.Native")
        .csharp_dll_name("srt_interop")
        .generate_to_file("src/srt_ffi.rs", "dotnet/LibSRT.g.cs")
        .unwrap();
}
