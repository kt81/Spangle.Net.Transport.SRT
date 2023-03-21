#  __     __         _       _     _
#  \ \   / /_ _ _ __(_) __ _| |__ | | ___ ___
#   \ \ / / _` | '__| |/ _` | '_ \| |/ _ \ __|
#    \ V / (_| | |  | | (_| | |_) | |  __\__ \
#     \_/ \__,_|_|  |_|\__,_|_.__/|_|\___|___/
#
ifeq ($(OS),Windows_NT)
	SHELL := powershell.exe
	STLIB_EXT := .dll
	TRIPLET := x64-windows-static-md
else
	# Linux or others
	SHELL := bash
	STLIB_EXT := .a
	UNAME_S := $(shell uname -s)
	ifeq ($(UNAME_S),Darwin)
		NIX_NAME := darwin_is_not_tested
	else
		NIX_NAME := linux
	endif
	UNAME_P := $(shell uname -p)
	ifeq ($(UNAME_P),x86_64)
		ARCH := x64
	else ifneq ($(filter arm%,$(UNAME_P)),)
		ARCH := arm64
	endif
endif

#   _____                    _
#  |_   _|_ _ _ __ __ _  ___| |_ ___
#    | |/ _` | '__/ _` |/ _ \ __/ __|
#    | | (_| | | | (_| |  __/ |_\__ \
#    |_|\__,_|_|  \__, |\___|\__|___/
#                 |___/

.PHONY: init vcpkg srt

interop/target/debug/srt_interop$(STLIB_EXT): vcpkg srt
	cargo build

vcpkg: interop/vcpkg
	interop/vcpkg/vcpkg intstall

init:
	git submodule update --init
interop/vcpkg: init
interop/srt: init

