import re
p = "Assets/Scripts/Core/RoomThemes.cs"
s = open(p, encoding="utf-8").read()
extras = {
 "세탁실": ["LaundryCart"], "린넨실": ["LaundryCart", "LaundryCart"], "폐기물실": ["LaundryCart", "Generator"],
 "보일러실": ["PipeManifold", "Generator"], "기계실": ["PipeManifold", "Generator"], "배전실": ["Generator"], "환기실": ["PipeManifold"], "정비실": ["PipeManifold", "Generator"],
 "직원 휴게실": ["Sofa", "TVCart", "WaterCooler"], "탈의실": ["CrackedMirror"], "취사장": ["KitchenStove", "FoodTrolley"], "급식실": ["KitchenStove", "FoodTrolley"], "구내식당": ["KitchenStove", "FoodTrolley"],
 "영안실": ["AutopsyTable", "Coffin"], "안치실": ["Coffin"], "부검실": ["AutopsyTable"], "병리실": ["LabBench", "AutopsyTable"], "연구실": ["LabBench", "Skeleton"],
 "방사선실": ["CrashCart"], "MRI실": ["CrashCart"], "검사실": ["Skeleton", "LabBench"], "소독실": ["CrashCart"], "격리실": ["CrackedMirror", "Crutches"],
 "접수실": ["WaterCooler", "Sofa"], "약국": ["CrashCart"], "응급 처치실": ["CrashCart", "CrashCart"], "진료실": ["Armchair", "Skeleton", "CrackedMirror"], "상담실": ["Armchair", "Safe"], "원무과": ["WaterCooler", "Safe"],
 "물리치료실": ["Crutches", "Skeleton"], "대기실": ["Sofa", "WaterCooler", "TVCart"], "매점": ["WaterCooler"], "경비실": ["TVCart", "Safe", "Armchair"], "도서실": ["Armchair", "RockingChair", "CrackedMirror"],
 "예배실": ["Statue", "Confessional", "Piano"],
 "병실": ["Crutches", "FireExtinguisher", "TVCart"], "소아 병실": ["ToyChest", "RockingChair", "Incubator"], "처치실": ["CrashCart"], "주사실": ["CrashCart", "WaterCooler"], "면회실": ["Sofa", "Piano", "Armchair"],
 "회복실": ["CrashCart"], "격리 병실": ["CrackedMirror"], "샤워실": ["CrackedMirror"], "의국": ["Skeleton", "Armchair"], "환자 휴게실": ["Sofa", "TVCart", "Piano", "RockingChair"],
 "수술실": ["CrashCart", "Skeleton"], "수술 준비실": ["CrashCart"], "중환자실": ["CrashCart", "FireExtinguisher"], "마취실": ["CrashCart"], "원장실": ["Armchair", "Safe", "CrackedMirror", "Piano"], "회의실": ["Armchair", "Safe"],
 "정신과 격리실": ["CrackedMirror", "RockingChair"], "VIP 병실": ["Armchair", "Sofa", "CrackedMirror"], "분만실": ["Incubator", "CrashCart"], "재활실": ["Crutches", "Skeleton"],
}
out = []
n = 0
for line in s.split("\n"):
    m = re.match(r'(\s*new Theme\("([^"]+)", P\()([^)]*)(\).*)', line)
    if m and m.group(2) in extras:
        add = "".join(', "%s"' % e for e in extras[m.group(2)])
        line = m.group(1) + m.group(3) + add + m.group(4)
        n += 1
    out.append(line)
s = "\n".join(out)
print("themes extended:", n)


def rep(old, new):
    global s
    assert old in s, old
    s = s.replace(old, new, 1)


