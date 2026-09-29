#!/usr/bin/env python3

import argparse
import os
import subprocess
import shutil
import glob

from download_net_runtime import update_netcore_runtime, PLATFORM_WINDOWS, PLATFORM_WINDOWS_ARM64, PLATFORM_LINUX, PLATFORM_LINUX_ARM64, PLATFORM_MACOS, PLATFORM_MACOS_ARM64
from exe_set_subsystem import set_subsystem

TFM = "net10.0"

p = os.path.join

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("platform", nargs="*")
    parser.add_argument("--x64-only", action="store_true")

    args = parser.parse_args()
    platforms: list[str] = args.platform
    x64_only: bool = args.x64_only

    script_path = os.path.dirname(os.path.realpath(__file__))
    os.chdir(script_path)

    if "windows" in platforms:
        publish_windows(x64_only)
    if "linux" in platforms:
        publish_linux(x64_only)
    if "osx" in platforms:
        publish_osx()


ZAPRET_REPO = "Flowseal/zapret-discord-youtube"
ZAPRET_RELEASE_API = f"https://api.github.com/repos/{ZAPRET_REPO}/releases/latest"
ZAPRET_RELEASES_PAGE = f"https://github.com/{ZAPRET_REPO}/releases/latest"


def latest_zapret_zip_url() -> str:
    """Finds the newest release archive.

    The API is tried first and the web pages second: the API allows only sixty unauthenticated
    calls an hour per address, and a build machine that has been talking to GitHub can be out of
    them. The pages have no such limit, but the asset list is loaded separately from the release
    page, so it takes two requests: the redirect gives the tag, the assets fragment gives the file.
    """
    import json
    import re
    import urllib.request

    headers = {"User-Agent": "Airlock Launcher build"}

    try:
        request = urllib.request.Request(ZAPRET_RELEASE_API, headers=headers)
        with urllib.request.urlopen(request, timeout=60) as response:
            release = json.load(response)

        return next(a["browser_download_url"] for a in release["assets"] if a["name"].endswith(".zip"))
    except Exception as e:
        print(f"  release API unavailable ({e}), reading the release pages instead")

    request = urllib.request.Request(ZAPRET_RELEASES_PAGE, headers=headers)
    with urllib.request.urlopen(request, timeout=60) as response:
        tag = response.geturl().rstrip("/").rsplit("/", 1)[-1]

    assets_url = f"https://github.com/{ZAPRET_REPO}/releases/expanded_assets/{tag}"
    request = urllib.request.Request(assets_url, headers=headers)
    with urllib.request.urlopen(request, timeout=60) as response:
        fragment = response.read().decode("utf-8", "replace")

    match = re.search(r'"(/' + ZAPRET_REPO + r'/releases/download/[^"]+\.zip)"', fragment)
    if not match:
        raise RuntimeError(f"no zip asset listed for {tag}")

    return "https://github.com" + match.group(1)


def bundle_zapret(publish_dir: str):
    """Ships zapret with the launcher.

    It is downloaded here, at build time, rather than by the launcher at run time: the players who
    need it are behind the very blocking that would stop them fetching it. Failing to fetch it does
    not fail the build - the launcher can still download it later, and can be told to.
    """
    import io as _io
    import json
    import urllib.request
    import zipfile

    target = p(publish_dir, "zapret")

    # A previous bundle may still be in use - winws.exe holds its libraries open while it filters
    # traffic - so files that refuse to go are left and overwritten below where possible.
    if os.path.isdir(target):
        shutil.rmtree(target, onexc=lambda func, path, exc: print(f"  keeping {path}: {exc}"))

    try:
        url = latest_zapret_zip_url()
        print(f"Bundling zapret from {url}")

        request = urllib.request.Request(url, headers={"User-Agent": "Airlock Launcher build"})
        with urllib.request.urlopen(request, timeout=120) as response:
            archive = zipfile.ZipFile(_io.BytesIO(response.read()))

        # The release is packed inside one versioned folder; drop it so winws.exe lands where the
        # launcher looks for it.
        names = archive.namelist()
        roots = {name.split("/")[0] for name in names if "/" in name}
        prefix = f"{roots.pop()}/" if len(roots) == 1 and all("/" in n for n in names) else ""

        for name in names:
            relative = name[len(prefix):]
            if not relative or name.endswith("/"):
                continue

            destination = p(target, *relative.split("/"))
            os.makedirs(os.path.dirname(destination), exist_ok=True)

            try:
                with archive.open(name) as source, open(destination, "wb") as out:
                    shutil.copyfileobj(source, out)
            except PermissionError:
                # In use by a running copy; what is already there is the same release anyway.
                print(f"  in use, kept as is: {relative}")

        if not os.path.isfile(p(target, "bin", "winws.exe")):
            raise RuntimeError("archive did not contain bin/winws.exe")
    except Exception as e:
        print(f"WARNING: could not bundle zapret ({e}); the launcher will offer to download it instead")
        if os.path.isdir(target):
            shutil.rmtree(target)


