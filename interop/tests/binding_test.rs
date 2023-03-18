extern crate srt_interop;

use futures::future::FutureExt as _;
use srt_interop::srt::*;
use std::ffi::{c_char, c_int};
use std::mem::size_of;

async unsafe fn setup_async() {
    let result = srt_startup();
    assert_ne!(result, SRT_ERROR);
}
async unsafe fn teardown_async() {
    let result = srt_cleanup();
    assert_ne!(result, SRT_ERROR);
}

#[tokio::test]
async fn start_listen() {
    run_test_async((|| async {
        unsafe {
            let sock = srt_create_socket();
            assert_ne!(sock, SRT_ERROR);
            let port = 9999u16;
            let sin = sockaddr {
                sa_family: AF_INET as sa_family_t,
                sa_data: [
                    // 2 bytes port
                    (port >> 8) as c_char,
                    (port & 0xFF) as c_char,
                    // 4 bytes addr
                    0,
                    0,
                    0,
                    0,
                    // 8 bytes zero
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                ],
            };

            srt_listen(sock, 1);
            srt_bind(sock, &sin, size_of::<sockaddr> as c_int);
            srt_close(sock);
        }
    })())
    .await;
}

async fn run_test_async<F>(test: F)
where
    F: std::future::Future,
{
    unsafe {
        setup_async().await;
        let result = std::panic::AssertUnwindSafe(test).catch_unwind().await;
        teardown_async().await;
        if let Err(err) = result {
            std::panic::resume_unwind(err);
        }
    }
}
