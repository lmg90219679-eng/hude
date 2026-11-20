using Escape.Core;
bool ok = TowerLayout.IsLayoutLocked(out string r); return ok ? "평면 잠금 일치" : "불일치: " + r;
