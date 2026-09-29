# Test de robustesse du toggle overlay via le bouton de barre des tâches (Views/TaskbarIndicatorWindow).
#
# Pourquoi ce script existe : un bug réel a été découvert (sept. 2026) où un seul clic sur
# l'icône de barre des tâches déclenchait jusqu'à 3 bascules en cascade de l'overlay (course entre
# le re-minimize du code et la restauration native de Windows), rendant le comportement
# imprévisible ("parfois ça apparaît, parfois ça disparaît"). Corrigé par un report du re-minimize
# (Dispatcher.BeginInvoke) + un anti-rebond de 400ms dans TaskbarIndicatorWindow.xaml.cs.
#
# Ce script simule le vrai chemin d'un clic utilisateur (WM_SYSCOMMAND/SC_RESTORE envoyé
# directement à la fenêtre technique — le message exact que Windows envoie pour restaurer une
# fenêtre minimisée depuis son bouton de barre des tâches) plutôt que de cliquer virtuellement à
# des coordonnées d'écran, ce qui serait fragile et non reproductible.
#
# NOTES TECHNIQUES (bugs PowerShell rencontrés en écrivant ce test, pour éviter de les reproduire) :
#  - Un scriptblock passé comme délégué EnumWindows natif, défini À L'INTÉRIEUR d'une fonction
#    plutôt qu'au niveau racine du script, ne capture pas fiablement les variables englobantes en
#    exécution via -File (ni GetNewClosure() ni $script: ne l'ont résolu) -> tout est à plat ici.
#  - Appeler IsIconic/IsWindowVisible DEPUIS le callback EnumWindows (au-delà de 2 P/Invoke)
#    casse silencieusement toute l'énumération en -File -> on fait l'énumération (hWnd + titre)
#    dans un premier passage minimal, puis on classe chaque hWnd (Iconic/Visible) dans une boucle
#    PowerShell normale ensuite, hors de tout callback natif.
#
# À relancer après toute modification de TaskbarIndicatorWindow.xaml.cs, App.xaml.cs (ToggleOverlay)
# ou OverlayWindow.xaml.cs. Prérequis : l'appli doit déjà tourner.
#
# Usage : powershell -File tests\manual\overlay-toggle-robustness.ps1

Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class OverlayRobustnessTest {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    public const uint WM_SYSCOMMAND = 0x0112;
    public const int SC_RESTORE = 0xF120;
}
"@

$targetProcess = Get-Process VoiceToText -ErrorAction SilentlyContinue
if (-not $targetProcess) {
    Write-Host "ERREUR : VoiceToText.exe n'est pas lancé. Démarre l'appli d'abord." -ForegroundColor Red
    exit 1
}
$targetPid = $targetProcess.Id
Write-Host "Cible : PID $targetPid"

# Trouve les 2 fenêtres "Dictée universelle" (overlay flottant + TaskbarIndicatorWindow) : passage
# d'énumération minimal (2 P/Invoke seulement dans le callback), puis classification hors callback.
$rawHandles = New-Object System.Collections.Generic.List[IntPtr]
[OverlayRobustnessTest]::EnumWindows({
    param($hWnd, $lParam)
    $procId = [uint32]0
    [OverlayRobustnessTest]::GetWindowThreadProcessId($hWnd, [ref]$procId) | Out-Null
    if ($procId -eq $targetPid) {
        $sb = New-Object System.Text.StringBuilder 256
        [OverlayRobustnessTest]::GetWindowText($hWnd, $sb, 256) | Out-Null
        if ($sb.ToString() -eq "Dictée universelle") {
            $rawHandles.Add($hWnd)
        }
    }
    return $true
}, [IntPtr]::Zero) | Out-Null

$tbHwnd = [IntPtr]::Zero
$overlayHwnd = [IntPtr]::Zero
foreach ($h in $rawHandles) {
    if ([OverlayRobustnessTest]::IsIconic($h)) {
        $tbHwnd = $h
    } else {
        $overlayHwnd = $h
    }
}

if ($tbHwnd -eq [IntPtr]::Zero -or $overlayHwnd -eq [IntPtr]::Zero) {
    Write-Host "ERREUR : fenêtres introuvables (taskbar=$tbHwnd, overlay=$overlayHwnd)." -ForegroundColor Red
    exit 1
}
Write-Host "TaskbarIndicatorWindow hWnd = $tbHwnd / OverlayWindow hWnd = $overlayHwnd"
Write-Host ""

$failures = 0

Write-Host "--- Test 1 : 8 clics espacés (600ms) doivent alterner proprement ---"
for ($i = 1; $i -le 8; $i++) {
    [OverlayRobustnessTest]::PostMessage($tbHwnd, [OverlayRobustnessTest]::WM_SYSCOMMAND, [IntPtr][OverlayRobustnessTest]::SC_RESTORE, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 600

    $visible = [OverlayRobustnessTest]::IsWindowVisible($overlayHwnd)
    $expected = ($i % 2 -eq 1)
    $ok = $visible -eq $expected
    if (-not $ok) { $failures++ }
    $mark = if ($ok) { "OK" } else { "ECHEC (attendu $expected)" }
    Write-Host "  Clic $i -> visible=$visible [$mark]"
}

Write-Host ""
Write-Host "--- Test 2 : double-clic rapide (50ms d'écart) doit compter comme UN SEUL toggle ---"
$beforeState = [OverlayRobustnessTest]::IsWindowVisible($overlayHwnd)
[OverlayRobustnessTest]::PostMessage($tbHwnd, [OverlayRobustnessTest]::WM_SYSCOMMAND, [IntPtr][OverlayRobustnessTest]::SC_RESTORE, [IntPtr]::Zero) | Out-Null
Start-Sleep -Milliseconds 50
[OverlayRobustnessTest]::PostMessage($tbHwnd, [OverlayRobustnessTest]::WM_SYSCOMMAND, [IntPtr][OverlayRobustnessTest]::SC_RESTORE, [IntPtr]::Zero) | Out-Null
Start-Sleep -Milliseconds 600
$afterState = [OverlayRobustnessTest]::IsWindowVisible($overlayHwnd)

$ok = $afterState -ne $beforeState
if (-not $ok) { $failures++ }
$mark = if ($ok) { "OK" } else { "ECHEC (visible avant/après identique : $beforeState/$afterState, double-clic pas absorbé)" }
Write-Host "  Avant=$beforeState Après=$afterState [$mark]"

Write-Host ""
if ($failures -eq 0) {
    Write-Host "TOUS LES TESTS PASSENT" -ForegroundColor Green
} else {
    Write-Host "$failures ECHEC(S) DETECTE(S)" -ForegroundColor Red
    exit 1
}
