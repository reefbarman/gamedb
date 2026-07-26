#!/usr/bin/env python3

import json
import pathlib
import re
import subprocess
import urllib.parse

ROOT = pathlib.Path(__file__).resolve().parent.parent


def fail(message):
    raise SystemExit(message)


def require(condition, message):
    if not condition:
        fail(message)


def load_json(path):
    with (ROOT / path).open(encoding="utf-8-sig") as handle:
        return json.load(handle)


def validate_generated_files():
    tracked = subprocess.run(
        ["git", "ls-files"],
        cwd=ROOT,
        check=True,
        capture_output=True,
        text=True,
    ).stdout.splitlines()
    generated = re.compile(
        r"(^|/)(Library|Temp|Obj|Build|Builds|Logs|UserSettings|TestResults|CoverageResults)(/|$)|\.(csproj|sln)$",
        re.IGNORECASE,
    )
    rejected = [path for path in tracked if generated.search(path)]
    require(not rejected, "Generated Unity/IDE files are tracked:\n" + "\n".join(rejected))


def validate_json():
    json_paths = {
        pathlib.Path("package.json"),
        pathlib.Path("TestProject~/Packages/manifest.json"),
        pathlib.Path("TestProject~/Packages/packages-lock.json"),
        *pathlib.Path(ROOT / "Runtime").rglob("*.asmdef"),
        *pathlib.Path(ROOT / "Editor").rglob("*.asmdef"),
        *pathlib.Path(ROOT / "Tests").rglob("*.asmdef"),
        *pathlib.Path(ROOT / "Samples~").rglob("*.json"),
    }
    for path in sorted(json_paths, key=str):
        if path.is_absolute():
            path = path.relative_to(ROOT)
        load_json(path)


def validate_package_contract():
    package = load_json("package.json")
    require(package.get("name") == "com.reefbarman.gamedb", "Unexpected package name")
    require(
        re.fullmatch(r"\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?", package.get("version", "")),
        "Invalid package version",
    )
    require(package.get("unity") == "6000.5", "package.json unity must be 6000.5")
    require(package.get("unityRelease") == "4f1", "package.json unityRelease must be 4f1")
    require(
        package.get("dependencies")
        == {
            "com.unity.modules.imgui": "1.0.0",
            "com.unity.modules.unitywebrequest": "1.0.0",
            "com.unity.nuget.newtonsoft-json": "3.2.2",
        },
        "Unexpected package dependency contract",
    )

    required_paths = [
        "README.md",
        "CHANGELOG.md",
        "LICENSE.md",
        "Third Party Notices.md",
        "Documentation~/index.md",
        "Documentation~/editor-authoring.md",
        "Documentation~/runtime.md",
        "Documentation~/api-reference.md",
        "Documentation~/automation.md",
        "Samples~/Basic/README.md",
        "Samples~/Basic/Resources/GameDBs/basic.json",
        "Samples~/Basic/Resources/GameDBs/basic.schema.json",
    ]
    required_paths.extend(sample["path"] for sample in package.get("samples", []))
    for path in required_paths:
        require((ROOT / path).exists(), f"Missing package file: {path}")

    manifest = load_json("TestProject~/Packages/manifest.json")
    require(
        manifest["dependencies"].get("com.reefbarman.gamedb") == "file:../..",
        "Development project must use file:../..",
    )
    require(
        "com.reefbarman.gamedb" in manifest.get("testables", []),
        "Package must remain testable",
    )

    project_version = (ROOT / "TestProject~/ProjectSettings/ProjectVersion.txt").read_text(
        encoding="utf-8-sig"
    )
    require(
        "m_EditorVersion: 6000.5.4f1" in project_version,
        "Unexpected TestProject Unity version",
    )

    tests = load_json("Tests/EditMode/GameDBLibrary.Tests.asmdef")
    require(tests.get("name") == "GameDBLibrary.Tests", "Unexpected test assembly name")
    require(tests.get("includePlatforms") == ["Editor"], "Tests must be EditMode-only")
    require(
        "UNITY_INCLUDE_TESTS" in tests.get("defineConstraints", []),
        "Missing UNITY_INCLUDE_TESTS constraint",
    )


def validate_metadata():
    for tree_name in ("Runtime", "Editor", "Tests"):
        tree = ROOT / tree_name
        for path in (tree, *tree.rglob("*")):
            if path.name.endswith(".meta"):
                target = pathlib.Path(str(path)[:-5])
                require(target.exists(), f"Orphan metadata file: {path.relative_to(ROOT)}")
            else:
                meta = pathlib.Path(str(path) + ".meta")
                require(meta.is_file(), f"Missing metadata file: {meta.relative_to(ROOT)}")


def validate_markdown_links():
    markdown_paths = [
        ROOT / "README.md",
        ROOT / "CHANGELOG.md",
        *(ROOT / "Documentation~").glob("*.md"),
        *(ROOT / "Samples~").rglob("*.md"),
    ]
    link_pattern = re.compile(r"\[[^\]]*\]\(([^)]+)\)")
    for markdown in markdown_paths:
        text = markdown.read_text(encoding="utf-8-sig")
        for target in link_pattern.findall(text):
            if target.startswith(("http://", "https://", "mailto:", "#")):
                continue
            relative = urllib.parse.unquote(target.split("#", 1)[0])
            if not relative:
                continue
            resolved = (markdown.parent / relative).resolve()
            require(
                resolved.exists(),
                f"Broken Markdown link in {markdown.relative_to(ROOT)}: {target}",
            )


def main():
    validate_generated_files()
    validate_json()
    validate_package_contract()
    validate_metadata()
    validate_markdown_links()
    print("Local UPM package integrity checks passed.")


if __name__ == "__main__":
    main()
