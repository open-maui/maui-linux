#!/usr/bin/env bash
# Regenerate the Wayland protocol bindings shipped as libopenmaui_wl.so, for
# linux-x64 and linux-arm64.
#
# Usage: native/build.sh [--no-regen] [--arch x64|arm64|all]
#   --no-regen   compile the generated .c files already in this directory
#                (skip wayland-scanner; use it to rebuild one architecture
#                without touching the sources)
#   --arch       which library to build (default: all)
#
# Outputs (both are packed by OpenMaui.Controls.Linux.csproj):
#   native/libopenmaui_wl.so               x86-64  -> runtimes/linux-x64/native
#   native/linux-arm64/libopenmaui_wl.so   aarch64 -> runtimes/linux-arm64/native
#
# Required packages (x64 build and regeneration): wayland-devel,
# wayland-protocols-devel, gcc
#   Fedora:   dnf install -y wayland-devel wayland-protocols-devel gcc
#   Debian:   apt install -y libwayland-dev wayland-protocols gcc
#   Arch:     pacman -S wayland wayland-protocols gcc
#
# The arm64 build is a cross build with clang (--target=aarch64-linux-gnu) and
# an LLVM linker: ld.lld on PATH, $LLD, or the rust-lld that ships with a
# rustup toolchain. No aarch64 sysroot is needed: the generated files only
# define wl_interface/wl_message data, so they are compiled freestanding
# against the host's wayland-util.h (pointer and int layout is identical on
# LP64 x86-64 and aarch64). The three core interfaces the bindings reference
# (wl_surface, wl_seat, wl_output) are satisfied at link time by a throwaway
# stub carrying the soname libwayland-client.so.0, so the result records the
# same DT_NEEDED as the x64 build and resolves against the real libwayland at
# load time.
#
# The resulting .so exports the wl_interface data symbols that
# LinuxApplication's Wayland window dlsym's at startup. Re-run this whenever
# you bump the wayland-protocols version we target. The script is idempotent.

set -euo pipefail
cd "$(dirname "$0")"

REGEN=1
ARCH=all
while [ $# -gt 0 ]; do
    case "$1" in
        --no-regen) REGEN=0 ;;
        --arch) shift; ARCH="${1:-}" ;;
        --arch=*) ARCH="${1#--arch=}" ;;
        -h|--help) sed -n '2,12p' "$0"; exit 0 ;;
        *) echo "unknown argument: $1" >&2; exit 2 ;;
    esac
    shift
done
case "$ARCH" in x64|arm64|all) ;; *) echo "--arch must be x64, arm64 or all" >&2; exit 2 ;; esac

SOURCES=(xdg-shell.c xdg-decoration.c fractional-scale.c viewporter.c text-input-v3.c primary-selection-v1.c)

if [ "$REGEN" = 1 ]; then
    WP=/usr/share/wayland-protocols

    wayland-scanner private-code "$WP/stable/xdg-shell/xdg-shell.xml"                                 xdg-shell.c
    wayland-scanner private-code "$WP/unstable/xdg-decoration/xdg-decoration-unstable-v1.xml"         xdg-decoration.c
    wayland-scanner private-code "$WP/staging/fractional-scale/fractional-scale-v1.xml"               fractional-scale.c
    wayland-scanner private-code "$WP/stable/viewporter/viewporter.xml"                               viewporter.c
    wayland-scanner private-code "$WP/unstable/text-input/text-input-unstable-v3.xml"                 text-input-v3.c
    wayland-scanner private-code "$WP/unstable/primary-selection/primary-selection-unstable-v1.xml"   primary-selection-v1.c

    # wayland-scanner emits __attribute__((visibility("hidden"))) on every interface
    # definition; we strip it so the symbols are dlsym-able from C#.
    sed -i 's/__attribute__ ((visibility("hidden")))//g' "${SOURCES[@]}"
fi

list_exports() {
    nm -D --defined-only "$1" | awk '$3 ~ /_interface$/ {print $3}' | sort
}

build_x64() {
    gcc -shared -fPIC -fvisibility=default -O2 \
        -o libopenmaui_wl.so \
        "${SOURCES[@]}" \
        -lwayland-client

    echo "Built $(stat -c %s libopenmaui_wl.so) byte linux-x64 library:"
    list_exports libopenmaui_wl.so
}