rep("public const int FloorStyles = 8, WallStyles = 6, Tints = 6, LightColors = 5;", "public const int FloorStyles = 15, WallStyles = 12, Tints = 6, LightColors = 5;")
rep("//               벽  0 벗겨진 페인트 1 타일 2 푸른 페인트 3 나무 패널 4 낙서 콘크리트 5 빛바랜 벽지",
    "//               벽  0 벗겨진 페인트 1 타일 2 푸른 페인트 3 나무 패널 4 낙서 콘크리트 5 빛바랜 벽지\n"
    "        // 추가 바닥 8 헤링본 마룻바닥 9 고무 바닥 10 모자이크 타일 11 철판 12 물 고인 콘크리트 13 불탄 마루 14 자갈·잔해\n"
    "        // 추가 벽  6 벙커 콘크리트 7 두 톤 페인트 8 곰팡이 9 붉은 벽돌 10 짙은 나무 판넬 11 핏자국 벽")
rep("""                        env.floorTex = t.floors[h % t.floors.Length];
                        env.wallTex = t.walls[(h >> 3) % t.walls.Length];""",
    """                        env.floorTex = t.floors[h % t.floors.Length];
                        env.wallTex = t.walls[(h >> 3) % t.walls.Length];
                        // 테마에 어울리는 새 질감을 일부 방에 섞는다 (같은 테마라도 방마다 달라 보이게)
                        var xf = ExtraFloors(t.name); var xw = ExtraWalls(t.name);
                        if (xf.Length > 0 && (h >> 16) % 100 < 45) env.floorTex = xf[(h >> 20) % xf.Length];
                        if (xw.Length > 0 && (h >> 17) % 100 < 45) env.wallTex = xw[(h >> 22) % xw.Length];""")
rep("long key = ((((long)env.floorTex * 8 + env.wallTex) * 8 + env.tint) * 8 + env.light) * 8 + env.debris;",
    "long key = ((((long)env.floorTex * 16 + env.wallTex) * 8 + env.tint) * 8 + env.light) * 8 + env.debris;")
helper = '''
        // 테마별로 어울리는 새 바닥/벽 질감 (위 번호 참고)
        static int[] ExtraFloors(string name)
        {
            switch (name)
            {
                case "보일러실": case "기계실": case "배전실": case "정비실": case "환기실": return I(11, 12);
                case "수조실": case "폐기물실": case "창고": case "냉동 창고": return I(12, 14);
                case "샤워실": case "소독실": case "수술실": case "수술 준비실": case "영안실": case "부검실": case "병리실": case "탈의실": return I(10, 12);
                case "병실": case "소아 병실": case "격리 병실": case "격리실": case "중환자실": case "회복실": case "처치실": case "주사실": case "응급 처치실": return I(9, 10);
                case "정신과 격리실": return I(13, 9);
                case "원장실": case "회의실": case "VIP 병실": case "도서실": case "예배실": case "면회실": case "상담실": case "진료실": case "의국": return I(8, 13);
                case "취사장": case "급식실": case "구내식당": case "세탁실": case "린넨실": return I(10, 9);
                default: return I(8, 14);
            }
        }

        static int[] ExtraWalls(string name)
        {
            switch (name)
            {
                case "보일러실": case "기계실": case "배전실": case "정비실": case "환기실": case "창고": case "냉동 창고": case "폐기물실": return I(6, 8);
                case "수조실": case "샤워실": case "세탁실": case "린넨실": return I(8, 9);
                case "취사장": case "급식실": case "구내식당": return I(9, 7);
                case "영안실": case "안치실": case "부검실": case "정신과 격리실": case "격리실": case "격리 병실": return I(11, 6);
                case "원장실": case "회의실": case "도서실": case "예배실": case "VIP 병실": case "면회실": return I(10, 9);
                default: return I(7, 10);
            }
        }
'''
rep("        // 특별한 방 이름 (층, 방): 정문 로비, 시작 방, 간호사실, 승강기 앞", helper + "\n        // 특별한 방 이름 (층, 방): 정문 로비, 시작 방, 간호사실, 승강기 앞")
open(p, "w", encoding="utf-8", newline="").write(s)

