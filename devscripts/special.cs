using UnityEngine; using Escape.Core; using System.Linq;
int vis = 0, hiddenRooms = 0, visCorr = 0;
for (int r = 0; r < TowerLayout.RoomsPerFloor; r++) { bool visible = r % 3 != 0; if (TowerLayout.IsCorridorRoom(r)) { if (visible) visCorr++; } else { if (visible) vis++; else hiddenRooms++; } }
int firstRoom0 = Enumerable.Range(0, TowerLayout.RoomsPerFloor).Where(r => !TowerLayout.IsCorridorRoom(r) && TowerLayout.RoomRect(r).xMin < 0.1f && TowerLayout.RoomRect(r).yMin < 0.1f).First();
return $"보이는 방 {vis}, 숨은 방 {hiddenRooms}, 보이는 복도 구역 {visCorr} | 특별: 정문로비 {TowerLayout.ExitRoom}%3={TowerLayout.ExitRoom % 3}, 시작방 {TowerLayout.StartRoom}%3={TowerLayout.StartRoom % 3}, 간호사실 {TowerLayout.NurseStationRoom}%3={TowerLayout.NurseStationRoom % 3}, 전기실문 방 {firstRoom0}%3={firstRoom0 % 3}";