find_lld() {
    if [ -n "${LLD:-}" ]; then echo "$LLD"; return; fi
    if command -v ld.lld >/dev/null 2>&1; then command -v ld.lld; return; fi
    local rl
    rl=$(ls -1 "${RUSTUP_HOME:-$HOME/.rustup}"/toolchains/*/lib/rustlib/*/bin/rust-lld 2>/dev/null | sort -V | tail -1 || true)
    if [ -n "$rl" ]; then echo "$rl"; return; fi
    return 1
}

build_arm64() {
    local lld tmp wl_util_dir
    if ! command -v clang >/dev/null 2>&1; then
        echo "arm64: clang not found; skipping (install clang to cross-build)" >&2
        return 1
    fi
    if ! lld=$(find_lld); then
        echo "arm64: no LLVM linker found (ld.lld, \$LLD or rustup's rust-lld); skipping" >&2
        return 1
    fi
    # rust-lld needs to be told it is acting as the ELF (GNU ld) linker.
    local flavor=()
    case "$(basename "$lld")" in rust-lld|lld) flavor=(-flavor gnu) ;; esac

    wl_util_dir=$(dirname "$(echo '#include <wayland-util.h>' | cpp -H -o /dev/null 2>&1 | awk '/wayland-util.h/ {print $2; exit}')")
    [ -f "$wl_util_dir/wayland-util.h" ] || wl_util_dir=/usr/include

    tmp=$(mktemp -d)
    trap 'rm -rf "$tmp"' RETURN

    # Freestanding shims for the libc headers the generated code and
    # wayland-util.h include; nothing from them is used but NULL, the fixed
    # width integer types and va_list (all compiler builtins).
    mkdir -p "$tmp/include"
    cp "$wl_util_dir/wayland-util.h" "$tmp/include/"
    printf '#include <stddef.h>\n' > "$tmp/include/stdlib.h"
    printf '#include <stdint.h>\n' > "$tmp/include/inttypes.h"
    : > "$tmp/include/math.h"

    local cflags=(--target=aarch64-linux-gnu -ffreestanding -nostdlibinc
                  -isystem "$tmp/include" -fPIC -fvisibility=default -O2)

    local objs=()
    for src in "${SOURCES[@]}"; do
        clang "${cflags[@]}" -c "$src" -o "$tmp/${src%.c}.o"
        objs+=("$tmp/${src%.c}.o")
    done

    # Stub libwayland-client.so.0 exporting the core interfaces referenced
    # from the generated files (every extern wl_* interface).
    {
        echo '#include "wayland-util.h"'
        grep -h '^extern const struct wl_interface wl_' "${SOURCES[@]}" | sort -u \
            | sed 's/^extern const struct wl_interface \(wl_[a-z0-9_]*\);/const struct wl_interface \1;/'
    } > "$tmp/wlstub.c"
    clang "${cflags[@]}" -c "$tmp/wlstub.c" -o "$tmp/wlstub.o"
    "$lld" "${flavor[@]}" -m aarch64linux -shared -soname libwayland-client.so.0 \
        -o "$tmp/libwayland-client.so.0" "$tmp/wlstub.o"

    mkdir -p linux-arm64
    "$lld" "${flavor[@]}" -m aarch64linux -shared -soname libopenmaui_wl.so \
        --hash-style=both --build-id -z relro -z now \
        -o linux-arm64/libopenmaui_wl.so \
        "${objs[@]}" "$tmp/libwayland-client.so.0"

    echo "Built $(stat -c %s linux-arm64/libopenmaui_wl.so) byte linux-arm64 library:"
    readelf -h linux-arm64/libopenmaui_wl.so | grep -E 'Class|Machine'
    readelf -d linux-arm64/libopenmaui_wl.so | grep -E 'NEEDED|SONAME'
    readelf --dyn-syms -W linux-arm64/libopenmaui_wl.so \
        | awk '$7 != "UND" && $8 ~ /_interface$/ {print $8}' | sort
}

case "$ARCH" in
    x64)   build_x64 ;;
    arm64) build_arm64 ;;
    all)
        build_x64
        # The x64 library is the one every developer machine needs; a missing
        # cross toolchain must not fail it.
        build_arm64 || echo "linux-arm64 library not rebuilt (see above)" >&2
        ;;
esac