p = "Assets/Scripts/Editor/EnvBuilder.cs"
s = open(p, encoding="utf-8").read()
rep('''            "FloorWood.png", "FloorLino.png", "FloorCarpet.png", "FloorBlood.png",
        };''', '''            "FloorWood.png", "FloorLino.png", "FloorCarpet.png", "FloorBlood.png",
            "FloorParquet.png", "FloorRubber.png", "FloorMosaic.png", "FloorGrate.png", "FloorFlooded.png", "FloorBurned.png", "FloorGravel.png",
        };''')
rep('''            "WallDecay.png", "WallTile.png", "WallBlue.png", "WallWood.png", "WallGraffiti.png", "WallPaper.png",
        };''', '''            "WallDecay.png", "WallTile.png", "WallBlue.png", "WallWood.png", "WallGraffiti.png", "WallPaper.png",
            "WallBunker.png", "WallStripe.png", "WallMold.png", "WallBrick.png", "WallPanel.png", "WallBlood.png",
        };''')
open(p, "w", encoding="utf-8", newline="").write(s)

p = "Assets/Scripts/Editor/HospitalBuilder.cs"
s = open(p, encoding="utf-8").read()
rep('''            { "MedicalWasteBin", new PropSpec(0.95f, 0.65f) },''', '''            { "MedicalWasteBin", new PropSpec(0.95f, 0.65f) },
            { "CrashCart",     new PropSpec(1.0f,  0.8f) },
            { "Incubator",     new PropSpec(1.1f,  1.0f) },
            { "LabBench",      new PropSpec(1.0f,  1.8f) },
            { "AutopsyTable",  new PropSpec(0.95f, 2.0f) },
            { "Coffin",        new PropSpec(0.95f, 2.0f) },
            { "Skeleton",      new PropSpec(1.8f,  0.8f) },
            { "Crutches",      new PropSpec(1.3f,  0.7f) },
            { "FireExtinguisher", new PropSpec(1.2f, 0.5f) },
            { "WaterCooler",   new PropSpec(1.2f,  0.5f) },
            { "TVCart",        new PropSpec(1.6f,  0.8f) },
            { "RockingChair",  new PropSpec(1.0f,  0.8f) },
            { "ToyChest",      new PropSpec(0.7f,  0.8f) },
            { "Piano",         new PropSpec(1.4f,  1.6f) },
            { "Generator",     new PropSpec(1.4f,  1.8f) },
            { "PipeManifold",  new PropSpec(2.2f,  1.0f) },
            { "LaundryCart",   new PropSpec(1.0f,  1.0f) },
            { "FoodTrolley",   new PropSpec(1.3f,  0.9f) },
            { "Safe",          new PropSpec(1.1f,  0.8f) },
            { "Armchair",      new PropSpec(0.9f,  0.9f) },
            { "Sofa",          new PropSpec(0.9f,  1.9f) },
            { "KitchenStove",  new PropSpec(0.95f, 1.3f) },
            { "CrackedMirror", new PropSpec(1.9f,  0.9f) },
            { "Statue",        new PropSpec(1.8f,  0.8f) },
            { "Confessional",  new PropSpec(2.3f,  1.4f) },''')
rep('''            { "FoldingScreen", ("칸막이 뒤에 숨는다", -1f) },''', '''            { "FoldingScreen", ("칸막이 뒤에 숨는다", -1f) },
            { "Coffin", ("관 뒤에 숨는다", 0.55f) },
            { "LabBench", ("실험대 밑에 숨는다", 0.55f) },
            { "LaundryCart", ("세탁 카트 뒤에 숨는다", 0.55f) },
            { "Confessional", ("고해소 안에 숨는다", 1.5f) },''')
rep('string[] models = { "WaitingBench", "Wheelchair", "IVStand", "MedicineCart", "WaitingBench", "FoldingScreen" };',
    'string[] models = { "WaitingBench", "Wheelchair", "IVStand", "MedicineCart", "WaitingBench", "FoldingScreen", "FireExtinguisher", "WaterCooler", "Crutches", "CrashCart" };')
open(p, "w", encoding="utf-8", newline="").write(s)
print("ok")
