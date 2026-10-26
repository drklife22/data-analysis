걸어다니는 친구 1.0

전주치이카와 2.06 프로그램을 바탕으로 캐릭터 그림만 바꾼 버전입니다.
움직임, 친밀도, 간식, 할 일, 정각 알림 같은 기능은 원본과 같습니다.
추가 동작: 가끔 물고기를 쫓아 빠르게 달려가요. "커서를 따라오기"를 켜면 커서를 쫓아가요. 우클릭 → 동작 바로 보기 → 물고기 쫓아가기로 바로 볼 수 있어요.

실행
1. 걸어다니는친구.exe를 실행합니다. 설치는 필요 없습니다.
2. 종료하려면 작업표시줄 친구 아이콘을 우클릭한 뒤 종료를 누릅니다.
3. 기록은 AppData/Roaming/걸어다니는친구/ 폴더에 저장됩니다. 전주치이카와 기록과 섞이지 않습니다.

그림 바꾸기
1. moving character 폴더의 사진을 바꾸거나, prepare_art.py의 POSES 표에서 사진 이름을 바꿉니다.
2. python prepare_art.py  → 배경을 지우고 assets 폴더에 저장합니다.
3. python source/build.py --no-prepared
4. output/걸어다니는친구.exe --export-assets assets/prepared-sprites.bin
5. python source/build.py  → output/걸어다니는친구.exe

빌드에는 Python 3(Pillow) + Mono mcs 또는 Windows .NET Framework 4.x의 C# 컴파일러가 필요합니다.