def publish_windows(x64_only: bool):
    update_netcore_runtime([PLATFORM_WINDOWS])
    if not x64_only:
        update_netcore_runtime([PLATFORM_WINDOWS_ARM64])

    clear_prev_publish("Windows")

    dotnet_publish("SS14.Launcher/SS14.Launcher.csproj", "win-x64", False, "/p:FullRelease=True", "/p:RobustILLink=true", "/p:TargetOS=Windows")
    dotnet_publish("SS14.Loader/SS14.Loader.csproj", "win-x64", False, "/p:FullRelease=True", "/p:RobustILLink=true", "/p:TargetOS=Windows")
    if os.name == 'nt':
        dotnet_publish("SS14.Launcher.Bootstrap/SS14.Launcher.Bootstrap.csproj", "win-x64", True, "/p:FullRelease=True", "/p:RobustILLink=true", "/p:TargetOS=Windows")

    safe_set_subsystem(f"SS14.Launcher/bin/Release/{TFM}/win-x64/publish/SS14.Launcher.exe")
    safe_set_subsystem(f"SS14.Loader/bin/Release/{TFM}/win-x64/publish/SS14.Loader.exe")

    if not x64_only:
        dotnet_publish("SS14.Launcher/SS14.Launcher.csproj", "win-arm64", False, "/p:FullRelease=True", "/p:RobustILLink=true", "/p:TargetOS=Windows")
        dotnet_publish("SS14.Loader/SS14.Loader.csproj", "win-arm64", False, "/p:FullRelease=True", "/p:RobustILLink=true", "/p:TargetOS=Windows")
        safe_set_subsystem(f"SS14.Launcher/bin/Release/{TFM}/win-arm64/publish/SS14.Launcher.exe")
        safe_set_subsystem(f"SS14.Loader/bin/Release/{TFM}/win-arm64/publish/SS14.Loader.exe")

    os.makedirs("bin/publish/Windows/bin_x64/loader", exist_ok=True)
    os.makedirs("bin/publish/Windows/dotnet_x64", exist_ok=True)
    if not x64_only:
        os.makedirs("bin/publish/Windows/bin_arm64/loader", exist_ok=True)
        os.makedirs("bin/publish/Windows/dotnet_arm64", exist_ok=True)

    bootstrap_path = f"SS14.Launcher.Bootstrap/bin/Release/{TFM}-windows/win-x64/publish/Airlock Launcher.exe"
    # Natively compiled copy we need to get from a separate worker.
    if os.path.isfile("Airlock Launcher.exe"):
        bootstrap_path = "Airlock Launcher.exe"
    shutil.copyfile(bootstrap_path, "bin/publish/Windows/Airlock Launcher.exe")
    shutil.copyfile("SS14.Launcher.Bootstrap/console.bat", "bin/publish/Windows/console.bat")

    shutil.copytree("Dependencies/dotnet/windows", "bin/publish/Windows/dotnet_x64", dirs_exist_ok=True)
    shutil.copytree(f"SS14.Launcher/bin/Release/{TFM}/win-x64/publish", "bin/publish/Windows/bin_x64", dirs_exist_ok=True)
    shutil.copytree(f"SS14.Loader/bin/Release/{TFM}/win-x64/publish", "bin/publish/Windows/bin_x64/loader", dirs_exist_ok=True)

    if not x64_only:
        shutil.copytree("Dependencies/dotnet/windows-arm64", "bin/publish/Windows/dotnet_arm64", dirs_exist_ok=True)
        shutil.copytree(f"SS14.Launcher/bin/Release/{TFM}/win-arm64/publish", "bin/publish/Windows/bin_arm64", dirs_exist_ok=True)
        shutil.copytree(f"SS14.Loader/bin/Release/{TFM}/win-arm64/publish", "bin/publish/Windows/bin_arm64/loader", dirs_exist_ok=True)

    bundle_zapret("bin/publish/Windows")

    shutil.make_archive("SS14.Launcher_Windows", "zip", "bin/publish/Windows")

