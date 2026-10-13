from __future__ import annotations

import argparse
import re
from pathlib import Path

OLD_REPORT_PATTERN = re.compile(
    r"^(?P<row_index>\d+)_(?P<hs_code>[^_]+)_(?P<product_name>.+)_(?P<target_country>[^_]+)_(?P<timestamp>\d{8}_\d{6})(?P<suffix>\.[^.]+)$"
)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="기존 자동 생성 파일명을 사이트 기본 파일명 형식으로 변경합니다.")
    parser.add_argument("--download-dir", default=str(Path(__file__).resolve().parent), help="보고서 PDF가 있는 도구 폴더")
    parser.add_argument("--yes", action="store_true", help="확인 질문 없이 바로 파일명을 변경합니다.")
    parser.add_argument("--dry-run", action="store_true", help="변경하지 않고 계획만 출력합니다.")
    return parser


def main() -> None:
    args = build_parser().parse_args()
    download_dir = Path(args.download_dir)

    if not download_dir.exists():
        raise FileNotFoundError(f"다운로드 폴더를 찾을 수 없습니다: {download_dir}")

    plans = build_rename_plans(download_dir)

    if not plans:
        print("변경할 PDF 파일을 찾지 못했습니다.")
        return

    print("변경 예정 파일:")
    for source, target in plans:
        print(f"- {source.name} -> {target.name}")

    if args.dry_run:
        print("dry-run 모드라 실제 파일명은 변경하지 않았습니다.")
        return

    if not args.yes:
        answer = input("위 파일명을 변경할까요? [y/N]: ").strip().lower()
        if answer != "y":
            print("취소했습니다.")
            return

    for source, target in plans:
        source.rename(target)

    print(f"{len(plans)}개 파일명을 변경했습니다.")


def build_rename_plans(download_dir: Path) -> list[tuple[Path, Path]]:
    plans: list[tuple[Path, Path]] = []
    reserved_targets: set[Path] = set()

    for source in sorted(download_dir.glob("*.pdf")):
        match = OLD_REPORT_PATTERN.match(source.name)
        if not match:
            continue

        info = match.groupdict()
        target = build_target_path(download_dir, info, reserved_targets)
        if source.resolve() == target.resolve():
            continue

        reserved_targets.add(target)
        plans.append((source, target))

    return plans


def build_target_path(download_dir: Path, info: dict[str, str], reserved_targets: set[Path]) -> Path:
    hs_code = info["hs_code"].strip()
    product_name = info["product_name"].strip()
    target_country = info["target_country"].strip()
    suffix = info["suffix"] or ".pdf"
    filename = sanitize_report_filename(f"{target_country}_{product_name}({hs_code})_수출시장분석보고서{suffix}")
    return unique_target_path(download_dir / filename, reserved_targets)


def unique_target_path(path: Path, reserved_targets: set[Path]) -> Path:
    if not path.exists() and path not in reserved_targets:
        return path

    stem = path.stem
    suffix = path.suffix
    parent = path.parent
    counter = 1
    while True:
        candidate = parent / f"{stem} ({counter}){suffix}"
        if not candidate.exists() and candidate not in reserved_targets:
            return candidate
        counter += 1


def sanitize_report_filename(filename: str) -> str:
    return re.sub(r'[\\/:*?"<>|]', "_", filename).strip()


if __name__ == "__main__":
    main()
