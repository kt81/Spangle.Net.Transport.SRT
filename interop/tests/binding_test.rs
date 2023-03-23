extern crate srt_interop;

use futures::future::FutureExt as _;
use srt_interop::srt::*;
use std::ffi::{c_char, c_int, CStr};
use std::mem::size_of;

unsafe fn lasterror() -> String {
    return CStr::from_ptr(srt_getlasterror_str())
        .to_str()
        .unwrap()
        .to_owned();
}

async unsafe fn setup_async() {
    assert_ne!(SRT_ERROR, srt_startup(), "{}", lasterror());
}

async unsafe fn teardown_async() {
    assert_ne!(SRT_ERROR, srt_cleanup(), "{}", lasterror());
}

#[tokio::test]
async fn start_listen() {
    run_test_async((|| async {
        unsafe {
            let sock = srt_create_socket();
            assert_ne!(SRT_ERROR, sock);
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

            assert_ne!(
                SRT_ERROR,
                srt_bind(sock, &sin, size_of::<sockaddr> as c_int),
                "Bind error: {}",
                lasterror()
            );
            assert_ne!(
                SRT_ERROR,
                srt_listen(sock, 1),
                "Listen error: {}",
                lasterror()
            );
            assert_ne!(SRT_ERROR, srt_close(sock), "Close error: {}", lasterror());
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
