#!/usr/bin/env bash
# Regenerate the Wayland protocol bindings shipped as libopenmaui_wl.so.
#
# Required packages: wayland-devel, wayland-protocols-devel, gcc
#   Fedora:   dnf install -y wayland-devel wayland-protocols-devel gcc
#   Debian:   apt install -y libwayland-dev wayland-protocols gcc
#   Arch:     pacman -S wayland wayland-protocols gcc
#
# This script regenerates xdg-shell.c, xdg-decoration.c, and fractional-scale.c
# from the system-installed XML and links them into libopenmaui_wl.so. The
# resulting .so exports the wl_interface data symbols that LinuxApplication's
# Wayland window dlsym's at startup. Re-run this whenever you bump the
# wayland-protocols version we target.

set -euo pipefail
cd "$(dirname "$0")"

WP=/usr/share/wayland-protocols

wayland-scanner private-code "$WP/stable/xdg-shell/xdg-shell.xml"                       xdg-shell.c
wayland-scanner private-code "$WP/unstable/xdg-decoration/xdg-decoration-unstable-v1.xml" xdg-decoration.c
wayland-scanner private-code "$WP/staging/fractional-scale/fractional-scale-v1.xml"     fractional-scale.c
wayland-scanner private-code "$WP/stable/viewporter/viewporter.xml"                     viewporter.c
wayland-scanner private-code "$WP/unstable/text-input/text-input-unstable-v3.xml"       text-input-v3.c

# wayland-scanner emits __attribute__((visibility("hidden"))) on every interface
# definition; we strip it so the symbols are dlsym-able from C#.
sed -i 's/__attribute__ ((visibility("hidden")))//g' \
    xdg-shell.c xdg-decoration.c fractional-scale.c viewporter.c text-input-v3.c

gcc -shared -fPIC -fvisibility=default -O2 \
    -o libopenmaui_wl.so \
    xdg-shell.c xdg-decoration.c fractional-scale.c viewporter.c text-input-v3.c \
    -lwayland-client

echo "Built $(ls -la libopenmaui_wl.so | awk '{print $5}') byte library:"
nm -D libopenmaui_wl.so | grep -E "_interface$" | grep -v ' U '
