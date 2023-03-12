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

    bindgen::Builder::default()
        .header("./vcpkg/installed/x64-linux/include/srt/srt.h")
        .parse_callbacks(Box::new(ignored_macros))
        // .allowlist_function("^srt_.*")
        // .allowlist_type("^sockaddr.*")
        // .allowlist_var("^SRT_.*")
        // exclude methods that have Option types in the signature (and maybe will not be used)
        .blocklist_function(".*_callback$")
        .blocklist_function(".*handler$")
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
        .csharp_dll_name("libsrt")
        .generate_to_file("srt_ffi.rs", "dotnet/LibSRT.g.cs")
        .unwrap();
}
