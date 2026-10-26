"""Build the self-contained Windows companion; Python standard library only."""
from pathlib import Path
import os
import shutil
import subprocess
import sys

source = Path(__file__).resolve().parent
project = source.parent
output = project / "output"
output.mkdir(exist_ok=True)
env = os.environ.copy()
if shutil.which("mcs"):
    compiler = [shutil.which("mcs")]
    references = ["-r:" + n for n in ["System.Windows.Forms", "System.Drawing", "System.Core", "System.Xml"]]
elif os.name == "nt":
    compiler = [str(Path(env.get("WINDIR", r"C:\Windows")) / "Microsoft.NET/Framework/v4.0.30319/csc.exe")]
    references = ["-r:" + n + ".dll" for n in ["System.Windows.Forms", "System.Drawing", "System.Core", "System.Xml"]]
else:
    raise SystemExit("Install Mono mcs, or build on Windows with .NET Framework 4.x.")
# 첫 번째 빌드에서 그림을 미리 계산한 prepared-sprites.bin을 만든 뒤, 그것을 넣어 다시 빌드하면 시작이 빨라집니다.
prepared = "--no-prepared" not in sys.argv
exe = output / "마루는 강쥐.exe"
args = compiler + ["-target:winexe", "-optimize+", "-platform:anycpu", "-out:" + str(exe),
                   "-win32icon:" + str(project / "assets/friend.ico")] + references
for path in sorted((project / "assets").iterdir()):
    if path.suffix == ".ico" or (path.name == "prepared-sprites.bin" and not prepared):
        continue
    args.append("-resource:" + str(path) + "," + path.name)
for path in sorted(source.iterdir()):
    if path.suffix in [".cs", ".py", ".txt"]:
        args.append("-resource:" + str(path) + ",source/" + path.name)
args += [str(p) for p in sorted(source.glob("*.cs"))]
subprocess.run(args, env=env, check=True)
print("Built", exe, "bytes", exe.stat().st_size)
