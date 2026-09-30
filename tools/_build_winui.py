#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""只构建 WinUI 3 主程序（用 VS 的构建引擎）。用于快速迭代排查启动崩溃。"""
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
VSWHERE = r"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"
WINUI = os.path.join("src", "ByteForge.Settings.WinUI", "ByteForge.Settings.WinUI.csproj")


def find_vs():
    if not os.path.exists(VSWHERE):
        return None
    install = subprocess.run(
        [VSWHERE, "-latest", "-property", "installationPath"],
        capture_output=True, text=True,
    ).stdout.strip()
    if not install:
        return None
    tool = os.path.join(install, "MSBuild", "Current", "Bin", "MSBuild.exe")
    return tool if os.path.exists(tool) else None


cfg = sys.argv[1] if len(sys.argv) > 1 else "Release"
tool = find_vs()
print("VS build tool:", tool)
if not tool:
    sys.exit("未找到 VS 构建引擎")

cmd = [tool, WINUI, f"-p:Configuration={cfg}", "-p:Platform=x64", "-restore", "-v:m"]
print("$", " ".join(cmd))
r = subprocess.run(cmd, cwd=ROOT)
print("exit =", r.returncode)
sys.exit(r.returncode)