def publish_linux(x64_only: bool):
    update_netcore_runtime([PLATFORM_LINUX])
    if not x64_only:
        update_netcore_runtime([PLATFORM_LINUX_ARM64])

    clear_prev_publish("Linux")

    os.makedirs("bin/publish/Linux", exist_ok=True)

    dotnet_publish("SS14.Launcher/SS14.Launcher.csproj", "linux-x64", False, "/p:FullRelease=True", "/p:RobustILLink=true", "/p:TargetOS=Linux")
    dotnet_publish("SS14.Loader/SS14.Loader.csproj", "linux-x64", False, "/p:FullRelease=True", "/p:RobustILLink=true", "/p:TargetOS=Linux")

    if not x64_only:
        dotnet_publish("SS14.Launcher/SS14.Launcher.csproj", "linux-arm64", False, "/p:FullRelease=True", "/p:RobustILLink=true", "/p:TargetOS=Linux")
        dotnet_publish("SS14.Loader/SS14.Loader.csproj", "linux-arm64", False, "/p:FullRelease=True", "/p:RobustILLink=true", "/p:TargetOS=Linux")

    os.makedirs("bin/publish/Linux/bin_x64/loader", exist_ok=True)
    os.makedirs("bin/publish/Linux/dotnet_x64", exist_ok=True)
    if not x64_only:
        os.makedirs("bin/publish/Linux/bin_arm64/loader", exist_ok=True)
        os.makedirs("bin/publish/Linux/dotnet_arm64", exist_ok=True)

    shutil.copytree("Dependencies/dotnet/linux", "bin/publish/Linux/dotnet_x64", dirs_exist_ok=True)
    shutil.copytree(f"SS14.Launcher/bin/Release/{TFM}/linux-x64/publish", "bin/publish/Linux/bin_x64", dirs_exist_ok=True)
    shutil.copytree(f"SS14.Loader/bin/Release/{TFM}/linux-x64/publish", "bin/publish/Linux/bin_x64/loader", dirs_exist_ok=True)

    if not x64_only:
        shutil.copytree("Dependencies/dotnet/linux-arm64", "bin/publish/Linux/dotnet_arm64", dirs_exist_ok=True)
        shutil.copytree(f"SS14.Launcher/bin/Release/{TFM}/linux-arm64/publish", "bin/publish/Linux/bin_arm64", dirs_exist_ok=True)
        shutil.copytree(f"SS14.Loader/bin/Release/{TFM}/linux-arm64/publish", "bin/publish/Linux/bin_arm64/loader", dirs_exist_ok=True)

    shutil.copyfile("PublishFiles/SS14.Launcher", "bin/publish/Linux/SS14.Launcher")
    shutil.copyfile("PublishFiles/SS14.desktop", "bin/publish/Linux/SS14.desktop")

    shutil.make_archive("SS14.Launcher_Linux", "zip", "bin/publish/Linux")


def publish_osx():
    update_netcore_runtime([PLATFORM_MACOS, PLATFORM_MACOS_ARM64])

    clear_prev_publish("macOS")

    os.makedirs("bin/publish/macOS", exist_ok=True)
    shutil.copytree("PublishFiles/Airlock Launcher.app", "bin/publish/macOS/Airlock Launcher.app")

    res_root = "bin/publish/macOS/Airlock Launcher.app/Contents/Resources"

    loader_res_root = f"{res_root}/SimpleStation14.app/Contents/Resources"

    for arch in ["x64", "arm64"]:
        full_arch_name = "x86_64" if arch == "x64" else arch
        dotnet_publish("SS14.Launcher/SS14.Launcher.csproj", f"osx-{arch}", False, "/p:FullRelease=True", "/p:RobustILLink=true", "/p:TargetOS=MacOS")
        dotnet_publish("SS14.Loader/SS14.Loader.csproj", f"osx-{arch}", False, "/p:FullRelease=True", "/p:RobustILLink=true", "/p:TargetOS=MacOS")

        shutil.copytree(f"SS14.Launcher/bin/Release/{TFM}/osx-{arch}/publish", f"{res_root}/{full_arch_name}/bin", dirs_exist_ok=True)
        shutil.copytree(f"SS14.Loader/bin/Release/{TFM}/osx-{arch}/publish", f"{loader_res_root}/{full_arch_name}/bin", dirs_exist_ok=True)

    shutil.copytree("Dependencies/dotnet/mac", f"{res_root}/x86_64/dotnet")
    shutil.copytree("Dependencies/dotnet/mac-arm64", f"{res_root}/arm64/dotnet")

    shutil.make_archive("SS14.Launcher_macOS", "zip", "bin/publish/macOS/")

def clear_prev_publish(publish_dir: str):
    shutil.rmtree(f"bin/publish/{publish_dir}", ignore_errors=True)

    for path in glob.glob("**/bin"):
        shutil.rmtree(path)


def run(*args: str):
    subprocess.run(args, shell=False, check=True)

def safe_set_subsystem(exe: str):
    if os.name != 'nt':
        set_subsystem(exe, 2)

def dotnet_publish(proj: str, rid: str, self_contained: bool, *args: str):
    run(
        "dotnet",
        "publish",
        proj,
        "--runtime",
        rid,
        "--self-contained" if self_contained else "--no-self-contained",
        "--configuration",
        "Release",
        "/nologo",
        *args)

if __name__ == "__main__":
    main()
