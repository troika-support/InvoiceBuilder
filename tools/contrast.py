"""WCAG 2.2 contrast check for Invoice Builder tokens. Run: python tools/contrast.py"""
def lum(h):
    h=h.lstrip('#'); r,g,b=[int(h[i:i+2],16)/255 for i in (0,2,4)]
    f=lambda c: c/12.92 if c<=0.03928 else ((c+0.055)/1.055)**2.4
    return 0.2126*f(r)+0.7152*f(g)+0.0722*f(b)
def cr(a,b):
    la,lb=sorted([lum(a),lum(b)],reverse=True); return (la+0.05)/(lb+0.05)
pairs = {
 "text / surface (L)":("#0F172A","#FFFFFF"), "text / bg (L)":("#0F172A","#F4F5F7"),
 "text-2 / surface (L)":("#334155","#FFFFFF"), "muted / surface (L)":("#434E60","#FFFFFF"),
 "muted / bg (L)":("#434E60","#F4F5F7"), "primary link / surface (L)":("#115E59","#FFFFFF"),
 "on-primary / primary (L)":("#FFFFFF","#115E59"), "primary-soft text (L)":("#0B4F4A","#E6F4F2"),
 "danger / surface (L)":("#991B1B","#FFFFFF"), "success / surface (L)":("#166534","#FFFFFF"),
 "nav text / nav bg":("#CBD5E1","#0F172A"), "nav muted / nav bg":("#A9B4C4","#0F172A"),
 "nav active":("#FFFFFF","#134E4A"),
 "pill draft (L)":("#1E293B","#E2E8F0"), "pill info (L)":("#1E3A8A","#DBEAFE"),
 "pill accept (L)":("#134E4A","#CCFBF1"), "pill warn (L)":("#78350F","#FEF3C7"),
 "pill ok (L)":("#14532D","#DCFCE7"), "pill bad (L)":("#7F1D1D","#FEE2E2"),
 "text / surface (D)":("#F1F5F9","#111827"), "text / bg (D)":("#F1F5F9","#0B1120"),
 "text-2 / surface (D)":("#CBD5E1","#111827"), "muted / surface (D)":("#A3B0C2","#111827"),
 "primary link / surface (D)":("#2DD4BF","#111827"), "on-primary / primary (D)":("#042F2E","#2DD4BF"),
 "primary-soft text (D)":("#CCFBF1","#134E4A"), "danger / surface (D)":("#FCA5A5","#111827"),
 "success / surface (D)":("#86EFAC","#111827"),
 "pill draft (D)":("#E2E8F0","#334155"), "pill info (D)":("#DBEAFE","#1E3A8A"),
 "pill accept (D)":("#CCFBF1","#134E4A"), "pill warn (D)":("#FEF3C7","#78350F"),
 "pill ok (D)":("#DCFCE7","#14532D"), "pill bad (D)":("#FEE2E2","#7F1D1D"),
 "Troika: ink on accent":("#111111","#F59E0B"), "Troika: client muted":("#44403C","#F5F5F4"),
}
worst=99
for k,(a,b) in pairs.items():
    c=cr(a,b); worst=min(worst,c); lvl="AAA" if c>=7 else ("AA" if c>=4.5 else ("AA-large" if c>=3 else "FAIL"))
    print(f"{k:30s} {a} on {b}  {c:5.2f}  {lvl}")
print(f"lowest ratio: {worst:.2f}")
